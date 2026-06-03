-- =============================================================================
-- Roles: Make InstituteId nullable — global/B2C roles get InstituteId = NULL
--
-- Follows: 20260602120000_AddInstituteIdToRolesAndMigrateInstituteRoles
-- EF migration: 20260603120000_MakeRolesInstituteIdNullableForGlobalRoles
--
-- Why: InstituteId=1 previously served as both the "platform institute" and
-- the sentinel for global/B2C roles. This prevented institute-1 admins from
-- customising their own roles. Using NULL as the global sentinel decouples the
-- two concerns: NULL = global catalog (never modified), any non-null value =
-- institute-specific.
--
-- Safe to run: wrapped in a transaction. Run EF migration first OR this script
-- (both do the same thing; running twice is harmless — no rows with InstituteId=1
-- will remain after the first run).
-- =============================================================================

BEGIN;

-- ── Step 1: Drop NOT NULL constraint ─────────────────────────────────────────
ALTER TABLE "Roles" ALTER COLUMN "InstituteId" DROP DEFAULT;
ALTER TABLE "Roles" ALTER COLUMN "InstituteId" DROP NOT NULL;

-- ── Step 2: Global roles → NULL ───────────────────────────────────────────────
-- All rows with InstituteId=1 at this point are the original B2C/global roles.
-- Institute-1-specific rows do not exist yet (no institute-1 admin has saved
-- customisations), so updating all InstituteId=1 rows is safe.
UPDATE "Roles" SET "InstituteId" = NULL WHERE "InstituteId" = 1;

-- ── Step 3: Verify ────────────────────────────────────────────────────────────
DO $$
DECLARE
    global_count   integer;
    inst_count     integer;
BEGIN
    SELECT COUNT(*) INTO global_count FROM "Roles" WHERE "InstituteId" IS NULL;
    SELECT COUNT(*) INTO inst_count   FROM "Roles" WHERE "InstituteId" IS NOT NULL;
    IF global_count = 0 THEN
        RAISE EXCEPTION 'No global (NULL InstituteId) roles found after migration. Something is wrong.';
    END IF;
    RAISE NOTICE 'Migration complete: % global roles (InstituteId=NULL), % institute roles.',
        global_count, inst_count;
END $$;

COMMIT;

-- ── Post-migration check ──────────────────────────────────────────────────────
SELECT
    "InstituteId",
    COUNT(*) AS role_count
FROM "Roles"
GROUP BY "InstituteId"
ORDER BY "InstituteId" NULLS FIRST;
