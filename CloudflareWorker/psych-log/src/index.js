// Cloudflare Worker: receives click/session logs from the Unity WebGL build
// and writes them to a D1 database. This is the only thing the client ever
// talks to directly — the D1 binding (and any future secrets) stay server-side.

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

// --- Incremental CSV export to Dropbox ---------------------------------
// Every write below ends by kicking this off in the background (ctx.waitUntil),
// so the CSV in Dropbox stays current without the client waiting on it.
// Rather than re-reading the whole database each time, csv_sync_state tracks
// the last D1 rowid we've already exported per table, so each run only reads
// and appends rows that arrived since the previous menu load.

const SYNC_TABLE_NAMES = [
  "sessions",
  "click_events",
  "ai_responses",
  "user_responses",
  "card_events",
  "menu_durations",
];

// Union of every column across all six tables, so all rows can share one CSV.
// New columns must be appended at the end, never inserted in the middle:
// the live Dropbox file's header was already written by an earlier sync, and
// rows are appended positionally, so shifting existing column positions
// would misalign every row written after the change against that header.
const CSV_COLUMNS = [
  "table_name",
  "row_id",
  "session_id",
  "timestamp",
  "timestamp_et",
  "username",
  "user_agent",
  "object_name",
  "kind",
  "scenario",
  "content",
  "response",
  "event_type",
  "cards",
  "elapsed_ms",
  "menu_name",
  "duration_ms",
  "score",
  "test_group",
  "features",
];
const CSV_HEADER = CSV_COLUMNS.join(",");

const MAX_ROWS_PER_TABLE_PER_SYNC = 2000;

function rowToCsvRecord(tableName, row) {
  const base = { table_name: tableName, row_id: row.row_id, session_id: row.session_id };
  switch (tableName) {
    case "sessions":
      return {
        ...base,
        timestamp: row.started_at,
        timestamp_et: row.started_at_et,
        username: row.username,
        user_agent: row.user_agent,
        test_group: row.test_group,
        features: row.features,
      };
    case "click_events":
      return { ...base, timestamp: row.timestamp, timestamp_et: row.timestamp_et, object_name: row.object_name };
    case "ai_responses":
      return {
        ...base,
        timestamp: row.timestamp,
        timestamp_et: row.timestamp_et,
        kind: row.kind,
        scenario: row.scenario,
        content: row.content,
        score: row.score,
      };
    case "user_responses":
      return {
        ...base,
        timestamp: row.timestamp,
        timestamp_et: row.timestamp_et,
        scenario: row.scenario,
        response: row.response,
      };
    case "card_events":
      return {
        ...base,
        timestamp: row.timestamp,
        timestamp_et: row.timestamp_et,
        scenario: row.scenario,
        event_type: row.event_type,
        cards: row.cards,
        elapsed_ms: row.elapsed_ms,
      };
    case "menu_durations":
      return {
        ...base,
        timestamp: row.timestamp,
        timestamp_et: row.timestamp_et,
        menu_name: row.menu_name,
        duration_ms: row.duration_ms,
        scenario: row.scenario,
      };
  }
}

function csvEscape(value) {
  if (value === null || value === undefined) return "";
  const str = String(value);
  if (/[",\n\r]/.test(str)) {
    return `"${str.replace(/"/g, '""')}"`;
  }
  return str;
}

function recordToCsvLine(record) {
  return CSV_COLUMNS.map((col) => csvEscape(record[col])).join(",");
}

async function ensureSyncState(env) {
  await env.DB.batch(
    SYNC_TABLE_NAMES.map((name) =>
      env.DB.prepare(`INSERT OR IGNORE INTO csv_sync_state (table_name, last_rowid) VALUES (?, 0)`).bind(name)
    )
  );
}

async function getLastRowids(env) {
  const { results } = await env.DB.prepare(`SELECT table_name, last_rowid FROM csv_sync_state`).all();
  const map = {};
  for (const r of results) map[r.table_name] = r.last_rowid;
  return map;
}

// tableName always comes from the SYNC_TABLE_NAMES whitelist above, never
// from request input, so interpolating it into the query is safe.
async function fetchNewRowsForTable(env, tableName, lastRowid) {
  const { results } = await env.DB.prepare(
    `SELECT rowid AS row_id, * FROM ${tableName} WHERE rowid > ?1 ORDER BY rowid ASC LIMIT ?2`
  )
    .bind(lastRowid, MAX_ROWS_PER_TABLE_PER_SYNC)
    .all();
  return results;
}

async function getDropboxAccessToken(env) {
  const resp = await fetch("https://api.dropbox.com/oauth2/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "refresh_token",
      refresh_token: env.DROPBOX_REFRESH_TOKEN,
      client_id: env.DROPBOX_APP_KEY,
      client_secret: env.DROPBOX_APP_SECRET,
    }),
  });
  if (!resp.ok) {
    throw new Error(`Dropbox token refresh failed: ${resp.status} ${await resp.text()}`);
  }
  const data = await resp.json();
  return data.access_token;
}

