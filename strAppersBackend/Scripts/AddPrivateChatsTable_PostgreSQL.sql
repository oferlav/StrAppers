-- Add PrivateChats table for PostgreSQL
-- Private chat between two participants (by email) on a board.
-- Email1 and Email2 must always be stored in alphabetical order so there is a single record per pair.
-- Application logic must normalize (email1, email2) to alphabetical order before insert/select.

-- Create PrivateChats table
CREATE TABLE IF NOT EXISTS "PrivateChats" (
    "Id" SERIAL PRIMARY KEY,
    "BoardId" VARCHAR(50) NOT NULL,
    "Email1" VARCHAR(255) NOT NULL,
    "Email2" VARCHAR(255) NOT NULL,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "ChatHistory" TEXT NULL,
    CONSTRAINT "FK_PrivateChats_ProjectBoards_BoardId"
        FOREIGN KEY ("BoardId")
        REFERENCES "ProjectBoards" ("BoardId")
        ON DELETE CASCADE
);

COMMENT ON TABLE "PrivateChats" IS 'Private chat between two participants (by email) on a board; Email1/Email2 stored in alphabetical order';
COMMENT ON COLUMN "PrivateChats"."BoardId" IS 'Foreign key to ProjectBoards (Trello board ID)';
COMMENT ON COLUMN "PrivateChats"."Email1" IS 'First participant email (alphabetically first); references Students.Email';
COMMENT ON COLUMN "PrivateChats"."Email2" IS 'Second participant email (alphabetically second); references Students.Email';
COMMENT ON COLUMN "PrivateChats"."UpdatedAt" IS 'Last update time';
COMMENT ON COLUMN "PrivateChats"."ChatHistory" IS 'Chat content (same format as GroupChat: [timestamp] email: text)';

-- Index for lookups by board
CREATE INDEX IF NOT EXISTS "IX_PrivateChats_BoardId"
    ON "PrivateChats" ("BoardId");

-- Unique constraint: one row per (BoardId, Email1, Email2) with emails in alphabetical order
CREATE UNIQUE INDEX IF NOT EXISTS "IX_PrivateChats_BoardId_Email1_Email2"
    ON "PrivateChats" ("BoardId", "Email1", "Email2");
