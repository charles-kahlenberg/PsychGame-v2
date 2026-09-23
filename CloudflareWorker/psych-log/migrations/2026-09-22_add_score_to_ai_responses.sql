-- One-time migration for the already-deployed database: schema.sql's
-- CREATE TABLE IF NOT EXISTS won't add a column to a table that already
-- exists, so this has to run separately.
-- Run once: wrangler d1 execute psych_log_db --remote --file=./migrations/2026-09-22_add_score_to_ai_responses.sql

ALTER TABLE ai_responses ADD COLUMN score INTEGER;
