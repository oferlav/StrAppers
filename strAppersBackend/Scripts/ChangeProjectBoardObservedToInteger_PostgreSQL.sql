-- Script to change ProjectBoards.Observed from boolean to integer
-- This script drops the existing column and recreates it as integer type

-- Step 1: Drop the existing Observed column
ALTER TABLE "ProjectBoards" 
DROP COLUMN IF EXISTS "Observed";

-- Step 2: Add the Observed column as INTEGER with default value 0
ALTER TABLE "ProjectBoards" 
ADD COLUMN "Observed" INTEGER NOT NULL DEFAULT 0;

-- Step 3: Add comment to document the column
COMMENT ON COLUMN "ProjectBoards"."Observed" IS 'Number of times the project board has been observed (count)';

-- Verification query (optional - uncomment to check results)
-- SELECT "BoardId", "Observed" FROM "ProjectBoards" LIMIT 10;




