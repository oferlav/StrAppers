-- Kickoff meeting dispute feature: adds columns for the squad kickoff-date agreement flow.
-- Run this script manually (no EF Core migration for this feature).
--
-- IMPORTANT: Existing ProjectBoards rows are intentionally left untouched. KickoffState,
-- LastDateStudentId and SuggestedKickoffDate stay NULL on every pre-existing board forever —
-- NULL means "legacy board, not part of this feature." Only boards created after the backend
-- deploy will have KickoffState set (starting at 0). See MeetingDispute_Kickoff_Plan.md
-- sections 4 and 8 for the rationale.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'ProjectBoards' AND column_name = 'KickoffState'
    ) THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "KickoffState" integer NULL;
        RAISE NOTICE 'Column KickoffState added successfully to ProjectBoards table';
    ELSE
        RAISE NOTICE 'Column KickoffState already exists in ProjectBoards table';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'ProjectBoards' AND column_name = 'LastDateStudentId'
    ) THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "LastDateStudentId" integer NULL;
        ALTER TABLE "ProjectBoards" ADD CONSTRAINT "FK_ProjectBoards_Students_LastDateStudentId"
            FOREIGN KEY ("LastDateStudentId") REFERENCES "Students"("Id");
        RAISE NOTICE 'Column LastDateStudentId (+ FK) added successfully to ProjectBoards table';
    ELSE
        RAISE NOTICE 'Column LastDateStudentId already exists in ProjectBoards table';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'ProjectBoards' AND column_name = 'SuggestedKickoffDate'
    ) THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "SuggestedKickoffDate" timestamp with time zone NULL;
        RAISE NOTICE 'Column SuggestedKickoffDate added successfully to ProjectBoards table';
    ELSE
        RAISE NOTICE 'Column SuggestedKickoffDate already exists in ProjectBoards table';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'ProjectBoards' AND column_name = 'IsStale'
    ) THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "IsStale" boolean NOT NULL DEFAULT false;
        RAISE NOTICE 'Column IsStale added successfully to ProjectBoards table';
    ELSE
        RAISE NOTICE 'Column IsStale already exists in ProjectBoards table';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Students' AND column_name = 'ApprovedKickoff'
    ) THEN
        ALTER TABLE "Students" ADD COLUMN "ApprovedKickoff" boolean NOT NULL DEFAULT false;
        RAISE NOTICE 'Column ApprovedKickoff added successfully to Students table';
    ELSE
        RAISE NOTICE 'Column ApprovedKickoff already exists in Students table';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE tablename = 'ProjectBoards' AND indexname = 'IX_ProjectBoards_KickoffState_CreatedAt'
    ) THEN
        -- Speeds up the periodic reset-job scan (StudentTeamBuilderService.Worker).
        CREATE INDEX "IX_ProjectBoards_KickoffState_CreatedAt" ON "ProjectBoards" ("CreatedAt")
            WHERE "KickoffState" IS NOT NULL AND "KickoffState" < 2;
        RAISE NOTICE 'Index IX_ProjectBoards_KickoffState_CreatedAt created successfully';
    ELSE
        RAISE NOTICE 'Index IX_ProjectBoards_KickoffState_CreatedAt already exists';
    END IF;
END $$;

COMMENT ON COLUMN "ProjectBoards"."KickoffState" IS 'NULL = legacy board (pre-feature, unaffected); 0 = nobody proposed a kickoff time; 1 = a time is proposed, awaiting unanimous approval; 2 = everyone approved (terminal), real invite sent.';
COMMENT ON COLUMN "ProjectBoards"."LastDateStudentId" IS 'Student who most recently proposed SuggestedKickoffDate.';
COMMENT ON COLUMN "ProjectBoards"."SuggestedKickoffDate" IS 'Kickoff date/time currently proposed (KickoffState 0/1) or agreed (KickoffState 2).';
COMMENT ON COLUMN "ProjectBoards"."IsStale" IS 'True once the squad was auto-reset for failing to agree on a kickoff time within the 3-day deadline.';
COMMENT ON COLUMN "Students"."ApprovedKickoff" IS 'Whether this student approved the board''s current SuggestedKickoffDate. Reset to false whenever a new date is suggested or the squad is reset.';

-- Verify
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE (table_name = 'ProjectBoards' AND column_name IN ('KickoffState','LastDateStudentId','SuggestedKickoffDate','IsStale'))
   OR (table_name = 'Students' AND column_name = 'ApprovedKickoff')
ORDER BY table_name, column_name;
