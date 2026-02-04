-- Merge MentorPrompt rows that share the same RoleId, CategoryId, and HasPlaceholders = false.
-- For each such group with 2+ rows, concatenates PromptStrings in SortOrder with a formatted separator (double newline + "---" + double newline), then replaces the group with a single row.
-- Rows with HasPlaceholders = true are left unchanged.
-- Run after InsertMentorPrompt_GeneralMentoring_PostgreSQL.sql (or whenever you have multiple no-placeholder fragments per category that you want merged).
-- Idempotent: safe to run again after merge (groups will have 1 row each).

BEGIN;

-- 1. Build merged content for groups that have 2+ rows with HasPlaceholders = false
CREATE TEMP TABLE _merge_candidates ON COMMIT DROP AS
SELECT
  "RoleId",
  "CategoryId",
  string_agg("PromptString", E'\n\n---\n\n' ORDER BY "SortOrder") AS "PromptString",
  min("SortOrder") AS "SortOrder"
FROM "MentorPrompt"
WHERE "HasPlaceholders" = false
GROUP BY "RoleId", "CategoryId"
HAVING count(*) >= 2;

-- 2. Delete the original rows that will be replaced by the merged row
DELETE FROM "MentorPrompt" p
USING _merge_candidates m
WHERE p."RoleId" IS NOT DISTINCT FROM m."RoleId"
  AND p."CategoryId" = m."CategoryId"
  AND p."HasPlaceholders" = false;

-- 3. Insert one row per group with merged PromptString
INSERT INTO "MentorPrompt" ("RoleId", "CategoryId", "PromptString", "SortOrder", "HasPlaceholders", "IsActive", "UpdatedAt")
SELECT "RoleId", "CategoryId", "PromptString", "SortOrder", false, true, NULL
FROM _merge_candidates;

COMMIT;
