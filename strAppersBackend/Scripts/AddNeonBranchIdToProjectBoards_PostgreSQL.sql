-- Add NeonBranchId column to ProjectBoards table
-- This field stores the Neon branch ID for each database (ensures isolation - each database on its own branch)
-- Run this script if you need to manually add the column instead of using EF Core migrations

-- Check if column already exists before adding
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'ProjectBoards' 
        AND column_name = 'NeonBranchId'
    ) THEN
        ALTER TABLE "ProjectBoards" 
        ADD COLUMN "NeonBranchId" VARCHAR(100) NULL;
        
        RAISE NOTICE 'Column NeonBranchId added successfully to ProjectBoards table';
    ELSE
        RAISE NOTICE 'Column NeonBranchId already exists in ProjectBoards table';
    END IF;
END $$;

-- Add comment for documentation
COMMENT ON COLUMN "ProjectBoards"."NeonBranchId" IS 'Neon branch ID for this database (ensures isolation - each database on its own branch with unique hostname)';
