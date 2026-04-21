-- Adds:
--   1) InstituteTemplates.Name (character varying(100), NOT NULL)
--   2) InstituteRoles.TemplateId (nullable FK -> InstituteTemplates.Id)
--
-- Safe to run multiple times.

BEGIN;

ALTER TABLE "InstituteTemplates"
    ADD COLUMN IF NOT EXISTS "Name" character varying(100);

UPDATE "InstituteTemplates"
SET "Name" = COALESCE(NULLIF(TRIM("Name"), ''), 'Template')
WHERE "Name" IS NULL OR TRIM("Name") = '';

ALTER TABLE "InstituteTemplates"
    ALTER COLUMN "Name" SET NOT NULL;

ALTER TABLE "InstituteRoles"
    ADD COLUMN IF NOT EXISTS "TemplateId" integer NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_InstituteRoles_InstituteTemplates_TemplateId'
    ) THEN
        ALTER TABLE "InstituteRoles"
            ADD CONSTRAINT "FK_InstituteRoles_InstituteTemplates_TemplateId"
            FOREIGN KEY ("TemplateId")
            REFERENCES "InstituteTemplates" ("Id")
            ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_InstituteRoles_TemplateId"
    ON "InstituteRoles" ("TemplateId");

COMMIT;
