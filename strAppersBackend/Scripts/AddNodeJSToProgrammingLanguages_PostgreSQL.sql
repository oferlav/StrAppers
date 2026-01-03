-- Add NodeJS (id=7) to ProgrammingLanguages table
-- This script adds NodeJS as a programming language option

-- Insert NodeJS if it doesn't already exist
INSERT INTO "ProgrammingLanguages" ("Id", "Name", "ReleaseYear", "Creator", "Description", "IsActive", "CreatedAt")
VALUES 
    (7, 'NodeJS', 2009, 'Ryan Dahl', 'A JavaScript runtime built on Chrome''s V8 JavaScript engine. Popular frameworks: Express.js, Nest.js, Fastify. Enables server-side JavaScript development, perfect for building scalable network applications, APIs, and real-time web applications.', TRUE, NOW())
ON CONFLICT ("Id") DO UPDATE 
SET 
    "Name" = EXCLUDED."Name",
    "ReleaseYear" = EXCLUDED."ReleaseYear",
    "Creator" = EXCLUDED."Creator",
    "Description" = EXCLUDED."Description",
    "IsActive" = EXCLUDED."IsActive",
    "UpdatedAt" = NOW();

-- Verify NodeJS was added successfully
SELECT 
    "Id",
    "Name",
    "ReleaseYear",
    "Creator",
    "IsActive",
    "CreatedAt"
FROM "ProgrammingLanguages"
WHERE "Id" = 7 OR "Name" = 'NodeJS'
ORDER BY "Id";







