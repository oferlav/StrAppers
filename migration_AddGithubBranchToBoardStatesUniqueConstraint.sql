-- =============================================
-- Migration: Add GithubBranch to BoardStates Unique Constraint
-- =============================================
-- This script updates the unique constraint on BoardStates to include GithubBranch
-- This allows multiple PR webhook records for different branches while preventing duplicates
-- =============================================
-- 
-- IMPORTANT NOTES:
-- 1. PostgreSQL treats NULL values as DISTINCT in unique constraints
--    - Multiple rows with NULL GithubBranch are allowed
--    - This is safe for webhook records where GithubBranch might be NULL for non-PR events
-- 2. The constraint will be: (BoardId, Source, Webhook, GithubBranch)
-- 3. This allows:
--    - Multiple PRs for different branches: (board1, "GitHub", true, "1-B") and (board1, "GitHub", true, "2-F")
--    - Multiple records with NULL: (board1, "GitHub", true, NULL) for different event types
-- =============================================

-- Step 1: Drop the existing unique constraint/index
DROP INDEX IF EXISTS "IX_BoardStates_BoardId_Source_Webhook";

-- Step 2: Create the new unique constraint/index including GithubBranch
CREATE UNIQUE INDEX IF NOT EXISTS "IX_BoardStates_BoardId_Source_Webhook_GithubBranch"
ON "BoardStates"("BoardId", "Source", "Webhook", "GithubBranch");

-- Step 3: Verify the constraint was created
SELECT 
    indexname,
    indexdef
FROM pg_indexes 
WHERE tablename = 'BoardStates' 
AND indexname = 'IX_BoardStates_BoardId_Source_Webhook_GithubBranch';

-- Step 4: Check for any potential duplicate violations before applying
-- This query will show any existing duplicates that would violate the new constraint
SELECT 
    "BoardId",
    "Source",
    "Webhook",
    "GithubBranch",
    COUNT(*) as "DuplicateCount"
FROM "BoardStates"
GROUP BY "BoardId", "Source", "Webhook", "GithubBranch"
HAVING COUNT(*) > 1
ORDER BY "DuplicateCount" DESC, "BoardId", "Source";

-- NOTE: If the above query returns any rows, you have existing duplicates that need to be resolved
-- before applying this constraint. You may need to:
-- 1. Delete duplicate records (keeping the most recent one)
-- 2. Or update one of the duplicates to have a different GithubBranch value
-- 3. Or handle them case-by-case based on your business logic
