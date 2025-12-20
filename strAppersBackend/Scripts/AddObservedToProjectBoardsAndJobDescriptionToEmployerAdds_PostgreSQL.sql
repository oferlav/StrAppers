-- Add Observed field to ProjectBoards table and JobDescription field to EmployerAdds table
-- Run this script on both DEV and PROD PostgreSQL databases

-- ============================================
-- 1. Add 'Observed' column to ProjectBoards table
-- ============================================
ALTER TABLE "ProjectBoards"
ADD COLUMN IF NOT EXISTS "Observed" BOOLEAN NOT NULL DEFAULT FALSE;

-- Add comment for documentation
COMMENT ON COLUMN "ProjectBoards"."Observed" IS 'Whether the project board is being observed';

-- ============================================
-- 2. Add 'JobDescription' column to EmployerAdds table
-- ============================================
ALTER TABLE "EmployerAdds"
ADD COLUMN IF NOT EXISTS "JobDescription" TEXT;

-- Add comment for documentation
COMMENT ON COLUMN "EmployerAdds"."JobDescription" IS 'Job description text for the employer add';

-- ============================================
-- Verification queries (optional - can be run separately)
-- ============================================
-- Verify ProjectBoards.Observed column
-- SELECT column_name, data_type, is_nullable, column_default 
-- FROM information_schema.columns 
-- WHERE table_name = 'ProjectBoards' 
-- AND column_name = 'Observed';

-- Verify EmployerAdds.JobDescription column
-- SELECT column_name, data_type, is_nullable 
-- FROM information_schema.columns 
-- WHERE table_name = 'EmployerAdds' 
-- AND column_name = 'JobDescription';




