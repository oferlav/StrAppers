-- =============================================================================
-- A3 Migration: Add InstituteId/SquadId/IsTechnical/Competencies to Roles
--               + copy InstituteRoles (InstituteId <> 1) into Roles
--
-- Database: PostgreSQL
-- Safe to run: wrapped in a transaction — any failure rolls back everything.
-- Pre-requisite: an Institutes row with Id = 1 must exist.
--
-- Run the PREVIEW query below first (outside the transaction) to see what
-- will be copied, then run the migration block.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- PREVIEW (run this first, separately — does not change anything)
-- -----------------------------------------------------------------------------

SELECT
    (SELECT COUNT(*) FROM "Roles")                                 AS current_roles_count,
    (SELECT COUNT(*) FROM "InstituteRoles" WHERE "InstituteId" <> 1) AS institute_roles_to_copy,
    (SELECT EXISTS (SELECT 1 FROM "Institutes" WHERE "Id" = 1))    AS platform_institute_exists;


-- -----------------------------------------------------------------------------
-- MIGRATION (run as a block)
-- -----------------------------------------------------------------------------

BEGIN;

-- Prerequisite check: platform institute (Id=1) must exist
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "Institutes" WHERE "Id" = 1) THEN
        RAISE EXCEPTION
            'Prerequisite failed: no Institutes row with Id=1. '
            'Create the platform institute first, then re-run.';
    END IF;
END $$;

-- ── Step 1: Add new columns to Roles ─────────────────────────────────────────
--
-- InstituteId: NOT NULL DEFAULT 1 — PostgreSQL backfills all existing rows
--              with 1 immediately, no separate UPDATE needed.
-- SquadId:     nullable — base institute roles have no squad.
-- IsTechnical: NOT NULL DEFAULT false.
-- Competencies: nullable text.

ALTER TABLE "Roles"
    ADD COLUMN "InstituteId"  integer NOT NULL DEFAULT 1,
    ADD COLUMN "SquadId"      integer          DEFAULT NULL,
    ADD COLUMN "IsTechnical"  boolean NOT NULL DEFAULT false,
    ADD COLUMN "Competencies" text             DEFAULT NULL;

-- ── Step 2: Explicit confirmation that existing rows now have InstituteId = 1 ─
--
-- The DEFAULT 1 above already handles this. This UPDATE is a no-op but makes
-- the intent explicit and will catch any edge case.

UPDATE "Roles"
SET "InstituteId" = 1
WHERE "InstituteId" IS NULL OR "InstituteId" = 0;

-- ── Step 3: Indexes ───────────────────────────────────────────────────────────

CREATE INDEX "IX_Roles_InstituteId" ON "Roles" ("InstituteId");
CREATE INDEX "IX_Roles_SquadId"     ON "Roles" ("SquadId");

-- ── Step 4: Foreign key constraints ──────────────────────────────────────────

ALTER TABLE "Roles"
    ADD CONSTRAINT "FK_Roles_Institutes_InstituteId"
        FOREIGN KEY ("InstituteId")
        REFERENCES "Institutes" ("Id")
        ON DELETE CASCADE,

    ADD CONSTRAINT "FK_Roles_InstituteSquads_SquadId"
        FOREIGN KEY ("SquadId")
        REFERENCES "InstituteSquads" ("Id")
        ON DELETE SET NULL;

-- ── Step 5: Copy InstituteRoles (InstituteId <> 1) into Roles ─────────────────
--
-- These are institute-specific base roles. SquadId = NULL because InstituteRole
-- is the base layer (not squad-scoped). TemplateId is always null in production
-- and is not carried over — it has no equivalent in Roles.
-- New auto-increment Ids are assigned; original InstituteRoles rows are untouched.

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
    ir."Name",
    ir."Description",
    ir."Competencies",
    COALESCE(ir."Category", ''),   -- Category is NOT NULL on Roles; default to empty string
    ir."Type",
    ir."SkillId",
    ir."CustomerEngagement",
    ir."IsTechnical",
    ir."IsActive",
    ir."InstituteId",
    NULL,              -- SquadId: base institute roles are not squad-scoped
    ir."CreatedAt",
    ir."UpdatedAt"
FROM "InstituteRoles" ir
WHERE ir."InstituteId" <> 1;

-- ── Step 6: Verify before committing ─────────────────────────────────────────

DO $$
DECLARE
    b2c_count        integer;
    institute_count  integer;
    source_count     integer;
BEGIN
    SELECT COUNT(*) INTO b2c_count
    FROM "Roles" WHERE "InstituteId" = 1;

    SELECT COUNT(*) INTO institute_count
    FROM "Roles" WHERE "InstituteId" <> 1;

    SELECT COUNT(*) INTO source_count
    FROM "InstituteRoles" WHERE "InstituteId" <> 1;

    IF institute_count <> source_count THEN
        RAISE EXCEPTION
            'Row count mismatch: InstituteRoles source has % rows but Roles received %. Rolling back.',
            source_count, institute_count;
    END IF;

    RAISE NOTICE 'Migration verified: % B2C roles (InstituteId=1), % institute roles copied (% expected).',
        b2c_count, institute_count, source_count;
END $$;

COMMIT;

-- -----------------------------------------------------------------------------
-- POST-MIGRATION CHECK (run after commit to confirm final state)
-- -----------------------------------------------------------------------------

SELECT
    "InstituteId",
    COUNT(*)        AS role_count,
    COUNT("SquadId") AS squad_scoped
FROM "Roles"
GROUP BY "InstituteId"
ORDER BY "InstituteId";
