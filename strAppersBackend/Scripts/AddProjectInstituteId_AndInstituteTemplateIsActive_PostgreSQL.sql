BEGIN;

-- 1) Projects.InstituteId (nullable FK -> Institutes.Id)
ALTER TABLE "Projects"
    ADD COLUMN IF NOT EXISTS "InstituteId" integer;

CREATE INDEX IF NOT EXISTS "IX_Projects_InstituteId"
    ON "Projects" ("InstituteId");

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_Projects_Institutes_InstituteId'
    ) THEN
        ALTER TABLE "Projects"
            ADD CONSTRAINT "FK_Projects_Institutes_InstituteId"
            FOREIGN KEY ("InstituteId")
            REFERENCES "Institutes" ("Id")
            ON DELETE SET NULL;
    END IF;
END $$;

-- 2) InstituteTemplates.IsActive (boolean NOT NULL default false)
ALTER TABLE "InstituteTemplates"
    ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT false;

UPDATE "InstituteTemplates"
SET "IsActive" = false
WHERE "IsActive" IS NULL;

COMMIT;
