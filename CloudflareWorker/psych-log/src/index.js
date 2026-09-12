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
        await env.DB.prepare(
          `INSERT INTO sessions (session_id, username, started_at, user_agent)
           VALUES (?, ?, ?, ?)
           ON CONFLICT(session_id) DO NOTHING`
        )
          .bind(sessionId, username || null, new Date().toISOString(), userAgent)
          .run();

        return jsonResponse({ ok: true });
      }

      if (url.pathname === "/log") {
        const { sessionId, timestamp, objectName } = body;
        if (!sessionId || !timestamp) {
          return jsonResponse({ error: "Missing sessionId or timestamp" }, 400);
        }

        await env.DB.prepare(
          `INSERT INTO click_events (session_id, timestamp, object_name) VALUES (?, ?, ?)`
        )
          .bind(sessionId, timestamp, objectName || null)
          .run();

        return jsonResponse({ ok: true });
      }

      return jsonResponse({ error: "Not found" }, 404);
    } catch (err) {
      return jsonResponse({ error: "Server error", details: String(err) }, 500);
    }
  },
};
