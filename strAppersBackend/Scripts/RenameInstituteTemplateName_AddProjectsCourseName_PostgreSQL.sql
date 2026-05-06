-- Aligns with EF migration 20260504053218_InstituteTemplates_CourseName_Projects_CourseName
-- Run once against PostgreSQL (dev/staging/prod) if you apply schema outside EF.

BEGIN;

ALTER TABLE "InstituteTemplates" RENAME COLUMN "Name" TO "CourseName";

ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "CourseName" character varying(100) NULL;

UPDATE "Projects"
SET "CourseName" = LEFT("Title", 100)
WHERE "CourseName" IS NULL;

COMMIT;
