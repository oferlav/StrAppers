-- Move the "User Prompt Template" fragment from "Current Mentor Context" to "User Message Templates".
-- That fragment (SortOrder 1, HasPlaceholders true) is for the USER message, not the system prompt.
-- Run after AddUserMessageTemplatesCategory_PostgreSQL.sql.

UPDATE "MentorPrompt"
SET "CategoryId" = (
  SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'User Message Templates' LIMIT 1
)
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "SortOrder" = 1
  AND "RoleId" IS NULL;
