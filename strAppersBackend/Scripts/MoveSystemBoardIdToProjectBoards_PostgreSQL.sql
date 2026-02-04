-- Move SystemBoardId from Students table to ProjectBoards table
-- This script adds the SystemBoardId column to ProjectBoards table
-- Note: The user will manually delete the SystemBoardId column from Students table
-- Note: PostgreSQL converts unquoted identifiers to lowercase

-- Add SystemBoardId column to ProjectBoards table (VARCHAR(50) to match ProjectBoards.Id)
DO $$ 
BEGIN
    -- Add SystemBoardId column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'ProjectBoards' 
        AND column_name = 'SystemBoardId'
    ) THEN
        ALTER TABLE "ProjectBoards" 
        ADD COLUMN "SystemBoardId" VARCHAR(50) NULL;
        
        -- Add comment for documentation
        COMMENT ON COLUMN "ProjectBoards"."SystemBoardId" IS 'Foreign key to ProjectBoards table (System board ID - self-referencing)';
    END IF;
END $$;

-- Add foreign key constraint from ProjectBoards.SystemBoardId to ProjectBoards.BoardId (self-referencing)
DO $$ 
BEGIN
    -- Add foreign key constraint if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'FK_ProjectBoards_ProjectBoards_SystemBoardId'
        AND table_name = 'ProjectBoards'
    ) THEN
        ALTER TABLE "ProjectBoards" 
        ADD CONSTRAINT "FK_ProjectBoards_ProjectBoards_SystemBoardId" 
        FOREIGN KEY ("SystemBoardId") REFERENCES "ProjectBoards"("BoardId") ON DELETE SET NULL;
    END IF;
END $$;

-- Create index on SystemBoardId for better performance
CREATE INDEX IF NOT EXISTS "IX_ProjectBoards_SystemBoardId" ON "ProjectBoards"("SystemBoardId");

-- Add comment for documentation
COMMENT ON COLUMN "ProjectBoards"."SystemBoardId" IS 'Foreign key to ProjectBoards table (System board ID - self-referencing)';

-- NOTE: The SystemBoardId column should be manually removed from the Students table
-- using the following command (uncomment when ready):
-- ALTER TABLE "Students" DROP COLUMN IF EXISTS "SystemBoardId";
-- DROP INDEX IF EXISTS "IX_Students_SystemBoardId";
-- ALTER TABLE "Students" DROP CONSTRAINT IF EXISTS "FK_Students_ProjectBoards_SystemBoardId";
