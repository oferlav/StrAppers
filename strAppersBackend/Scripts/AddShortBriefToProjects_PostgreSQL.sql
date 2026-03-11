-- Add ShortBrief column to Projects table for PostgreSQL
-- Type: character varying(1000). Optional short summary/brief for the project.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Projects'
          AND column_name = 'ShortBrief'
    ) THEN
        ALTER TABLE "Projects"
        ADD COLUMN "ShortBrief" character varying(1000) NULL;

        COMMENT ON COLUMN "Projects"."ShortBrief" IS 'Short brief/summary of the project (max 1000 characters).';
    END IF;
END $$;
