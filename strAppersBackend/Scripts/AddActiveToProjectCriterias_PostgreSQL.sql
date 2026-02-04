-- Add Active column to ProjectCriterias table for PostgreSQL.
-- When false, criteria are excluded from GET /api/Projects/use/ProjectCriteria and from classification.
-- New rows default to false; existing rows are set to true so they remain visible.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'ProjectCriterias'
          AND column_name = 'Active'
    ) THEN
        ALTER TABLE "ProjectCriterias"
        ADD COLUMN "Active" BOOLEAN NOT NULL DEFAULT FALSE;

        COMMENT ON COLUMN "ProjectCriterias"."Active" IS 'When false, criteria are excluded from GET ProjectCriteria and from classification.';

        -- Existing criteria remain visible until explicitly deactivated
        UPDATE "ProjectCriterias" SET "Active" = true;
    END IF;
END $$;
