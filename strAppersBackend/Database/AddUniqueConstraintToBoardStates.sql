-- =============================================
-- Add Unique Constraint to BoardStates Table
-- =============================================
-- This script adds a unique constraint on (BoardId, Source, Webhook) to prevent duplicate records
-- This allows multiple records per BoardId+Source combination if Webhook differs
-- (e.g., one record with Webhook=true from Railway webhook, one with Webhook=false from runtime errors)
-- Run this script if you have existing duplicate records that need to be cleaned up first
-- =============================================

-- Step 1: Remove any duplicate records (keep the most recent one)
-- WARNING: This will delete duplicate records. Review the results before proceeding.
-- Uncomment the following block if you need to clean up existing duplicates:

/*
-- Cleanup script for old constraint (BoardId, Source) - update if needed for new constraint
WITH RankedRecords AS (
    SELECT 
        "Id",
        ROW_NUMBER() OVER (
            PARTITION BY "BoardId", "Source", "Webhook" 
            ORDER BY "UpdatedAt" DESC, "CreatedAt" DESC
        ) AS rn
    FROM "BoardStates"
)
DELETE FROM "BoardStates"
WHERE "Id" IN (
    SELECT "Id" FROM RankedRecords WHERE rn > 1
);
*/

-- Step 2: Drop the existing non-unique index if it exists
DROP INDEX IF EXISTS "IX_BoardStates_BoardId_Source";

-- Step 3: Create the unique index/constraint on (BoardId, Source, Webhook)
-- This allows multiple records per BoardId+Source combination if Webhook differs
CREATE UNIQUE INDEX IF NOT EXISTS "IX_BoardStates_BoardId_Source_Webhook" 
ON "BoardStates"("BoardId", "Source", "Webhook");

-- Verification
SELECT 
    indexname, 
    indexdef 
FROM pg_indexes 
WHERE tablename = 'BoardStates' 
AND indexname = 'IX_BoardStates_BoardId_Source_Webhook';
