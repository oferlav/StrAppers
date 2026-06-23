-- Migration: AddTestResultsToBoardStates
-- Branch: feat/student-test-runner
-- Date: 2026-06-23
-- Run manually on prod DB (Neon PostgreSQL)

ALTER TABLE "BoardStates"
  ADD COLUMN IF NOT EXISTS "LastTestStatus"   character varying(50),
  ADD COLUMN IF NOT EXISTS "LastTestOutput"   text,
  ADD COLUMN IF NOT EXISTS "LastTestRunDate"  timestamp with time zone;
