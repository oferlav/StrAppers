-- Institute-owned project designs (custom + activated catalog copies).
-- Run after reviewing your DB state; align with EF migration AddInstituteProjectsAndTemplateProjectLinks when possible.

CREATE TABLE IF NOT EXISTS "InstituteProjects" (
    "Id" SERIAL NOT NULL,
    "InstituteId" INTEGER NOT NULL,
    "BaseProjectId" INTEGER NULL,
    "Title" VARCHAR(200) NOT NULL,
    "Mission" VARCHAR(2000) NULL,
    "OneLiner" VARCHAR(250) NULL,
    "Description" VARCHAR(1000) NULL,
    "ExtendedDescription" TEXT NULL,
    "SystemDesign" TEXT NULL,
    "DataSchema" TEXT NULL,
    "Logo" TEXT NULL,
    "SystemDesignDoc" BYTEA NULL,
    "SystemDesignFormatted" VARCHAR(2000) NULL,
    "Priority" VARCHAR(50) NOT NULL DEFAULT 'Medium',
    "OrganizationId" INTEGER NULL,
    "isAvailable" BOOLEAN NOT NULL DEFAULT TRUE,
    "InUse" BOOLEAN NOT NULL DEFAULT TRUE,
    "Kickoff" BOOLEAN NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NULL,
    "TrelloBoardJson" TEXT NULL,
    "CustomerPastStory" TEXT NULL,
    "ShortBrief" VARCHAR(2000) NULL,
    "deployment_manifest" TEXT NULL,
    "ide_generation_status" VARCHAR(50) NOT NULL DEFAULT 'not_started',
    "total_chunks" INTEGER NOT NULL DEFAULT 0,
    "completed_chunks" INTEGER NOT NULL DEFAULT 0,
    "mock_records_count" INTEGER NOT NULL DEFAULT 10,
    "CriteriaIds" VARCHAR(500) NULL,
    CONSTRAINT "PK_InstituteProjects" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_InstituteProjects_Institutes_InstituteId" FOREIGN KEY ("InstituteId") REFERENCES "Institutes" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_InstituteProjects_Organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_InstituteProjects_Projects_BaseProjectId" FOREIGN KEY ("BaseProjectId") REFERENCES "Projects" ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_InstituteProjects_InstituteId" ON "InstituteProjects" ("InstituteId");
CREATE INDEX IF NOT EXISTS "IX_InstituteProjects_BaseProjectId" ON "InstituteProjects" ("BaseProjectId");

ALTER TABLE "ProjectModules" ADD COLUMN IF NOT EXISTS "InstituteProjectId" INTEGER NULL;
ALTER TABLE "ProjectModules" ADD COLUMN IF NOT EXISTS "OriginalModuleId" INTEGER NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_ProjectModules_InstituteProjects_InstituteProjectId'
    ) THEN
        ALTER TABLE "ProjectModules"
            ADD CONSTRAINT "FK_ProjectModules_InstituteProjects_InstituteProjectId"
            FOREIGN KEY ("InstituteProjectId") REFERENCES "InstituteProjects" ("Id") ON DELETE CASCADE;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_ProjectModules_InstituteProjectId" ON "ProjectModules" ("InstituteProjectId");

-- InstituteTemplates: link either to Projects or InstituteProjects
ALTER TABLE "InstituteTemplates" ADD COLUMN IF NOT EXISTS "InstituteProjectId" INTEGER NULL;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'InstituteTemplates' AND column_name = 'ProjectId' AND is_nullable = 'NO'
    ) THEN
        ALTER TABLE "InstituteTemplates" ALTER COLUMN "ProjectId" DROP NOT NULL;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_InstituteTemplates_InstituteProjects_InstituteProjectId'
    ) THEN
        ALTER TABLE "InstituteTemplates"
            ADD CONSTRAINT "FK_InstituteTemplates_InstituteProjects_InstituteProjectId"
            FOREIGN KEY ("InstituteProjectId") REFERENCES "InstituteProjects" ("Id") ON DELETE CASCADE;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_InstituteTemplates_InstituteProjectId" ON "InstituteTemplates" ("InstituteProjectId");

-- Optional: separate id ranges (adjust sequence start after initial deploy if needed)
-- SELECT setval(pg_get_serial_sequence('"InstituteProjects"', 'Id'), 10000000, false);
