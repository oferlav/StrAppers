-- Add TrelloBoardJson column to Projects table for PostgreSQL
-- This script adds the TrelloBoardJson field (TEXT) to store cached TrelloProjectCreationRequest JSON.
-- When Trello:UseDBProjectBoard is true and this column has data, board creation uses it instead of calling AI.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Projects'
          AND column_name = 'TrelloBoardJson'
    ) THEN
        ALTER TABLE "Projects"
        ADD COLUMN "TrelloBoardJson" TEXT NULL;

        COMMENT ON COLUMN "Projects"."TrelloBoardJson" IS 'Cached JSON for Trello board creation (TrelloProjectCreationRequest). When UseDBProjectBoard is true and this has data, create board uses it instead of calling AI.';
    END IF;
END $$;
