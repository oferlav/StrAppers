-- Teardown before recreating "Institutes" (e.g. column changes, wrong first deploy).
-- PostgreSQL. Safe to run multiple times (IF EXISTS).
--
-- Order: FK must be dropped before the referenced table.
-- Students.InstituteId is kept; re-run AddInstitutes_and_StudentInstituteId.sql
-- afterward to recreate the table, seed, index, and FK.

ALTER TABLE "Students" DROP CONSTRAINT IF EXISTS "FK_Students_Institutes_InstituteId";

DROP INDEX IF EXISTS "IX_Students_InstituteId";

DROP TABLE IF EXISTS "Institutes";

-- Optional: remove the student link column entirely (only if you are abandoning
-- InstituteId on Students):
-- ALTER TABLE "Students" DROP COLUMN IF EXISTS "InstituteId";
