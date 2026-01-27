-- =============================================
-- Migration: Fix GithubPages GithubBranch NULL to Empty String
-- =============================================
-- This script fixes the duplication issue in GithubPages records
-- by converting NULL GithubBranch values to empty string ''
-- =============================================
-- 
-- ISSUE:
-- PostgreSQL treats NULL values as DISTINCT in unique constraints
-- Multiple rows with NULL GithubBranch were allowed, causing duplicates
-- 
-- SOLUTION:
-- Update all GithubPages records with NULL GithubBranch to empty string ''
-- This matches the approach used for Railway records
-- =============================================

-- Step 1: Check for existing duplicates (should show the problem)
SELECT 
    "BoardId",
    "Source",
    "Webhook",
    "GithubBranch",
    COUNT(*) as "DuplicateCount",
    MIN("CreatedAt") as "FirstCreated",
    MAX("CreatedAt") as "LastCreated"
FROM "BoardStates"
WHERE "Source" = 'GithubPages'
GROUP BY "BoardId", "Source", "Webhook", "GithubBranch"
HAVING COUNT(*) > 1
ORDER BY "DuplicateCount" DESC, "BoardId";

-- Step 2: Update NULL GithubBranch to empty string for GithubPages records
UPDATE "BoardStates"
SET "GithubBranch" = ''
WHERE "Source" = 'GithubPages' 
  AND ("GithubBranch" IS NULL OR "GithubBranch" = '');

-- Step 3: For duplicate records, keep only the most recent one per BoardId
-- Delete older duplicates, keeping the one with the latest UpdatedAt
WITH RankedRecords AS (
    SELECT 
        "Id",
        ROW_NUMBER() OVER (
            PARTITION BY "BoardId", "Source", "Webhook", "GithubBranch"
            ORDER BY "UpdatedAt" DESC, "CreatedAt" DESC
        ) as rn
    FROM "BoardStates"
    WHERE "Source" = 'GithubPages'
)
DELETE FROM "BoardStates"
WHERE "Id" IN (
    SELECT "Id" 
    FROM RankedRecords 
    WHERE rn > 1
);

-- Step 4: Verify no duplicates remain
SELECT 
    "BoardId",
    "Source",
    "Webhook",
    "GithubBranch",
    COUNT(*) as "Count"
FROM "BoardStates"
WHERE "Source" = 'GithubPages'
GROUP BY "BoardId", "Source", "Webhook", "GithubBranch"
HAVING COUNT(*) > 1;

-- Expected result: 0 rows (no duplicates)

-- Step 5: Verify all GithubPages records now have empty string (not NULL)
SELECT 
    COUNT(*) as "TotalGithubPagesRecords",
    COUNT("GithubBranch") as "NonNullGithubBranch",
    COUNT(*) FILTER (WHERE "GithubBranch" = '') as "EmptyStringGithubBranch",
    COUNT(*) FILTER (WHERE "GithubBranch" IS NULL) as "NullGithubBranch"
FROM "BoardStates"
WHERE "Source" = 'GithubPages';

-- Expected: NullGithubBranch = 0, EmptyStringGithubBranch = TotalGithubPagesRecords
