-- Add MentorChatHistory table for PostgreSQL
-- This script creates the MentorChatHistory table to store conversation history for mentor chatbot
-- Note: PostgreSQL converts unquoted identifiers to lowercase

-- Create MentorChatHistory table
CREATE TABLE IF NOT EXISTS "MentorChatHistory" (
    "Id" SERIAL PRIMARY KEY,
    "StudentId" INTEGER NOT NULL,
    "SprintId" INTEGER NOT NULL,
    "Role" VARCHAR(50) NOT NULL,
    "Message" TEXT NOT NULL,
    "AIModelName" VARCHAR(100) NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "FK_MentorChatHistory_Students" FOREIGN KEY ("StudentId") REFERENCES "Students"("Id") ON DELETE CASCADE
);

-- Add comments for documentation
COMMENT ON TABLE "MentorChatHistory" IS 'Stores chat history for mentor conversations';
COMMENT ON COLUMN "MentorChatHistory"."Id" IS 'Primary key identifier';
COMMENT ON COLUMN "MentorChatHistory"."StudentId" IS 'Foreign key to Students table';
COMMENT ON COLUMN "MentorChatHistory"."SprintId" IS 'Sprint ID for this conversation';
COMMENT ON COLUMN "MentorChatHistory"."Role" IS 'Message role: "user" or "assistant"';
COMMENT ON COLUMN "MentorChatHistory"."Message" IS 'The chat message content';
COMMENT ON COLUMN "MentorChatHistory"."AIModelName" IS 'AI model used for assistant messages';
COMMENT ON COLUMN "MentorChatHistory"."CreatedAt" IS 'Message creation timestamp';

-- Create indexes for efficient querying
CREATE INDEX IF NOT EXISTS "IX_MentorChatHistory_StudentId_SprintId_CreatedAt" 
    ON "MentorChatHistory" ("StudentId", "SprintId", "CreatedAt");

CREATE INDEX IF NOT EXISTS "IX_MentorChatHistory_CreatedAt" 
    ON "MentorChatHistory" ("CreatedAt");

