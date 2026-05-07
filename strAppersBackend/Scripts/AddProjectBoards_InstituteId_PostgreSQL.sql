-- Adds nullable InstituteId to ProjectBoards and links it to Institutes(Id).
-- Safe to run multiple times.

ALTER TABLE "ProjectBoards"
    ADD COLUMN IF NOT EXISTS "InstituteId" integer NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_ProjectBoards_Institutes_InstituteId'
    ) THEN
        ALTER TABLE "ProjectBoards"
            ADD CONSTRAINT "FK_ProjectBoards_Institutes_InstituteId"
            FOREIGN KEY ("InstituteId")
            REFERENCES "Institutes" ("Id")
            ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_ProjectBoards_InstituteId"
    ON "ProjectBoards" ("InstituteId");
