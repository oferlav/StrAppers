-- =============================================
-- Migration: Fix Railway Records GithubBranch NULL to Empty String
-- =============================================
-- This script updates existing Railway records that have NULL GithubBranch
-- to use empty string '' instead. This ensures ON CONFLICT works correctly
-- since PostgreSQL's ON CONFLICT cannot match NULL values (NULL != NULL).
-- =============================================
-- 
-- IMPORTANT NOTES:
-- 1. Railway records should be unique per board (one record per board)
-- 2. Using empty string '' instead of NULL allows ON CONFLICT to work correctly
-- 3. This only affects Railway records (Source='Railway', Webhook=true)
-- =============================================

-- Step 1: Update existing Railway records with NULL GithubBranch to empty string
UPDATE "BoardStates"
SET "GithubBranch" = ''
WHERE "Source" = 'Railway' 
  AND "Webhook" = true 
  AND ("GithubBranch" IS NULL OR "GithubBranch" = '');

-- Step 2: Verify the update
SELECT 
    "Source",
    "Webhook",
    COUNT(*) FILTER (WHERE "GithubBranch" IS NULL) as "NullCount",
    COUNT(*) FILTER (WHERE "GithubBranch" = '') as "EmptyStringCount",
    COUNT(*) FILTER (WHERE "GithubBranch" IS NOT NULL AND "GithubBranch" != '') as "OtherCount",
    COUNT(*) as "TotalCount"
FROM "BoardStates"
WHERE "Source" = 'Railway' AND "Webhook" = true
GROUP BY "Source", "Webhook";

-- Step 3: Check for any duplicate Railway records (should be 0 after fix)
SELECT 
    "BoardId",
    "Source",
    "Webhook",
    "GithubBranch",
    COUNT(*) as "DuplicateCount"
FROM "BoardStates"
WHERE "Source" = 'Railway' AND "Webhook" = true
GROUP BY "BoardId", "Source", "Webhook", "GithubBranch"
HAVING COUNT(*) > 1
ORDER BY "DuplicateCount" DESC, "BoardId";

-- NOTE: If Step 3 returns any rows, you have duplicate Railway records that need to be resolved.
-- You can delete duplicates keeping only the most recent one:
-- 
-- WITH ranked_records AS (
--     SELECT "Id",
--            ROW_NUMBER() OVER (PARTITION BY "BoardId", "Source", "Webhook", "GithubBranch" 
--                               ORDER BY "UpdatedAt" DESC) as rn
--     FROM "BoardStates"
--     WHERE "Source" = 'Railway' AND "Webhook" = true
-- )
-- DELETE FROM "BoardStates"
-- WHERE "Id" IN (
--     SELECT "Id" FROM ranked_records WHERE rn > 1
-- );
