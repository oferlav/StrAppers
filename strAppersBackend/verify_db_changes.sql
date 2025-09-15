-- Verify all database changes for AI Agent System Design Module
-- This script verifies that all required tables and columns exist

-- Check Projects table has the new fields
SELECT 'Projects Table - SystemDesign Fields' as check_type, column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'Projects' 
AND column_name IN ('SystemDesign', 'SystemDesignDoc')
ORDER BY column_name;

-- Check DesignVersions table exists and has correct structure
SELECT 'DesignVersions Table Structure' as check_type, column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'DesignVersions'
ORDER BY ordinal_position;

-- Check foreign key constraints
SELECT 'Foreign Key Constraints' as check_type, 
       tc.constraint_name, 
       tc.table_name, 
       kcu.column_name, 
       ccu.table_name AS foreign_table_name,
       ccu.column_name AS foreign_column_name 
FROM information_schema.table_constraints AS tc 
JOIN information_schema.key_column_usage AS kcu
  ON tc.constraint_name = kcu.constraint_name
  AND tc.table_schema = kcu.table_schema
JOIN information_schema.constraint_column_usage AS ccu
  ON ccu.constraint_name = tc.constraint_name
  AND ccu.table_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY' 
AND tc.table_name = 'DesignVersions';

-- Check indexes
SELECT 'Indexes' as check_type, indexname, tablename, indexdef
FROM pg_indexes 
WHERE tablename IN ('Projects', 'DesignVersions')
ORDER BY tablename, indexname;
