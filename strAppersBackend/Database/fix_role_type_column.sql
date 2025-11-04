-- Fix Role.Type column NULL values
-- This script updates existing roles with NULL Type values to have appropriate default values

-- Update existing roles with NULL Type values
-- Based on the seed data in ApplicationDbContext.cs, we'll assign appropriate types

-- Project Manager -> Type 4 (Leadership)
UPDATE "Roles" SET "Type" = 4 WHERE "Name" = 'Project Manager' AND "Type" IS NULL;

-- Frontend Developer -> Type 1 (Developer)
UPDATE "Roles" SET "Type" = 1 WHERE "Name" = 'Frontend Developer' AND "Type" IS NULL;

-- Backend Developer -> Type 1 (Developer)
UPDATE "Roles" SET "Type" = 1 WHERE "Name" = 'Backend Developer' AND "Type" IS NULL;

-- UI/UX Designer -> Type 3 (UI/UX Designer)
UPDATE "Roles" SET "Type" = 3 WHERE "Name" = 'UI/UX Designer' AND "Type" IS NULL;

-- Quality Assurance -> Type 2 (Junior Developer)
UPDATE "Roles" SET "Type" = 2 WHERE "Name" = 'Quality Assurance' AND "Type" IS NULL;

-- Team Lead -> Type 4 (Leadership)
UPDATE "Roles" SET "Type" = 4 WHERE "Name" = 'Team Lead' AND "Type" IS NULL;

-- Research Assistant -> Type 2 (Junior Developer)
UPDATE "Roles" SET "Type" = 2 WHERE "Name" = 'Research Assistant' AND "Type" IS NULL;

-- Documentation Specialist -> Type 2 (Junior Developer)
UPDATE "Roles" SET "Type" = 2 WHERE "Name" = 'Documentation Specialist' AND "Type" IS NULL;

-- For any other roles that might exist, set a default type of 2 (Junior Developer)
UPDATE "Roles" SET "Type" = 2 WHERE "Type" IS NULL;

-- Verify the update
SELECT "Id", "Name", "Type", "IsActive" FROM "Roles" ORDER BY "Id";





