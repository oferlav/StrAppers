-- Add NextMeetingTitle column to ProjectBoards table
-- Stores the title of the next scheduled Teams meeting (set when creating a meeting via create-meeting-smtp-for-board)
-- Run this script if you need to manually add the column instead of using EF Core migrations

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'ProjectBoards'
          AND column_name = 'NextMeetingTitle'
    ) THEN
        ALTER TABLE "ProjectBoards"
        ADD COLUMN "NextMeetingTitle" VARCHAR(500) NULL;

        RAISE NOTICE 'Column NextMeetingTitle added successfully to ProjectBoards table';
    ELSE
        RAISE NOTICE 'Column NextMeetingTitle already exists in ProjectBoards table';
    END IF;
END $$;

COMMENT ON COLUMN "ProjectBoards"."NextMeetingTitle" IS 'Title of the next scheduled Teams meeting for the board';
