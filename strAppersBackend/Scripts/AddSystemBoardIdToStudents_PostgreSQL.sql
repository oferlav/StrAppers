-- Add SystemBoardId column to Students table for PostgreSQL
-- This script adds the SystemBoardId foreign key to the existing Students table
-- Note: PostgreSQL converts unquoted identifiers to lowercase

-- Add SystemBoardId column (VARCHAR(50) to match ProjectBoards.Id)
DO $$ 
BEGIN
    -- Add SystemBoardId column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'SystemBoardId'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "SystemBoardId" VARCHAR(50) NULL;
        
        -- Add comment for documentation
        COMMENT ON COLUMN "Students"."SystemBoardId" IS 'Foreign key to ProjectBoards table (System board ID)';
    END IF;
END $$;

-- Add foreign key constraint from Students.SystemBoardId to ProjectBoards.BoardId
DO $$ 
BEGIN
    -- Add foreign key constraint if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'FK_Students_ProjectBoards_SystemBoardId'
        AND table_name = 'Students'
    ) THEN
        ALTER TABLE "Students" 
        ADD CONSTRAINT "FK_Students_ProjectBoards_SystemBoardId" 
        FOREIGN KEY ("SystemBoardId") REFERENCES "ProjectBoards"("BoardId") ON DELETE SET NULL;
    END IF;
END $$;

-- Create index on SystemBoardId for better performance
CREATE INDEX IF NOT EXISTS "IX_Students_SystemBoardId" ON "Students"("SystemBoardId");

-- Add comment for documentation
COMMENT ON COLUMN "Students"."SystemBoardId" IS 'Foreign key to ProjectBoards table (System board ID)';
