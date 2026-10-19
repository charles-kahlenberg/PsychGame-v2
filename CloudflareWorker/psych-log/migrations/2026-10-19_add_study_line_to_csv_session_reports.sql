-- One-time migration for the already-deployed database: schema.sql's
-- CREATE TABLE IF NOT EXISTS won't add a column to a table that already
-- exists, so this has to run separately. Run it BEFORE deploying the worker
-- that writes Study Data.csv, or the sync's INSERT will fail.
-- Run once (--file fails with an OAuth login: "Authentication error [code: 10000]"):
--   wrangler d1 execute psych_log_db --remote --command "ALTER TABLE csv_session_reports ADD COLUMN study_line TEXT;"
-- Applied to the live database on 2026-10-19.

ALTER TABLE csv_session_reports ADD COLUMN study_line TEXT;
