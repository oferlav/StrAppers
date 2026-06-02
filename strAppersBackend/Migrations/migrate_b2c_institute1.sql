-- =============================================================================
-- Gap 5 Migration: Set Student.InstituteId = 1 for all B2C students
--
-- B2C students currently have InstituteId = NULL.
-- After this migration they get InstituteId = 1 (the platform institute),
-- which routes them through the institute project selection path.
--
-- Prerequisite: Institutes row with Id = 1 must exist.
-- Database: PostgreSQL
-- Safe to run: wrapped in a transaction.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- PREVIEW (run separately first — no changes)
-- -----------------------------------------------------------------------------

SELECT
    COUNT(*)                                              AS total_students,
    COUNT(*) FILTER (WHERE "InstituteId" IS NULL)        AS b2c_students_to_update,
    COUNT(*) FILTER (WHERE "InstituteId" IS NOT NULL)    AS institute_students_unchanged;


-- -----------------------------------------------------------------------------
-- MIGRATION
-- -----------------------------------------------------------------------------

BEGIN;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "Institutes" WHERE "Id" = 1) THEN
        RAISE EXCEPTION 'Prerequisite failed: no Institutes row with Id=1.';
    END IF;
END $$;

UPDATE "Students"
SET "InstituteId" = 1
WHERE "InstituteId" IS NULL;

DO $$
DECLARE
    null_count integer;
BEGIN
    SELECT COUNT(*) INTO null_count FROM "Students" WHERE "InstituteId" IS NULL;
    IF null_count > 0 THEN
        RAISE EXCEPTION 'Migration failed: % students still have NULL InstituteId.', null_count;
    END IF;
    RAISE NOTICE 'Migration complete: all students now have a non-null InstituteId.';
END $$;

COMMIT;


-- -----------------------------------------------------------------------------
-- POST-MIGRATION CHECK
-- -----------------------------------------------------------------------------

SELECT "InstituteId", COUNT(*) AS student_count
FROM "Students"
GROUP BY "InstituteId"
ORDER BY "InstituteId";
