-- Add NeonProjectId column to ProjectBoards table
-- This field stores the Neon project ID for each database (project-per-tenant isolation - each database in its own Neon project)
-- Run this script if you need to manually add the column instead of using EF Core migrations
--
-- IMPORTANT: This implements project-per-tenant model where each board gets its own isolated Neon project.
-- This ensures complete isolation - users will only see their own database when connecting via tools like pgAdmin.

-- Check if column already exists before adding
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'ProjectBoards' 
        AND column_name = 'NeonProjectId'
    ) THEN
        ALTER TABLE "ProjectBoards" 
        ADD COLUMN "NeonProjectId" VARCHAR(100) NULL;
        
        RAISE NOTICE 'Column NeonProjectId added successfully to ProjectBoards table';
    ELSE
        RAISE NOTICE 'Column NeonProjectId already exists in ProjectBoards table';
    END IF;
END $$;

-- Add comment for documentation
COMMENT ON COLUMN "ProjectBoards"."NeonProjectId" IS 'Neon project ID for this database (project-per-tenant isolation - each database in its own Neon project for complete isolation)';

-- Verify the column was added
SELECT column_name, data_type, character_maximum_length, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'ProjectBoards' 
AND column_name = 'NeonProjectId';
