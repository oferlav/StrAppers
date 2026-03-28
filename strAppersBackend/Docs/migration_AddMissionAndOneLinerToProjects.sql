-- Add Mission and OneLiner to Projects (PostgreSQL), varchar(250).
-- Matches EF migration: 20260325160000_AddMissionAndOneLinerToProjects (updated to 250).

ALTER TABLE "Projects"
    ADD COLUMN IF NOT EXISTS "Mission" character varying(250) NULL;

ALTER TABLE "Projects"
    ADD COLUMN IF NOT EXISTS "OneLiner" character varying(250) NULL;

-- Rollback (run manually if needed):
-- ALTER TABLE "Projects" DROP COLUMN IF EXISTS "Mission";
-- ALTER TABLE "Projects" DROP COLUMN IF EXISTS "OneLiner";
