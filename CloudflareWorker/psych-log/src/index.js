// Cloudflare Worker: receives click/session logs from the Unity WebGL build
// and writes them to a D1 database. This is the only thing the client ever
// talks to directly — the D1 binding (and any future secrets) stay server-side.

import { syncToDropbox } from "./sync.js";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
};

function jsonResponse(obj, status = 200) {
  return new Response(JSON.stringify(obj), {
    status,
    headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
  });
}

// Formats a UTC timestamp as a human-readable Eastern time string, e.g.
// "2026-09-12 04:35:15.748 PM EDT". Intl handles the EST/EDT DST switch
// itself. Millisecond precision matters here since clicks in the same
// scenario often land within the same second.
function toEasternString(dateInput) {
  const date = typeof dateInput === "string" ? new Date(dateInput) : dateInput;
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: "America/New_York",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    fractionalSecondDigits: 3,
    hour12: true,
    timeZoneName: "short",
  }).formatToParts(date);

  const get = (type) => parts.find((p) => p.type === type)?.value ?? "";
  return `${get("year")}-${get("month")}-${get("day")} ${get("hour")}:${get("minute")}:${get("second")}.${get("fractionalSecond")} ${get("dayPeriod")} ${get("timeZoneName")}`;
}

// Every write ends by refreshing the Dropbox reports in the background, so the
// client never waits on Dropbox. See sync.js for how that stays incremental.
function triggerDropboxSync(ctx, env) {
  ctx.waitUntil(syncToDropbox(env).catch((err) => console.error("Dropbox sync failed:", err)));
}

// Builds the D1 prepared statements for one /log/batch request. Client sends
// everything it cached since the last menu load in one payload instead of
// one request per event; malformed entries (missing required fields) are
// skipped rather than failing the whole batch.
function buildBatchStatements(env, body) {
  const statements = [];

  for (const c of body.clicks || []) {
    if (!c.sessionId || !c.timestamp) continue;
    statements.push(
      env.DB.prepare(
        `INSERT INTO click_events (session_id, timestamp, timestamp_et, object_name) VALUES (?, ?, ?, ?)`
      ).bind(c.sessionId, c.timestamp, toEasternString(c.timestamp), c.objectName || null)
    );
  }

  for (const a of body.aiResponses || []) {
    if (!a.sessionId || !a.timestamp || !a.kind) continue;
    statements.push(
      env.DB.prepare(
        `INSERT INTO ai_responses (session_id, timestamp, timestamp_et, kind, scenario, content, score) VALUES (?, ?, ?, ?, ?, ?, ?)`
      ).bind(
        a.sessionId,
        a.timestamp,
        toEasternString(a.timestamp),
        a.kind,
        a.scenario || null,
        a.content || null,
        typeof a.score === "number" && a.score >= 0 ? a.score : null
      )
    );
  }

  for (const u of body.userResponses || []) {
    if (!u.sessionId || !u.timestamp) continue;
    statements.push(
      env.DB.prepare(
        `INSERT INTO user_responses (session_id, timestamp, timestamp_et, scenario, response) VALUES (?, ?, ?, ?, ?)`
      ).bind(u.sessionId, u.timestamp, toEasternString(u.timestamp), u.scenario || null, u.response || null)
    );
  }

  for (const ce of body.cardEvents || []) {
    if (!ce.sessionId || !ce.timestamp || !ce.eventType) continue;
    statements.push(
      env.DB.prepare(
        `INSERT INTO card_events (session_id, timestamp, timestamp_et, scenario, event_type, cards, elapsed_ms) VALUES (?, ?, ?, ?, ?, ?, ?)`
      ).bind(
        ce.sessionId,
        ce.timestamp,
        toEasternString(ce.timestamp),
        ce.scenario || null,
        ce.eventType,
        ce.cards || null,
        typeof ce.elapsedMs === "number" && ce.elapsedMs >= 0 ? ce.elapsedMs : null
      )
    );
  }

  for (const md of body.menuDurations || []) {
    if (!md.sessionId || !md.timestamp || !md.menuName || typeof md.durationMs !== "number") continue;
    statements.push(
      env.DB.prepare(
        `INSERT INTO menu_durations (session_id, timestamp, timestamp_et, menu_name, duration_ms, scenario) VALUES (?, ?, ?, ?, ?, ?)`
      ).bind(md.sessionId, md.timestamp, toEasternString(md.timestamp), md.menuName, md.durationMs, md.scenario || null)
    );
  }

  return statements;
}

