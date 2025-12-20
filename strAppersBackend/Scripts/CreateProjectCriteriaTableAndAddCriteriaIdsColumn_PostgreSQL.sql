-- Create ProjectCriteria table and add CriteriaIds column to Projects table for PostgreSQL
-- This script creates the ProjectCriteria lookup table and adds CriteriaIds column to Projects
-- Run this script on both production and test databases

-- Create ProjectCriteria table (if it doesn't exist)
CREATE TABLE IF NOT EXISTS "ProjectCriterias" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL
);

-- Add CriteriaIds column to Projects table (if it doesn't exist)
DO $$ 
BEGIN
    -- Add CriteriaIds column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Projects' 
        AND column_name = 'CriteriaIds'
    ) THEN
        ALTER TABLE "Projects" 
        ADD COLUMN "CriteriaIds" VARCHAR(500);
        
        -- Add comment for documentation
        COMMENT ON COLUMN "Projects"."CriteriaIds" IS 'Comma-separated string of ProjectCriteria Ids attached to the project';
    END IF;
END $$;

-- Insert initial ProjectCriteria data (if they don't exist)
INSERT INTO "ProjectCriterias" ("Id", "Name")
VALUES 
    (1, 'Popular Projects'),
    (2, 'UI/UX Designer Needed'),
    (3, 'Backend Developer Needed'),
    (4, 'Frontend Developer Needed'),
    (5, 'Product manager Needed'),
    (6, 'Marketing Needed'),
    (7, 'New Projects')
ON CONFLICT ("Id") DO NOTHING;

-- Create index on ProjectCriterias Name for better performance (optional)
CREATE INDEX IF NOT EXISTS "IX_ProjectCriterias_Name" ON "ProjectCriterias"("Name");

-- Add comment for documentation
COMMENT ON TABLE "ProjectCriterias" IS 'Lookup table for project criteria/categories';





