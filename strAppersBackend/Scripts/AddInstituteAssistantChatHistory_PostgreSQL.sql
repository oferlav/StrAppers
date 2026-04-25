-- Institute staff assistant chat history (user + assistant text; not structured field outputs).
-- Matches EF migration 20260424150000_AddInstituteAssistantChatHistory.

CREATE TABLE IF NOT EXISTS "InstituteAssistantChatHistory" (
    "Id" SERIAL NOT NULL,
    "InstituteId" INTEGER NOT NULL,
    "TeacherId" INTEGER NOT NULL,
    "ProjectId" INTEGER NOT NULL,
    "Source" VARCHAR(32) NOT NULL,
    "IsAssistant" BOOLEAN NOT NULL,
    "Message" TEXT NOT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL,
    CONSTRAINT "PK_InstituteAssistantChatHistory" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_InstituteAssistantChatHistory_Institutes_InstituteId"
        FOREIGN KEY ("InstituteId") REFERENCES "Institutes" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_InstituteAssistantChatHistory_Teachers_TeacherId"
        FOREIGN KEY ("TeacherId") REFERENCES "Teachers" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_InstituteAssistantChatHistory_Projects_ProjectId"
        FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_IACH_InstituteId_TeacherId_ProjectId_Source_CreatedAt"
    ON "InstituteAssistantChatHistory" ("InstituteId", "TeacherId", "ProjectId", "Source", "CreatedAt");
