-- Manual SQL script to add MentorFeedback column to BoardStates table
-- This script is idempotent - it can be run multiple times safely

-- Check if the column already exists before adding it
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'BoardStates' 
        AND column_name = 'MentorFeedback'
    ) THEN
        ALTER TABLE "BoardStates" 
        ADD COLUMN "MentorFeedback" text NULL;
        
        RAISE NOTICE 'Column MentorFeedback added successfully to BoardStates table';
    ELSE
        RAISE NOTICE 'Column MentorFeedback already exists in BoardStates table';
    END IF;
END $$;
