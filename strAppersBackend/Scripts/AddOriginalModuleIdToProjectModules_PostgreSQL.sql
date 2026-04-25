-- Add OriginalModuleId to ProjectModules (PostgreSQL).
-- Matches EF migration 20260426180000_AddOriginalModuleIdToProjectModules.
-- When a project is duplicated, new module rows store the source ProjectModules.Id here
-- so TrelloBoardJson can be remapped old id -> new id without relying on sort order.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'ProjectModules'
          AND column_name = 'OriginalModuleId'
    ) THEN
        ALTER TABLE "ProjectModules"
        ADD COLUMN "OriginalModuleId" INTEGER NULL;
    END IF;
END
$$;

-- Optional: index for duplicate remap and analytics (safe to run if not exists)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_indexes
        WHERE schemaname = 'public'
          AND indexname = 'IX_ProjectModules_ProjectId_OriginalModuleId'
    ) THEN
        CREATE INDEX "IX_ProjectModules_ProjectId_OriginalModuleId"
        ON "ProjectModules" ("ProjectId", "OriginalModuleId");
    END IF;
END
$$;

COMMENT ON COLUMN "ProjectModules"."OriginalModuleId" IS 'When set, the ProjectModules.Id this row was copied from (e.g. project duplicate). NULL for normal created modules.';
