-- Run this once against the D1 database to create the log tables.
-- Dashboard: D1 > your database > Console > paste and run.
-- CLI:       wrangler d1 execute psych_log_db --remote --file=./schema.sql

CREATE TABLE IF NOT EXISTS sessions (
  session_id TEXT PRIMARY KEY,
  username TEXT,
  started_at TEXT NOT NULL,
  user_agent TEXT
);

CREATE TABLE IF NOT EXISTS click_events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  session_id TEXT NOT NULL,
  timestamp TEXT NOT NULL,
  object_name TEXT,
  FOREIGN KEY (session_id) REFERENCES sessions(session_id)
);
