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

-- Every AI-generated piece of text the player sees: Brainy's grading
-- feedback and the brain's hints / concept-help responses.
CREATE TABLE IF NOT EXISTS ai_responses (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  session_id TEXT NOT NULL,
  timestamp TEXT NOT NULL,
  timestamp_et TEXT NOT NULL,
  kind TEXT NOT NULL,              -- 'grading_feedback', 'hint', or 'concept_help'
  scenario TEXT,
  content TEXT,
  FOREIGN KEY (session_id) REFERENCES sessions(session_id)
);

-- Every scenario response the player writes and submits.
CREATE TABLE IF NOT EXISTS user_responses (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  session_id TEXT NOT NULL,
  timestamp TEXT NOT NULL,
  timestamp_et TEXT NOT NULL,
  scenario TEXT,
  response TEXT,
  FOREIGN KEY (session_id) REFERENCES sessions(session_id)
);

-- Vocab cards shown to the player, both the initial deal for a scenario and
-- every refresh. elapsed_ms is how long they had that scenario's cards in
-- front of them before refreshing (NULL for the initial deal).
CREATE TABLE IF NOT EXISTS card_events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  session_id TEXT NOT NULL,
  timestamp TEXT NOT NULL,
  timestamp_et TEXT NOT NULL,
  scenario TEXT,
  event_type TEXT NOT NULL,        -- 'initial' or 'refresh'
  cards TEXT,                      -- pipe-separated card names
  elapsed_ms INTEGER,
  FOREIGN KEY (session_id) REFERENCES sessions(session_id)
);

-- How long the player spends per visit to a tracked screen. A session can
-- have several rows for the same menu_name (e.g. bouncing between the main
-- menu and Rules) -- sum them for total time in that menu.
CREATE TABLE IF NOT EXISTS menu_durations (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  session_id TEXT NOT NULL,
  timestamp TEXT NOT NULL,         -- when the visit ended
  timestamp_et TEXT NOT NULL,
  menu_name TEXT NOT NULL,         -- 'main_menu', 'respond', or 'review'
  duration_ms INTEGER NOT NULL,
  scenario TEXT,                   -- populated for 'respond' visits
  FOREIGN KEY (session_id) REFERENCES sessions(session_id)
);
