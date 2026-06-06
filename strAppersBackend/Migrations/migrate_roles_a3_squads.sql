-- =============================================================================
-- A3 Migration Part 2: Copy InstituteSquadRoles into Roles
--
-- Run AFTER migrate_roles_a3.sql has been applied and verified.
-- That script added the InstituteId/SquadId/IsTechnical/Competencies columns
-- and copied InstituteRoles (base institute roles) into Roles.
--
-- This script copies the squad-scoped role rows (InstituteSquadRoles) into Roles,
-- giving each a SquadId and resolving InstituteId via the parent squad.
--
-- Database: PostgreSQL
-- Safe to run: wrapped in a transaction.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- PREVIEW (run separately first — no changes)
-- -----------------------------------------------------------------------------

SELECT
    sq."InstituteId",
    COUNT(*)                             AS squad_roles_to_copy,
    COUNT(DISTINCT sr."SquadId")         AS squads_affected
FROM "InstituteSquadRoles" sr
JOIN "InstituteSquads" sq ON sq."Id" = sr."SquadId"
GROUP BY sq."InstituteId"
ORDER BY sq."InstituteId";


-- -----------------------------------------------------------------------------
-- MIGRATION
-- -----------------------------------------------------------------------------

BEGIN;

-- Prerequisite: the Roles table must already have InstituteId / SquadId columns
-- (migrate_roles_a3.sql must have been run first).
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Roles' AND column_name = 'SquadId'
    ) THEN
        RAISE EXCEPTION 'Prerequisite failed: Roles.SquadId column not found. Run migrate_roles_a3.sql first.';
    END IF;
END $$;

-- ── Copy InstituteSquadRoles → Roles ─────────────────────────────────────────
--
-- InstituteSquadRole has no InstituteId column directly — resolve it via
-- the parent InstituteSquad row.
--
-- Each row becomes a Roles row scoped to its specific squad:
--   InstituteId = the squad's InstituteId
--   SquadId     = the squad's Id
--
-- New auto-increment Ids are assigned.
-- Original InstituteSquadRoles rows are untouched until Phase 9 drops the table.

INSERT INTO "Roles" (
    "Name",
    "Description",
    "Competencies",
    "Category",
    "Type",
    "SkillId",
    "CustomerEngagement",
    "IsTechnical",
    "IsActive",
    "InstituteId",
    "SquadId",
    "CreatedAt",
    "UpdatedAt"
)
SELECT
    sr."Name",
    sr."Description",
    sr."Competencies",
    COALESCE(sr."Category", ''),
    sr."Type",
    sr."SkillId",
    sr."CustomerEngagement",
    sr."IsTechnical",
    sr."IsActive",
    sq."InstituteId",    -- resolved from parent squad
    sr."SquadId",
    sr."CreatedAt",
    sr."UpdatedAt"
FROM "InstituteSquadRoles" sr
JOIN "InstituteSquads" sq ON sq."Id" = sr."SquadId";

-- ── Verify before committing ──────────────────────────────────────────────────

DO $$
DECLARE
    source_count  integer;
    copied_count  integer;
BEGIN
    SELECT COUNT(*) INTO source_count FROM "InstituteSquadRoles";

    SELECT COUNT(*) INTO copied_count
    FROM "Roles"
    WHERE "SquadId" IS NOT NULL;

    IF copied_count < source_count THEN
        RAISE EXCEPTION
            'Row count mismatch: InstituteSquadRoles has % rows but Roles.SquadId rows = %. Rolling back.',
            source_count, copied_count;
    END IF;

    RAISE NOTICE 'Migration verified: % InstituteSquadRole rows copied into Roles (SquadId IS NOT NULL).',
        copied_count;
END $$;

COMMIT;


-- -----------------------------------------------------------------------------
-- POST-MIGRATION CHECK
-- -----------------------------------------------------------------------------

-- Confirm squad roles are queryable by squad
SELECT
    "SquadId",
    "InstituteId",
    COUNT(*) AS role_count
FROM "Roles"
WHERE "SquadId" IS NOT NULL
GROUP BY "SquadId", "InstituteId"
ORDER BY "InstituteId", "SquadId";
