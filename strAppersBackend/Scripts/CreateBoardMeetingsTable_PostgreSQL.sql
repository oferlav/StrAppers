-- Add BoardMeetings table for PostgreSQL
-- This table stores meeting records for project boards
-- Run this AFTER creating the ProjectBoards table
-- Note: PostgreSQL converts unquoted identifiers to lowercase

-- First, create the table if it doesn't exist
CREATE TABLE IF NOT EXISTS "BoardMeetings" (
    "Id" SERIAL PRIMARY KEY,                    -- Auto-increment primary key
    "BoardId" VARCHAR(50) NOT NULL,            -- Foreign key to ProjectBoards table
    "MeetingTime" TIMESTAMP NOT NULL,          -- Meeting date and time
    CONSTRAINT "FK_BoardMeetings_ProjectBoards" FOREIGN KEY ("BoardId") REFERENCES "ProjectBoards"("BoardId") ON DELETE CASCADE
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS "IX_BoardMeetings_BoardId" ON "BoardMeetings"("BoardId");
CREATE INDEX IF NOT EXISTS "IX_BoardMeetings_MeetingTime" ON "BoardMeetings"("MeetingTime");

-- Add comments for documentation
COMMENT ON TABLE "BoardMeetings" IS 'Stores meeting records for project boards';
COMMENT ON COLUMN "BoardMeetings"."Id" IS 'Primary key (auto-increment)';
COMMENT ON COLUMN "BoardMeetings"."BoardId" IS 'Foreign key to ProjectBoards table (BoardId)';
COMMENT ON COLUMN "BoardMeetings"."MeetingTime" IS 'Meeting date and time';

