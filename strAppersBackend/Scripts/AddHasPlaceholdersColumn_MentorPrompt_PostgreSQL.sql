-- Add HasPlaceholders column to MentorPrompt.
-- When true, PromptString contains placeholders (e.g. {0}, {1}) that must be substituted at runtime (e.g. string.Format).
-- Run after AddPromptCategoriesAndMentorPromptTables_PostgreSQL.sql (or on existing MentorPrompt table).

ALTER TABLE "MentorPrompt"
ADD COLUMN IF NOT EXISTS "HasPlaceholders" BOOLEAN NOT NULL DEFAULT false;

COMMENT ON COLUMN "MentorPrompt"."HasPlaceholders" IS 'True when PromptString contains placeholders (e.g. {0}, {1}) to be substituted at assembly time';
