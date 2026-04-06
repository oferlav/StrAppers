-- Add AssistMe to Students (PostgreSQL), NOT NULL DEFAULT false.
-- Matches EF migration: 20260405120000_AddAssistMeToStudents

ALTER TABLE "Students"
    ADD COLUMN IF NOT EXISTS "AssistMe" boolean NOT NULL DEFAULT false;

-- Rollback (run manually if needed):
-- ALTER TABLE "Students" DROP COLUMN IF EXISTS "AssistMe";
