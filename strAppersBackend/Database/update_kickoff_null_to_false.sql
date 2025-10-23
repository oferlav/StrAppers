-- Update existing Projects with NULL Kickoff values to false
-- This script should be run once to fix existing data

UPDATE "Projects"
SET "Kickoff" = false
WHERE "Kickoff" IS NULL;

-- Verify the update
SELECT "Id", "Title", "Kickoff"
FROM "Projects"
ORDER BY "Id";




