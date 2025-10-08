-- Add BoardId column to Students table for PostgreSQL
-- This script adds the BoardId foreign key to the existing Students table
-- Note: PostgreSQL converts unquoted identifiers to lowercase

-- Add BoardId column (VARCHAR(50) to match ProjectBoards.Id)
DO $$ 
BEGIN
    -- Add BoardId column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'BoardId'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "BoardId" VARCHAR(50) NULL;
        
        -- Add comment for documentation
        COMMENT ON COLUMN "Students"."BoardId" IS 'Foreign key to ProjectBoards table (Trello board ID)';
    END IF;
END $$;

-- Add foreign key constraint from Students.BoardId to ProjectBoards.Id
DO $$ 
BEGIN
    -- Add foreign key constraint if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'FK_Students_ProjectBoards'
        AND table_name = 'Students'
    ) THEN
        ALTER TABLE "Students" 
        ADD CONSTRAINT "FK_Students_ProjectBoards" 
        FOREIGN KEY ("BoardId") REFERENCES "ProjectBoards"("Id") ON DELETE SET NULL;
    END IF;
END $$;

-- Create index on BoardId for better performance
CREATE INDEX IF NOT EXISTS "IX_Students_BoardId" ON "Students"("BoardId");

-- Add comment for documentation
COMMENT ON COLUMN "Students"."BoardId" IS 'Foreign key to ProjectBoards table (Trello board ID)';





