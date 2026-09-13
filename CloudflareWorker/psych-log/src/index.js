// Cloudflare Worker: receives click/session logs from the Unity WebGL build
// and writes them to a D1 database. This is the only thing the client ever
// talks to directly — the D1 binding (and any future secrets) stay server-side.

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type, X-Export-Key",
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

// Research data export. Kept separate from the write path above: the game
// client posts to /session/start and /log with no auth (it has to, it's a
// public WebGL build), but reading exported data back out — including
// free-text participant responses in later variables — needs a shared
// secret so the export URLs aren't scrapeable by anyone who finds them.
function isAuthorizedExport(request, url, env) {
  if (!env.EXPORT_KEY) return false;
  const provided = request.headers.get("X-Export-Key") || url.searchParams.get("key");
  return provided === env.EXPORT_KEY;
}

async function handleListSessions(env) {
  const { results } = await env.DB.prepare(
    `SELECT s.session_id, s.username, s.started_at, s.started_at_et, s.user_agent,
            (SELECT COUNT(*) FROM click_events c WHERE c.session_id = s.session_id) AS click_count
     FROM sessions s
     ORDER BY s.started_at DESC`
  ).all();

  return jsonResponse({ sessions: results });
}

async function handleExportSession(env, sessionId) {
  const session = await env.DB.prepare(`SELECT * FROM sessions WHERE session_id = ?`)
    .bind(sessionId)
    .first();

  if (!session) return jsonResponse({ error: "Session not found" }, 404);

  const { results: clickEvents } = await env.DB.prepare(
    `SELECT id, timestamp, timestamp_et, object_name FROM click_events WHERE session_id = ? ORDER BY id ASC`
  )
    .bind(sessionId)
    .all();

  const document = { ...session, click_events: clickEvents };

  return new Response(JSON.stringify(document, null, 2), {
    headers: {
      ...CORS_HEADERS,
      "Content-Type": "application/json",
      "Content-Disposition": `attachment; filename="session_${sessionId}.json"`,
    },
  });
}

export default {
  async fetch(request, env) {
    if (request.method === "OPTIONS") {
      return new Response(null, { headers: CORS_HEADERS });
    }

    const url = new URL(request.url);

    if (request.method === "GET" && url.pathname.startsWith("/export/")) {
      if (!isAuthorizedExport(request, url, env)) {
        return jsonResponse({ error: "Unauthorized" }, 401);
      }

      if (url.pathname === "/export/sessions") {
        return handleListSessions(env);
      }

      const sessionPrefix = "/export/session/";
      if (url.pathname.startsWith(sessionPrefix)) {
        const sessionId = decodeURIComponent(url.pathname.slice(sessionPrefix.length));
        return handleExportSession(env, sessionId);
      }

      return jsonResponse({ error: "Not found" }, 404);
    }

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
        const { sessionId, username } = body;
        if (!sessionId) return jsonResponse({ error: "Missing sessionId" }, 400);

        const userAgent = request.headers.get("User-Agent") || "";
        const startedAt = new Date();
        await env.DB.prepare(
          `INSERT INTO sessions (session_id, username, started_at, started_at_et, user_agent)
           VALUES (?, ?, ?, ?, ?)
           ON CONFLICT(session_id) DO NOTHING`
        )
          .bind(sessionId, username || null, startedAt.toISOString(), toEasternString(startedAt), userAgent)
          .run();

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

        return jsonResponse({ ok: true });
      }

      return jsonResponse({ error: "Not found" }, 404);
    } catch (err) {
      return jsonResponse({ error: "Server error", details: String(err) }, 500);
    }
  },
};
