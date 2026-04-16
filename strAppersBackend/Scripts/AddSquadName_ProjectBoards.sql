-- Add SquadName to ProjectBoards (varchar 100, nullable) + index for search.
-- Safe to run once on PostgreSQL.

ALTER TABLE "ProjectBoards"
    ADD COLUMN IF NOT EXISTS "SquadName" character varying(100) NULL;

CREATE INDEX IF NOT EXISTS "IX_ProjectBoards_SquadName"
    ON "ProjectBoards" ("SquadName");
