-- Add BoardId column to Stakeholders (FK to ProjectBoards.BoardId)
-- Run in pgAdmin or psql. Idempotent: skips if column already exists.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Stakeholders'
          AND column_name = 'BoardId'
    ) THEN
        ALTER TABLE "Stakeholders"
        ADD COLUMN "BoardId" VARCHAR(50) NULL;

        COMMENT ON COLUMN "Stakeholders"."BoardId" IS 'FK to ProjectBoards.BoardId (Trello board ID)';

        CREATE INDEX "IX_Stakeholders_BoardId" ON "Stakeholders" ("BoardId");

        ALTER TABLE "Stakeholders"
        ADD CONSTRAINT "FK_Stakeholders_ProjectBoards_BoardId"
            FOREIGN KEY ("BoardId") REFERENCES "ProjectBoards" ("BoardId") ON DELETE SET NULL;
    END IF;
END $$;
