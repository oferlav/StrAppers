-- Add InUse column to Projects table for PostgreSQL
-- Matches EF migration 20260425130000_AddInUseToProjects.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Projects'
          AND column_name = 'InUse'
    ) THEN
        ALTER TABLE "Projects"
        ADD COLUMN "InUse" BOOLEAN NOT NULL DEFAULT TRUE;
    END IF;
END
$$;

COMMENT ON COLUMN "Projects"."InUse" IS 'Whether the project should be used in institute project-design workflows (default: TRUE).';
