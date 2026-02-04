-- Add category "User Message Templates" for fragments that build the USER message (not the system prompt).
-- Run after InsertPromptCategories_GeneralMentoring_PostgreSQL.sql.
-- Idempotent: only inserts when a category with the same Name does not exist.

INSERT INTO "PromptCategories" ("Name", "Description", "SortOrder")
SELECT v."Name", v."Description", v."SortOrder"
FROM (VALUES
  (
    'User Message Templates',
    'Templates for building the user message sent to the AI (e.g. current context, task details). Excluded from system prompt assembly.',
    50
  )
) AS v("Name", "Description", "SortOrder")
WHERE NOT EXISTS (SELECT 1 FROM "PromptCategories" c WHERE c."Name" = v."Name");
