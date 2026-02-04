-- Set Source (file, method, lines) for MentorPrompt rows where HasPlaceholders = true only.
-- Run after AddSourceColumn_MentorPrompt_PostgreSQL.sql and InsertMentorPrompt_GeneralMentoring_PostgreSQL.sql.
-- Matches rows by CategoryId (Current Mentor Context) + SortOrder + RoleId IS NULL.

-- User Prompt Template (SortOrder 1)
UPDATE "MentorPrompt"
SET "Source" = 'MentorController.cs, GetMentorContext, 487; GetMentorContextInternal, 5429'
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "RoleId" IS NULL
  AND "SortOrder" = 1
  AND "HasPlaceholders" = true;

-- Current date context template (SortOrder 4)
UPDATE "MentorPrompt"
SET "Source" = 'MentorController.cs, BuildEnhancedSystemPrompt, 1333'
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "RoleId" IS NULL
  AND "SortOrder" = 4
  AND "HasPlaceholders" = true;

-- Next Team Meeting - Past (SortOrder 6)
UPDATE "MentorPrompt"
SET "Source" = 'MentorController.cs, BuildEnhancedSystemPrompt, 1379'
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "RoleId" IS NULL
  AND "SortOrder" = 6
  AND "HasPlaceholders" = true;

-- Next Team Meeting - Future with URL (SortOrder 7)
UPDATE "MentorPrompt"
SET "Source" = 'MentorController.cs, BuildEnhancedSystemPrompt, 1388-1389'
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "RoleId" IS NULL
  AND "SortOrder" = 7
  AND "HasPlaceholders" = true;

-- Next Team Meeting - Future no URL (SortOrder 8)
UPDATE "MentorPrompt"
SET "Source" = 'MentorController.cs, BuildEnhancedSystemPrompt, 1395'
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "RoleId" IS NULL
  AND "SortOrder" = 8
  AND "HasPlaceholders" = true;
