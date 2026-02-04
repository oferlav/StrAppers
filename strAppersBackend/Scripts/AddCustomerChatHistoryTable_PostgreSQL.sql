-- Add CustomerChatHistory table for PostgreSQL
-- Same structure as MentorChatHistory. For Customer: StudentId column stores ProjectId, SprintId column stores SprintNumber (no FK to Students).
-- Note: PostgreSQL converts unquoted identifiers to lowercase; we use quoted names to match EF.

-- Create CustomerChatHistory table (no FK)
CREATE TABLE IF NOT EXISTS "CustomerChatHistory" (
    "Id" SERIAL PRIMARY KEY,
    "StudentId" INTEGER NOT NULL,
    "SprintId" INTEGER NOT NULL,
    "Role" VARCHAR(50) NOT NULL,
    "Message" TEXT NOT NULL,
    "AIModelName" VARCHAR(100) NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Add comments for documentation
COMMENT ON TABLE "CustomerChatHistory" IS 'Stores chat history for customer conversations (StudentId=ProjectId, SprintId=SprintNumber)';
COMMENT ON COLUMN "CustomerChatHistory"."Id" IS 'Primary key identifier';
COMMENT ON COLUMN "CustomerChatHistory"."StudentId" IS 'Stores ProjectId for customer context';
COMMENT ON COLUMN "CustomerChatHistory"."SprintId" IS 'Stores SprintNumber for customer context';
COMMENT ON COLUMN "CustomerChatHistory"."Role" IS 'Message role: "user" or "assistant"';
COMMENT ON COLUMN "CustomerChatHistory"."Message" IS 'The chat message content';
COMMENT ON COLUMN "CustomerChatHistory"."AIModelName" IS 'AI model used for assistant messages';
COMMENT ON COLUMN "CustomerChatHistory"."CreatedAt" IS 'Message creation timestamp';

-- Create indexes for efficient querying
CREATE INDEX IF NOT EXISTS "IX_CustomerChatHistory_StudentId"
    ON "CustomerChatHistory" ("StudentId");

CREATE INDEX IF NOT EXISTS "IX_CustomerChatHistory_SprintId"
    ON "CustomerChatHistory" ("SprintId");
