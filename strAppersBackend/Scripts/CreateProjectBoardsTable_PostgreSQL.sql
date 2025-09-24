-- Add ProjectBoards table for PostgreSQL
-- This table stores Trello board information for each project
-- Run this AFTER creating the Projects table
-- Note: PostgreSQL converts unquoted identifiers to lowercase

-- First, create the table if it doesn't exist
CREATE TABLE IF NOT EXISTS "ProjectBoards" (
    "Id" VARCHAR(50) PRIMARY KEY,  -- Trello board ID (e.g., "68cfc87d3369798f4f80b32b")
    "ProjectId" INTEGER NOT NULL,  -- Foreign key to Projects table
    "StartDate" TIMESTAMP NULL,    -- Project start date
    "EndDate" TIMESTAMP NULL,      -- Project end date
    "DueDate" TIMESTAMP NULL,      -- Project due date
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,  -- Record creation timestamp
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,  -- Record update timestamp
    "StatusId" INTEGER DEFAULT 1,  -- Project status ID (defaults to 1 = New)
    "HasAdmin" BOOLEAN NOT NULL DEFAULT TRUE  -- Whether project has admin access
);

-- Add foreign key constraints using ALTER TABLE
DO $$ 
BEGIN
    -- Add foreign key from ProjectBoards to Projects
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'FK_ProjectBoards_Projects'
        AND table_name = 'ProjectBoards'
    ) THEN
        ALTER TABLE "ProjectBoards" 
        ADD CONSTRAINT "FK_ProjectBoards_Projects" 
        FOREIGN KEY ("ProjectId") REFERENCES "Projects"("Id") ON DELETE CASCADE;
    END IF;

    -- Add foreign key from ProjectBoards to ProjectStatuses
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'FK_ProjectBoards_ProjectStatuses'
        AND table_name = 'ProjectBoards'
    ) THEN
        ALTER TABLE "ProjectBoards" 
        ADD CONSTRAINT "FK_ProjectBoards_ProjectStatuses" 
        FOREIGN KEY ("StatusId") REFERENCES "ProjectStatuses"("Id") ON DELETE SET NULL;
    END IF;
END $$;

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS "IX_ProjectBoards_ProjectId" ON "ProjectBoards"("ProjectId");
CREATE INDEX IF NOT EXISTS "IX_ProjectBoards_CreatedAt" ON "ProjectBoards"("CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_ProjectBoards_StatusId" ON "ProjectBoards"("StatusId");

-- Add comments for documentation
COMMENT ON TABLE "ProjectBoards" IS 'Stores Trello board information and project metadata for each project';
COMMENT ON COLUMN "ProjectBoards"."Id" IS 'Trello board ID (primary key)';
COMMENT ON COLUMN "ProjectBoards"."ProjectId" IS 'Foreign key to Projects table';
COMMENT ON COLUMN "ProjectBoards"."StartDate" IS 'Project start date';
COMMENT ON COLUMN "ProjectBoards"."EndDate" IS 'Project end date';
COMMENT ON COLUMN "ProjectBoards"."DueDate" IS 'Project due date';
COMMENT ON COLUMN "ProjectBoards"."CreatedAt" IS 'Record creation timestamp';
COMMENT ON COLUMN "ProjectBoards"."UpdatedAt" IS 'Record update timestamp';
COMMENT ON COLUMN "ProjectBoards"."StatusId" IS 'Project status ID';
COMMENT ON COLUMN "ProjectBoards"."HasAdmin" IS 'Whether project has admin access';
