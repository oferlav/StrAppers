-- Migration script: Split GitHub repos and add WebApiUrl
-- This script:
-- 1. Adds GithubFrontendUrl column
-- 2. Renames GithubUrl to GithubBackendUrl
-- 3. Adds WebApiUrl column

-- Step 1: Add GithubFrontendUrl column
ALTER TABLE "ProjectBoards" 
ADD COLUMN IF NOT EXISTS "GithubFrontendUrl" character varying(1000);

-- Step 2: Rename GithubUrl to GithubBackendUrl
-- First, copy data from GithubUrl to GithubBackendUrl if GithubBackendUrl doesn't exist
ALTER TABLE "ProjectBoards" 
ADD COLUMN IF NOT EXISTS "GithubBackendUrl" character varying(1000);

UPDATE "ProjectBoards"
SET "GithubBackendUrl" = "GithubUrl"
WHERE "GithubBackendUrl" IS NULL AND "GithubUrl" IS NOT NULL;

-- Now drop the old column
ALTER TABLE "ProjectBoards" 
DROP COLUMN IF EXISTS "GithubUrl";

-- Step 3: Add WebApiUrl column
ALTER TABLE "ProjectBoards" 
ADD COLUMN IF NOT EXISTS "WebApiUrl" character varying(1000);

-- Add comments for documentation
COMMENT ON COLUMN "ProjectBoards"."GithubBackendUrl" IS 'GitHub backend repository URL for the project board';
COMMENT ON COLUMN "ProjectBoards"."GithubFrontendUrl" IS 'GitHub frontend repository URL for the project board';
COMMENT ON COLUMN "ProjectBoards"."WebApiUrl" IS 'Web API URL (Swagger URL from Railway deployment)';



