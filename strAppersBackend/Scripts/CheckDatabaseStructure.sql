-- Check what tables exist in the database
-- Run this to see the current database structure

-- List all tables in the current database
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' 
ORDER BY table_name;

-- Check if Projects table exists (case-insensitive)
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' 
AND LOWER(table_name) = 'projects';

-- Check if Projects table exists (case-sensitive)
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' 
AND table_name = 'Projects';



