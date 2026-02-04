-- Add Source column to MentorPrompt.
-- Temporary reference to code/config source for refactoring (file, method, lines).
-- Populated only for rows where HasPlaceholders = true.
-- Run after AddHasPlaceholdersColumn_MentorPrompt_PostgreSQL.sql (or on existing MentorPrompt table).

ALTER TABLE "MentorPrompt"
ADD COLUMN IF NOT EXISTS "Source" VARCHAR(500) NULL;

COMMENT ON COLUMN "MentorPrompt"."Source" IS 'Code/config source for refactoring (file, method, lines). Filled only when HasPlaceholders = true.';
