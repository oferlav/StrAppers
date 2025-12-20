-- Script to add PasswordHash column to Employers table
-- This script adds the PasswordHash field (character varying 256) to the Employers table

-- Add PasswordHash column
ALTER TABLE "Employers" 
ADD COLUMN IF NOT EXISTS "PasswordHash" VARCHAR(256) NULL;

-- Add comment to document the column
COMMENT ON COLUMN "Employers"."PasswordHash" IS 'Hashed password for employer authentication';

-- Verification query (optional - uncomment to check results)
-- SELECT "Id", "Name", "ContactEmail", "PasswordHash" IS NOT NULL AS "HasPassword" FROM "Employers" LIMIT 10;




