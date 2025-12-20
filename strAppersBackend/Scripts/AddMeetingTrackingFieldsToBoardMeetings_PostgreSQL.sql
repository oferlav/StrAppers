-- Add meeting tracking fields to BoardMeetings table for PostgreSQL
-- This script adds fields for tracking individual student meeting access
-- Run this script to update existing BoardMeetings table
-- Note: PostgreSQL converts unquoted identifiers to lowercase, so we use quoted identifiers

-- Add StudentEmail column
ALTER TABLE "BoardMeetings" 
ADD COLUMN IF NOT EXISTS "StudentEmail" VARCHAR(255) NULL;

-- Add CustomMeetingUrl column (TEXT type for long URLs)
ALTER TABLE "BoardMeetings" 
ADD COLUMN IF NOT EXISTS "CustomMeetingUrl" TEXT NULL;

-- Add ActualMeetingUrl column (TEXT type for long URLs)
ALTER TABLE "BoardMeetings" 
ADD COLUMN IF NOT EXISTS "ActualMeetingUrl" TEXT NULL;

-- Add Attended column (boolean, default false)
ALTER TABLE "BoardMeetings" 
ADD COLUMN IF NOT EXISTS "Attended" BOOLEAN NOT NULL DEFAULT false;

-- Add JoinTime column (timestamp with time zone)
ALTER TABLE "BoardMeetings" 
ADD COLUMN IF NOT EXISTS "JoinTime" TIMESTAMP WITH TIME ZONE NULL;

-- Create indexes for better query performance
CREATE INDEX IF NOT EXISTS "IX_BoardMeetings_StudentEmail" ON "BoardMeetings"("StudentEmail");
CREATE INDEX IF NOT EXISTS "IX_BoardMeetings_Attended" ON "BoardMeetings"("Attended");

-- Add comments for documentation
COMMENT ON COLUMN "BoardMeetings"."StudentEmail" IS 'Student email address for this meeting invitation';
COMMENT ON COLUMN "BoardMeetings"."CustomMeetingUrl" IS 'Custom redirect URL for tracking individual student access';
COMMENT ON COLUMN "BoardMeetings"."ActualMeetingUrl" IS 'Actual Teams meeting URL (the real Microsoft Teams join link)';
COMMENT ON COLUMN "BoardMeetings"."Attended" IS 'Whether the student has attended the meeting (default: false)';
COMMENT ON COLUMN "BoardMeetings"."JoinTime" IS 'Timestamp when the student joined the meeting via their custom URL';




