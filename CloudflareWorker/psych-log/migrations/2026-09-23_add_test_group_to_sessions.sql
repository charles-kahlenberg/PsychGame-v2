-- One-time migration for the already-deployed database: schema.sql's
-- CREATE TABLE IF NOT EXISTS won't add a column to a table that already
-- exists, so this has to run separately.
-- Run once (--file fails with an OAuth login: "Authentication error [code: 10000]"):
--   wrangler d1 execute psych_log_db --remote --command "ALTER TABLE sessions ADD COLUMN test_group INTEGER; ALTER TABLE sessions ADD COLUMN features TEXT;"
-- Applied to the live database on 2026-09-23.
-- Sessions logged before this (the original build) are left NULL: they were all group 1.

ALTER TABLE sessions ADD COLUMN test_group INTEGER;
ALTER TABLE sessions ADD COLUMN features TEXT;
