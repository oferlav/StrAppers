-- Add Resources table for PostgreSQL
-- Resources are links (Figma or other) associated with a board and a student.

CREATE TABLE IF NOT EXISTS "Resources" (
    "Id" SERIAL PRIMARY KEY,
    "BoardId" VARCHAR(50) NOT NULL,
    "StudentId" INTEGER NOT NULL,
    "Name" VARCHAR(100) NOT NULL,
    "Url" VARCHAR(1000) NOT NULL,
    "IsFigma" BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT "FK_Resources_ProjectBoards_BoardId"
        FOREIGN KEY ("BoardId")
        REFERENCES "ProjectBoards" ("BoardId")
        ON DELETE CASCADE,
    CONSTRAINT "FK_Resources_Students_StudentId"
        FOREIGN KEY ("StudentId")
        REFERENCES "Students" ("Id")
        ON DELETE CASCADE
);

COMMENT ON TABLE "Resources" IS 'Resource links (Figma or other) per board and student';
COMMENT ON COLUMN "Resources"."BoardId" IS 'Foreign key to ProjectBoards';
COMMENT ON COLUMN "Resources"."StudentId" IS 'Foreign key to Students';
COMMENT ON COLUMN "Resources"."Name" IS 'Display name of the resource';
COMMENT ON COLUMN "Resources"."Url" IS 'Resource URL';
COMMENT ON COLUMN "Resources"."IsFigma" IS 'True if this is a Figma link';

CREATE INDEX IF NOT EXISTS "IX_Resources_BoardId"
    ON "Resources" ("BoardId");

CREATE INDEX IF NOT EXISTS "IX_Resources_StudentId"
    ON "Resources" ("StudentId");
