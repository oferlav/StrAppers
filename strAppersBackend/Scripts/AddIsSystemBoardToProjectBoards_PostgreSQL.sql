-- Add IsSystemBoard column to ProjectBoards table for PostgreSQL
-- True when the record is the SystemBoard (full template board); false for EmptyBoard or single board.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'ProjectBoards'
          AND column_name = 'IsSystemBoard'
    ) THEN
        ALTER TABLE "ProjectBoards"
        ADD COLUMN "IsSystemBoard" BOOLEAN NOT NULL DEFAULT FALSE;

        COMMENT ON COLUMN "ProjectBoards"."IsSystemBoard" IS 'True when this record is the SystemBoard (full template board); false for EmptyBoard or single board.';
    END IF;
END $$;
