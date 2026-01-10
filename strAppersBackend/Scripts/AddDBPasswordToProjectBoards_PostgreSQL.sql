-- Add DBPassword column to ProjectBoards table
-- This field stores the isolated database role password for manual connections (e.g., pgAdmin)
-- Run this script if you need to manually add the column instead of using EF Core migrations

-- Check if column already exists before adding
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'ProjectBoards' 
        AND column_name = 'DBPassword'
    ) THEN
        ALTER TABLE "ProjectBoards" 
        ADD COLUMN "DBPassword" VARCHAR(200) NULL;
        
        RAISE NOTICE 'Column DBPassword added successfully to ProjectBoards table';
    ELSE
        RAISE NOTICE 'Column DBPassword already exists in ProjectBoards table';
    END IF;
END $$;

-- Add comment for documentation
COMMENT ON COLUMN "ProjectBoards"."DBPassword" IS 'Database password for the isolated database role (for manual connections like pgAdmin)';


