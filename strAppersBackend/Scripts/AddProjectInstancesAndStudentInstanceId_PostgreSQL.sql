-- 1. Create ProjectInstances table (Id, ProjectId FK to Projects.Id, InstanceId int unique)
-- 2. Add Students.InstanceId column (nullable int, FK to ProjectInstances.InstanceId)

-- ProjectInstances table
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'ProjectInstances'
    ) THEN
        CREATE TABLE "ProjectInstances" (
            "Id"     serial PRIMARY KEY,
            "ProjectId" integer NOT NULL,
            "InstanceId" integer NOT NULL,
            CONSTRAINT "FK_ProjectInstances_Projects_ProjectId"
                FOREIGN KEY ("ProjectId") REFERENCES "Projects"("Id") ON DELETE RESTRICT
        );

        CREATE UNIQUE INDEX "IX_ProjectInstances_InstanceId" ON "ProjectInstances" ("InstanceId");
        CREATE INDEX "IX_ProjectInstances_ProjectId" ON "ProjectInstances" ("ProjectId");

        COMMENT ON TABLE "ProjectInstances" IS 'Instances of a project (e.g. cohorts/runs). InstanceId is unique for FK from Students.';
    END IF;
END $$;

-- Students.InstanceId column
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Students'
          AND column_name = 'InstanceId'
    ) THEN
        ALTER TABLE "Students"
        ADD COLUMN "InstanceId" integer NULL;

        CREATE INDEX "IX_Students_InstanceId" ON "Students" ("InstanceId");

        ALTER TABLE "Students"
        ADD CONSTRAINT "FK_Students_ProjectInstances_InstanceId"
            FOREIGN KEY ("InstanceId") REFERENCES "ProjectInstances" ("InstanceId") ON DELETE SET NULL;

        COMMENT ON COLUMN "Students"."InstanceId" IS 'FK to ProjectInstances.InstanceId - which project instance the student is in.';
    END IF;
END $$;
