-- Add CustomerPastStory column to Projects table for PostgreSQL
-- This script adds the CustomerPastStory field (TEXT) for the AI Customer backstory/past story.
-- The content is included in the Customer chatbot context (linked by ProjectId).

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Projects'
          AND column_name = 'CustomerPastStory'
    ) THEN
        ALTER TABLE "Projects"
        ADD COLUMN "CustomerPastStory" TEXT NULL;

        COMMENT ON COLUMN "Projects"."CustomerPastStory" IS 'Past story/backstory for the AI Customer. Injected into Customer chatbot context by ProjectId.';
    END IF;
END $$;
