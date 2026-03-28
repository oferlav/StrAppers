-- Widen Mission and OneLiner from varchar(100) to varchar(250) (PostgreSQL).
-- Use when columns already exist as (100) from an earlier deploy.
-- Matches EF migration: 20260326120000_AlterProjectMissionOneLinerToVarchar250

ALTER TABLE "Projects"
    ALTER COLUMN "Mission" TYPE character varying(250);

ALTER TABLE "Projects"
    ALTER COLUMN "OneLiner" TYPE character varying(250);

-- Rollback (may fail if any value exceeds 100 chars):
-- ALTER TABLE "Projects" ALTER COLUMN "Mission" TYPE character varying(100);
-- ALTER TABLE "Projects" ALTER COLUMN "OneLiner" TYPE character varying(100);
