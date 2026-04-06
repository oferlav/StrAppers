-- Add b2c to Students (PostgreSQL), NOT NULL DEFAULT true.
-- Matches EF migration: 20260327120000_AddB2cToStudents

ALTER TABLE "Students"
    ADD COLUMN IF NOT EXISTS "b2c" boolean NOT NULL DEFAULT true;

-- Rollback (run manually if needed):
-- ALTER TABLE "Students" DROP COLUMN IF EXISTS "b2c";
