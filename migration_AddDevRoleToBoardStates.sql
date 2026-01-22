-- =============================================
-- Migration: Add DevRole Column to BoardStates
-- =============================================
-- This script adds the DevRole column to the BoardStates table
-- and backfills existing records based on Source and GithubBranch
-- =============================================

-- Step 1: Add DevRole column
ALTER TABLE "BoardStates"
ADD COLUMN IF NOT EXISTS "DevRole" VARCHAR(50) NULL;

-- Step 2: Backfill existing records
-- Rule 1: Source="Railway" -> DevRole="Backend"
UPDATE "BoardStates"
SET "DevRole" = 'Backend'
WHERE "Source" = 'Railway' AND "DevRole" IS NULL;

-- Rule 2: Source="GithubPages" -> DevRole="Frontend"
UPDATE "BoardStates"
SET "DevRole" = 'Frontend'
WHERE "Source" = 'GithubPages' AND "DevRole" IS NULL;

-- Rule 3: For other records with GithubBranch populated:
--   - If GithubBranch contains 'B' (case-insensitive) -> DevRole="Backend"
--   - Otherwise -> DevRole="Frontend"
UPDATE "BoardStates"
SET "DevRole" = CASE 
    WHEN "GithubBranch" IS NOT NULL AND UPPER("GithubBranch") LIKE '%B%' THEN 'Backend'
    WHEN "GithubBranch" IS NOT NULL THEN 'Frontend'
    ELSE NULL
END
WHERE "DevRole" IS NULL AND "GithubBranch" IS NOT NULL;

-- Step 3: Verify the migration
-- Check counts by DevRole
SELECT 
    "DevRole",
    COUNT(*) as "Count"
FROM "BoardStates"
GROUP BY "DevRole"
ORDER BY "DevRole";

-- Check records without DevRole (should be minimal - only records without GithubBranch and not Railway/GithubPages)
SELECT 
    "Source",
    COUNT(*) as "Count"
FROM "BoardStates"
WHERE "DevRole" IS NULL
GROUP BY "Source"
ORDER BY "Source";
