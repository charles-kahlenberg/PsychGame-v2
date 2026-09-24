// Keeps the Dropbox report folder in sync with D1, incrementally:
//   1. Rows newer than csv_sync_state's per-table rowid are appended to the raw
//      CSV, and their sessions are queued in csv_dirty_sessions.
//   2. Each queued session is re-rendered from its own (indexed) rows: its
//      timeline file is rewritten and its pre-rendered overview/scenario
//      lines are stored in csv_session_reports.
//   3. The two summary files are rebuilt from those stored lines, which is
//      one small row per session — no re-scan of the event tables.
// A lease lock in csv_meta keeps overlapping triggers from racing; anything a
// skipped trigger would have handled is picked up by the running sync's next
// pass or by the cron fallback.

import scenariosFile from "../../../Assets/Resources/Scenarios.txt";
import { createDropboxClient, isDropboxConfigured } from "./dropbox.js";
import {
  ATTEMPT_COLUMNS,
  BOM,
  OVERVIEW_COLUMNS,
  LAYOUT_VERSION,
  buildReadme,
  buildSessionReport,
  csvFile,
  parseScenarioList,
} from "./reports.js";

const SCENARIO_TEXTS = parseScenarioList(scenariosFile);

const SYNC_TABLE_NAMES = ["sessions", "click_events", "ai_responses", "user_responses", "card_events", "menu_durations"];
const MAX_ROWS_PER_TABLE_PER_PASS = 2000;
const MAX_DROPBOX_CALLS = 40; // stays under the free plan's 50 outbound fetches
const MAX_SESSIONS_PER_PASS = 25;
const LOCK_LEASE_MS = 90_000;

const folder = (env) => env.DROPBOX_FOLDER || "/PsychGameLogs";

// --- Raw event log (one row per event, all tables mixed) ------------------

