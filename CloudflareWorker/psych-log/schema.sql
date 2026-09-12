-- Run this once against the D1 database to create the log tables.
-- Dashboard: D1 > your database > Console > paste and run.
-- CLI:       wrangler d1 execute psych_log_db --remote --file=./schema.sql

CREATE TABLE IF NOT EXISTS sessions (
  session_id TEXT PRIMARY KEY,
  username TEXT,
  started_at TEXT NOT NULL,        -- raw UTC ISO 8601, for sorting/analysis
  started_at_et TEXT NOT NULL,     -- human-readable Eastern time, e.g. "2026-09-12 04:35:15 PM EDT"
  user_agent TEXT
);

CREATE TABLE IF NOT EXISTS click_events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  session_id TEXT NOT NULL,
  timestamp TEXT NOT NULL,         -- raw UTC ISO 8601, for sorting/analysis
  timestamp_et TEXT NOT NULL,      -- human-readable Eastern time, e.g. "2026-09-12 04:35:15 PM EDT"
  object_name TEXT,
  FOREIGN KEY (session_id) REFERENCES sessions(session_id)
);