export default {
  async fetch(request, env, ctx) {
    if (request.method === "OPTIONS") {
      return new Response(null, { headers: CORS_HEADERS });
    }

    const url = new URL(request.url);

    if (request.method !== "POST") {
      return jsonResponse({ error: "Method not allowed" }, 405);
    }

    let body;
    try {
      body = await request.json();
    } catch {
      return jsonResponse({ error: "Invalid JSON" }, 400);
    }

    try {
      if (url.pathname === "/session/start") {
        const { sessionId, username, testGroup, features } = body;
        if (!sessionId) return jsonResponse({ error: "Missing sessionId" }, 400);

        const userAgent = request.headers.get("User-Agent") || "";
        const startedAt = new Date();
        await env.DB.prepare(
          `INSERT INTO sessions (session_id, username, started_at, started_at_et, user_agent, test_group, features)
           VALUES (?, ?, ?, ?, ?, ?, ?)
           ON CONFLICT(session_id) DO NOTHING`
        )
          .bind(
            sessionId,
            username || null,
            startedAt.toISOString(),
            toEasternString(startedAt),
            userAgent,
            typeof testGroup === "number" && testGroup > 0 ? testGroup : null,
            typeof features === "string" ? features : null
          )
          .run();

        triggerDropboxSync(ctx, env);
        return jsonResponse({ ok: true });
      }

      if (url.pathname === "/log") {
        const { sessionId, timestamp, objectName } = body;
        if (!sessionId || !timestamp) {
          return jsonResponse({ error: "Missing sessionId or timestamp" }, 400);
        }

        await env.DB.prepare(
          `INSERT INTO click_events (session_id, timestamp, timestamp_et, object_name) VALUES (?, ?, ?, ?)`
        )
          .bind(sessionId, timestamp, toEasternString(timestamp), objectName || null)
          .run();

        triggerDropboxSync(ctx, env);
        return jsonResponse({ ok: true });
      }

      if (url.pathname === "/log/ai-response") {
        const { sessionId, timestamp, kind, scenario, content, score } = body;
        if (!sessionId || !timestamp || !kind) {
          return jsonResponse({ error: "Missing sessionId, timestamp, or kind" }, 400);
        }

        await env.DB.prepare(
          `INSERT INTO ai_responses (session_id, timestamp, timestamp_et, kind, scenario, content, score) VALUES (?, ?, ?, ?, ?, ?, ?)`
        )
          .bind(
            sessionId,
            timestamp,
            toEasternString(timestamp),
            kind,
            scenario || null,
            content || null,
            typeof score === "number" && score >= 0 ? score : null
          )
          .run();

        triggerDropboxSync(ctx, env);
        return jsonResponse({ ok: true });
      }

      if (url.pathname === "/log/user-response") {
        const { sessionId, timestamp, scenario, response } = body;
        if (!sessionId || !timestamp) {
          return jsonResponse({ error: "Missing sessionId or timestamp" }, 400);
        }

        await env.DB.prepare(
          `INSERT INTO user_responses (session_id, timestamp, timestamp_et, scenario, response) VALUES (?, ?, ?, ?, ?)`
        )
          .bind(sessionId, timestamp, toEasternString(timestamp), scenario || null, response || null)
          .run();

        triggerDropboxSync(ctx, env);
        return jsonResponse({ ok: true });
      }

      if (url.pathname === "/log/card-event") {
        const { sessionId, timestamp, scenario, eventType, cards, elapsedMs } = body;
        if (!sessionId || !timestamp || !eventType) {
          return jsonResponse({ error: "Missing sessionId, timestamp, or eventType" }, 400);
        }

        await env.DB.prepare(
          `INSERT INTO card_events (session_id, timestamp, timestamp_et, scenario, event_type, cards, elapsed_ms) VALUES (?, ?, ?, ?, ?, ?, ?)`
        )
          .bind(
            sessionId,
            timestamp,
            toEasternString(timestamp),
            scenario || null,
            eventType,
            cards || null,
            typeof elapsedMs === "number" && elapsedMs >= 0 ? elapsedMs : null
          )
          .run();

        triggerDropboxSync(ctx, env);
        return jsonResponse({ ok: true });
      }

      if (url.pathname === "/log/batch") {
        const statements = buildBatchStatements(env, body);
        if (statements.length > 0) {
          await env.DB.batch(statements);
        }

        triggerDropboxSync(ctx, env);
        return jsonResponse({ ok: true, count: statements.length });
      }

      if (url.pathname === "/log/menu-duration") {
        const { sessionId, timestamp, menuName, durationMs, scenario } = body;
        if (!sessionId || !timestamp || !menuName || typeof durationMs !== "number") {
          return jsonResponse({ error: "Missing sessionId, timestamp, menuName, or durationMs" }, 400);
        }

        await env.DB.prepare(
          `INSERT INTO menu_durations (session_id, timestamp, timestamp_et, menu_name, duration_ms, scenario) VALUES (?, ?, ?, ?, ?, ?)`
        )
          .bind(sessionId, timestamp, toEasternString(timestamp), menuName, durationMs, scenario || null)
          .run();

        triggerDropboxSync(ctx, env);
        return jsonResponse({ ok: true });
      }

      return jsonResponse({ error: "Not found" }, 404);
    } catch (err) {
      return jsonResponse({ error: "Server error", details: String(err) }, 500);
    }
  },

  // Fallback for anything a write-triggered sync skipped or failed on.
  async scheduled(event, env, ctx) {
    triggerDropboxSync(ctx, env);
  },
};
