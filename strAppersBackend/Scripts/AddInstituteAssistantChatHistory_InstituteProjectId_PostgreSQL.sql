-- Optional manual migration matching EF 20260503110412_InstituteAssistantChatHistoryInstituteProject.
-- Run on PostgreSQL if you apply schema changes without dotnet ef.

DROP INDEX IF EXISTS "IX_IACH_InstituteId_TeacherId_ProjectId_Source_CreatedAt";

ALTER TABLE "InstituteAssistantChatHistory" ALTER COLUMN "ProjectId" DROP NOT NULL;

ALTER TABLE "InstituteAssistantChatHistory"
    ADD COLUMN IF NOT EXISTS "InstituteProjectId" integer NULL;

CREATE INDEX IF NOT EXISTS "IX_IACH_InstituteId_TeacherId_Scope_Source_CreatedAt"
    ON "InstituteAssistantChatHistory" ("InstituteId", "TeacherId", "ProjectId", "InstituteProjectId", "Source", "CreatedAt");

CREATE INDEX IF NOT EXISTS "IX_InstituteAssistantChatHistory_InstituteProjectId"
    ON "InstituteAssistantChatHistory" ("InstituteProjectId");

ALTER TABLE "InstituteAssistantChatHistory"
    DROP CONSTRAINT IF EXISTS "FK_InstituteAssistantChatHistory_InstituteProjects_InstitutePr~";

ALTER TABLE "InstituteAssistantChatHistory"
    ADD CONSTRAINT "FK_InstituteAssistantChatHistory_InstituteProjects_InstituteProjectId"
    FOREIGN KEY ("InstituteProjectId") REFERENCES "InstituteProjects" ("Id") ON DELETE CASCADE;
