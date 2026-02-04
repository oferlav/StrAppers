-- Fix semantic duplications in MentorPrompt (General Mentoring).
-- 1. G2 (Base Mentor Persona): Remove the "FOR NON-Developer roles..." bullet (duplicate of NonDeveloperCapabilitiesInfo).
-- 2. NonDeveloperCapabilitiesInfo (Current Mentor Context, SortOrder 10): Remove the code-formatting sentence (duplicate of G2).
-- Run after InsertMentorPrompt_GeneralMentoring_PostgreSQL.sql and InsertMentorPrompt_MissingGeneralMentoring_PostgreSQL.sql.

BEGIN;

-- 1. G2: Remove "- ⚠️ FOR NON-Developer roles (UI/UX Designer, Product Manager, etc.): Do NOT assume they have GitHub accounts or repositories. Provide general guidance appropriate to their role."
UPDATE "MentorPrompt"
SET "PromptString" = REPLACE(
  "PromptString",
  E'- ⚠️ FOR NON-Developer roles (UI/UX Designer, Product Manager, etc.): Do NOT assume they have GitHub accounts or repositories. Provide general guidance appropriate to their role.\n\n',
  E'\n\n'
)
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Base Mentor Persona and General Instructions' LIMIT 1)
  AND "RoleId" IS NULL
  AND "SortOrder" = 1
  AND "HasPlaceholders" = false;

-- 2. NonDeveloperCapabilitiesInfo: Remove the trailing "⚠️ CODE FORMATTING: ALWAYS wrap..." sentence
-- (Stored string has one newline before ⚠️, not two; use \n not \n\n to match.)
UPDATE "MentorPrompt"
SET "PromptString" = REPLACE(
  "PromptString",
  E'\n⚠️ CODE FORMATTING: ALWAYS wrap ALL code snippets, commands, and terminal commands in triple backticks (```bash\ncommand\n```) for proper frontend display.',
  ''
)
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "RoleId" IS NULL
  AND "SortOrder" = 10
  AND "HasPlaceholders" = true;

COMMIT;
