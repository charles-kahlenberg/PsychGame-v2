// Cloudflare Worker: receives click/session logs from the Unity WebGL build
// and writes them to a D1 database. This is the only thing the client ever
// talks to directly — the D1 binding (and any future secrets) stay server-side.

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
};

function jsonResponse(obj, status = 200) {
  return new Response(JSON.stringify(obj), {
    status,
    headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
  });
}

// Formats a UTC timestamp as a human-readable Eastern time string, e.g.
// "2026-09-12 04:35:15 PM EDT". Intl handles the EST/EDT DST switch itself.
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
    hour12: true,
    timeZoneName: "short",
  }).formatToParts(date);

  const get = (type) => parts.find((p) => p.type === type)?.value ?? "";
  return `${get("year")}-${get("month")}-${get("day")} ${get("hour")}:${get("minute")}:${get("second")} ${get("dayPeriod")} ${get("timeZoneName")}`;
}

export default {
  async fetch(request, env) {
    if (request.method === "OPTIONS") {
      return new Response(null, { headers: CORS_HEADERS });
    }

    if (request.method !== "POST") {
      return jsonResponse({ error: "Method not allowed" }, 405);
    }

    const url = new URL(request.url);

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
