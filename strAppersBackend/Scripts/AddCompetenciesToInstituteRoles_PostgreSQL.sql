-- Add Competencies column to InstituteRoles
ALTER TABLE "InstituteRoles"
ADD COLUMN IF NOT EXISTS "Competencies" text;

-- Optional backfill can be done per institute role as needed, e.g.:
-- UPDATE "InstituteRoles"
-- SET "Competencies" = 'GitHub workflows, REST API design, PostgreSQL schema migrations'
-- WHERE "Id" = 123;