async function downloadDropboxFile(accessToken, path) {
  const resp = await fetch("https://content.dropboxapi.com/2/files/download", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "Dropbox-API-Arg": JSON.stringify({ path }),
    },
  });

  if (resp.status === 409) {
    const err = await resp.json().catch(() => null);
    if (err?.error?.[".tag"] === "path" && err.error.path?.[".tag"] === "not_found") {
      return { content: null, rev: null };
    }
    throw new Error(`Dropbox download failed: ${JSON.stringify(err)}`);
  }

  if (!resp.ok) {
    throw new Error(`Dropbox download failed: ${resp.status} ${await resp.text()}`);
  }

  const resultHeader = resp.headers.get("Dropbox-API-Result");
  const rev = resultHeader ? JSON.parse(resultHeader).rev : null;
  const content = await resp.text();
  return { content, rev };
}

async function uploadDropboxFile(accessToken, path, content, rev) {
  const mode = rev ? { ".tag": "update", update: rev } : { ".tag": "add" };
  const resp = await fetch("https://content.dropboxapi.com/2/files/upload", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "Content-Type": "application/octet-stream",
      "Dropbox-API-Arg": JSON.stringify({ path, mode, mute: true }),
    },
    body: content,
  });

  if (!resp.ok) {
    const errText = await resp.text();
    const conflict = resp.status === 409 && errText.includes("conflict");
    throw Object.assign(new Error(`Dropbox upload failed: ${resp.status} ${errText}`), { conflict });
  }

  return resp.json();
}

// Downloads the current CSV, appends the new lines, and uploads it back.
// Uses Dropbox's rev-based optimistic locking so two overlapping syncs (e.g.
// two players hitting a menu load at once) can't silently clobber each
// other's rows — a conflict just means "someone updated it first," so we
// re-download the now-current file and retry the append.
async function appendCsvLinesToDropbox(env, accessToken, newLines) {
  const path = env.DROPBOX_CSV_PATH || "/psych_log.csv";
  const MAX_ATTEMPTS = 3;

  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    const { content, rev } = await downloadDropboxFile(accessToken, path);
    const existing = content && content.length > 0 ? content : `${CSV_HEADER}\n`;
    const separator = existing.endsWith("\n") ? "" : "\n";
    const newContent = existing + separator + newLines.join("\n") + "\n";

    try {
      await uploadDropboxFile(accessToken, path, newContent, rev);
      return;
    } catch (err) {
      if (err.conflict && attempt < MAX_ATTEMPTS) continue;
      throw err;
    }
  }
}

async function syncNewRowsToDropbox(env) {
  if (!env.DROPBOX_REFRESH_TOKEN || !env.DROPBOX_APP_KEY || !env.DROPBOX_APP_SECRET) {
    return; // Dropbox secrets not configured yet
  }

  await ensureSyncState(env);
  const lastRowids = await getLastRowids(env);

  const newRowsByTable = {};
  let totalNew = 0;
  for (const tableName of SYNC_TABLE_NAMES) {
    const rows = await fetchNewRowsForTable(env, tableName, lastRowids[tableName] || 0);
    if (rows.length > 0) {
      newRowsByTable[tableName] = rows;
      totalNew += rows.length;
    }
  }

  if (totalNew === 0) return;

  const csvLines = [];
  const newLastRowid = { ...lastRowids };
  for (const tableName of SYNC_TABLE_NAMES) {
    const rows = newRowsByTable[tableName];
    if (!rows) continue;
    for (const row of rows) {
      csvLines.push(recordToCsvLine(rowToCsvRecord(tableName, row)));
    }
    newLastRowid[tableName] = rows[rows.length - 1].row_id;
  }

  const accessToken = await getDropboxAccessToken(env);
  await appendCsvLinesToDropbox(env, accessToken, csvLines);

  const updateStmts = SYNC_TABLE_NAMES.filter((name) => newLastRowid[name] !== lastRowids[name]).map((name) =>
    env.DB.prepare(`UPDATE csv_sync_state SET last_rowid = ? WHERE table_name = ?`).bind(newLastRowid[name], name)
  );
  if (updateStmts.length > 0) {
    await env.DB.batch(updateStmts);
  }
}

function triggerDropboxSync(ctx, env) {
  ctx.waitUntil(syncNewRowsToDropbox(env).catch((err) => console.error("Dropbox sync failed:", err)));
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
};