const RAW_COLUMNS = [
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
const RAW_HEADER = RAW_COLUMNS.join(",");

function rawRecord(tableName, row) {
  const base = { ...row, table_name: tableName };
  if (tableName === "sessions") {
    return { ...base, timestamp: row.started_at, timestamp_et: row.started_at_et };
  }
  return base;
}

function rawEscape(value) {
  if (value === null || value === undefined) return "";
  const str = String(value);
  return /[",\n\r]/.test(str) ? `"${str.replace(/"/g, '""')}"` : str;
}

const rawLine = (record) => RAW_COLUMNS.map((col) => rawEscape(record[col])).join(",");

async function getLastRowids(env) {
  await env.DB.batch(
    SYNC_TABLE_NAMES.map((name) =>
      env.DB.prepare(`INSERT OR IGNORE INTO csv_sync_state (table_name, last_rowid) VALUES (?, 0)`).bind(name)
    )
  );
  const { results } = await env.DB.prepare(`SELECT table_name, last_rowid FROM csv_sync_state`).all();
  return Object.fromEntries(results.map((r) => [r.table_name, r.last_rowid]));
}

async function collectNewRawRows(env) {
  const lastRowids = await getLastRowids(env);

  // Table names come from the fixed whitelist above, never from request input.
  const results = await env.DB.batch(
    SYNC_TABLE_NAMES.map((name) =>
      env.DB.prepare(`SELECT rowid AS row_id, * FROM ${name} WHERE rowid > ?1 ORDER BY rowid ASC LIMIT ?2`).bind(
        lastRowids[name] || 0,
        MAX_ROWS_PER_TABLE_PER_PASS
      )
    )
  );

  const lines = [];
  const sessionIds = new Set();
  const newLastRowid = {};
  SYNC_TABLE_NAMES.forEach((name, i) => {
    const rows = results[i].results;
    if (rows.length === 0) return;
    for (const row of rows) {
      lines.push(rawLine(rawRecord(name, row)));
      sessionIds.add(row.session_id);
    }
    newLastRowid[name] = rows[rows.length - 1].row_id;
  });
  return { lines, sessionIds, newLastRowid };
}

// Returns how many new rows were appended.
async function appendNewRawRows(env, dropbox) {
  let batch = await collectNewRawRows(env);
  if (batch.lines.length === 0) return 0;

  // Queue sessions before touching Dropbox, so a failed upload still leaves
  // them marked for a later retry.
  await markSessionsDirty(env, [...batch.sessionIds]);

  // Append with Dropbox rev-based locking; a conflict means the file changed
  // underneath us, so re-download and retry.
  const path = `${folder(env)}/Raw Data/all_events.csv`;
  for (let attempt = 1; ; attempt++) {
    const { content, rev } = await dropbox.download(path);
    let existing = content || `${BOM}${RAW_HEADER}\n`;

    // Rows are appended positionally, so if the columns changed since this
    // file was written, appending would misalign everything. Rebuild it.
    if (existing.replace(BOM, "").split(/\r?\n/, 1)[0] !== RAW_HEADER) {
      await env.DB.prepare(`UPDATE csv_sync_state SET last_rowid = 0`).run();
      batch = await collectNewRawRows(env);
      existing = `${BOM}${RAW_HEADER}\n`;
    }

    const separator = existing.endsWith("\n") ? "" : "\n";
    try {
      await dropbox.upload(path, existing + separator + batch.lines.join("\n") + "\n", { rev });
      break;
    } catch (err) {
      if (!err.conflict || attempt >= 3) throw err;
    }
  }

  await env.DB.batch(
    Object.entries(batch.newLastRowid).map(([name, rowid]) =>
      env.DB.prepare(`UPDATE csv_sync_state SET last_rowid = ? WHERE table_name = ?`).bind(rowid, name)
    )
  );
  return batch.lines.length;
}

async function markSessionsDirty(env, sessionIds) {
  if (sessionIds.length === 0) return;
  await env.DB.batch(
    sessionIds.map((id) => env.DB.prepare(`INSERT OR IGNORE INTO csv_dirty_sessions (session_id) VALUES (?)`).bind(id))
  );
}

// --- Per-session reports ---------------------------------------------------

async function loadSessionRows(env, sessionId) {
  const q = (sql) => env.DB.prepare(sql).bind(sessionId);
  const [session, clicks, aiResponses, userResponses, cardEvents, menuDurations] = await env.DB.batch([
    q(`SELECT rowid AS row_id, * FROM sessions WHERE session_id = ?`),
    q(`SELECT * FROM click_events WHERE session_id = ? ORDER BY id`),
    q(`SELECT * FROM ai_responses WHERE session_id = ? ORDER BY id`),
    q(`SELECT * FROM user_responses WHERE session_id = ? ORDER BY id`),
    q(`SELECT * FROM card_events WHERE session_id = ? ORDER BY id`),
    q(`SELECT * FROM menu_durations WHERE session_id = ? ORDER BY id`),
  ]);
  return {
    session: session.results[0] || null,
    rows: {
      clicks: clicks.results,
      aiResponses: aiResponses.results,
      userResponses: userResponses.results,
      cardEvents: cardEvents.results,
      menuDurations: menuDurations.results,
    },
  };
}

// Returns how many sessions were refreshed.
async function refreshDirtySessions(env, dropbox, limit) {
  const { results: dirty } = await env.DB.prepare(`SELECT session_id FROM csv_dirty_sessions LIMIT ?`).bind(limit).all();
  if (dirty.length === 0) return 0;

  for (const { session_id: sessionId } of dirty) {
    const { session, rows } = await loadSessionRows(env, sessionId);
    const done = env.DB.prepare(`DELETE FROM csv_dirty_sessions WHERE session_id = ?`).bind(sessionId);

    if (!session) {
      await done.run(); // events without a session row can't be named or numbered
      continue;
    }

    const report = buildSessionReport(session, rows, SCENARIO_TEXTS);
    await dropbox.upload(`${folder(env)}/Session Timelines/${report.fileName}`, report.timelineCsv);
    await env.DB.batch([
      env.DB.prepare(
        `INSERT INTO csv_session_reports (session_id, session_num, overview_line, attempt_lines) VALUES (?, ?, ?, ?)
         ON CONFLICT(session_id) DO UPDATE SET session_num = excluded.session_num,
           overview_line = excluded.overview_line, attempt_lines = excluded.attempt_lines`
      ).bind(sessionId, session.row_id, report.overviewLine, JSON.stringify(report.attemptLines)),
      done,
    ]);
  }

  const { results: reports } = await env.DB.prepare(
    `SELECT overview_line, attempt_lines FROM csv_session_reports ORDER BY session_num`
  ).all();
  await dropbox.upload(
    `${folder(env)}/Sessions Overview.csv`,
    csvFile(
      OVERVIEW_COLUMNS,
      reports.map((r) => r.overview_line)
    )
  );
  await dropbox.upload(
    `${folder(env)}/Scenario Responses.csv`,
    csvFile(
      ATTEMPT_COLUMNS,
      reports.flatMap((r) => JSON.parse(r.attempt_lines))
    )
  );
  return dirty.length;
}

// When the report layout changes, every session's stored report lines are
// stale, so queue them all for re-rendering alongside the new README.
async function applyLayoutVersion(env, dropbox) {
  const row = await env.DB.prepare(`SELECT value FROM csv_meta WHERE key = 'layout_version'`).first();
  if (row && Number(row.value) === LAYOUT_VERSION) return;

  await env.DB.prepare(`INSERT OR IGNORE INTO csv_dirty_sessions (session_id) SELECT session_id FROM sessions`).run();
  await dropbox.upload(`${folder(env)}/README.txt`, buildReadme(SCENARIO_TEXTS));
  await env.DB.prepare(
    `INSERT INTO csv_meta (key, value) VALUES ('layout_version', ?) ON CONFLICT(key) DO UPDATE SET value = excluded.value`
  )
    .bind(String(LAYOUT_VERSION))
    .run();
}

// --- Lock + entry point ---------------------------------------------------

async function acquireLock(env, leaseUntil) {
  const res = await env.DB.prepare(
    `INSERT INTO csv_meta (key, value) VALUES ('sync_lock_until', ?1)
     ON CONFLICT(key) DO UPDATE SET value = excluded.value WHERE CAST(csv_meta.value AS INTEGER) < ?2`
  )
    .bind(String(leaseUntil), Date.now())
    .run();
  return res.meta.changes > 0;
}

async function releaseLock(env, leaseUntil) {
  await env.DB.prepare(`UPDATE csv_meta SET value = '0' WHERE key = 'sync_lock_until' AND value = ?`)
    .bind(String(leaseUntil))
    .run();
}

export async function syncToDropbox(env) {
  if (!isDropboxConfigured(env)) return;

  const leaseUntil = Date.now() + LOCK_LEASE_MS;
  if (!(await acquireLock(env, leaseUntil))) return;

  try {
    const dropbox = await createDropboxClient(env);
    await applyLayoutVersion(env, dropbox);

    // Keep going while there's work, so rows written during this sync (whose
    // own trigger hit the lock) still get picked up. Each pass reserves 4
    // calls for the raw append + the two summary files.
    while (true) {
      const room = MAX_DROPBOX_CALLS - dropbox.calls - 4;
      if (room <= 0) break;
      const appended = await appendNewRawRows(env, dropbox);
      const refreshed = await refreshDirtySessions(env, dropbox, Math.min(room, MAX_SESSIONS_PER_PASS));
      if (appended === 0 && refreshed === 0) break;
    }
  } finally {
    await releaseLock(env, leaseUntil);
  }
}
