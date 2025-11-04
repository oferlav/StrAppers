-- Create ProgrammingLanguages table
-- This script creates and populates the ProgrammingLanguages table with the top 6 most common programming languages

-- Create the ProgrammingLanguages table
CREATE TABLE IF NOT EXISTS "ProgrammingLanguages" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL UNIQUE,
    "ReleaseYear" INTEGER,
    "Creator" VARCHAR(200),
    "Description" TEXT,
    "IsActive" BOOLEAN DEFAULT TRUE,
    "CreatedAt" TIMESTAMP DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP DEFAULT NOW()
);

-- Create index for better performance on Name lookups
CREATE INDEX IF NOT EXISTS "IX_ProgrammingLanguages_Name" ON "ProgrammingLanguages" ("Name");
CREATE INDEX IF NOT EXISTS "IX_ProgrammingLanguages_IsActive" ON "ProgrammingLanguages" ("IsActive");

-- Insert the top 6 most popular backend/server-side programming languages (2024-2025)
INSERT INTO "ProgrammingLanguages" ("Name", "ReleaseYear", "Creator", "Description", "IsActive")
VALUES
    ('Python', 1991, 'Guido van Rossum', 'A high-level, interpreted backend programming language known for its simplicity and rapid development. Popular frameworks: Django, Flask, FastAPI. Used extensively in web backends, APIs, data processing, AI/ML services, and automation.', TRUE),
    ('Java', 1995, 'James Gosling', 'A class-based, object-oriented backend language designed for enterprise applications. Popular frameworks: Spring Boot, Jakarta EE. Widely used for large-scale backend systems, microservices, enterprise software, and cloud applications.', TRUE),
    ('C#', 2000, 'Microsoft', 'A modern, object-oriented backend programming language developed by Microsoft for the .NET ecosystem. Popular frameworks: ASP.NET Core, .NET. Used for web APIs, enterprise backend services, cloud applications, and Windows server applications.', TRUE),
    ('Go', 2009, 'Google', 'A compiled, statically typed backend language designed for simplicity and performance. Popular frameworks: Gin, Echo, Fiber. Ideal for microservices, cloud-native applications, high-concurrency systems, and distributed backend services.', TRUE),
    ('PHP', 1995, 'Rasmus Lerdorf', 'A server-side scripting language designed for web backend development. Popular frameworks: Laravel, Symfony, CodeIgniter. Powers millions of websites and content management systems like WordPress.', TRUE),
    ('Ruby', 1995, 'Yukihiro Matsumoto', 'A dynamic, object-oriented backend programming language known for developer productivity. Popular framework: Ruby on Rails. Used for rapid web application development, APIs, and backend services.', TRUE);

-- Verify the table was created and populated successfully
SELECT 
    "Id",
    "Name",
    "ReleaseYear",
    "Creator",
    "IsActive",
    "CreatedAt"
FROM "ProgrammingLanguages"
ORDER BY "Name";

