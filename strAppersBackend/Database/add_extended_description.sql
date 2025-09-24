-- Add ExtendedDescription column to Projects table
-- This script adds the ExtendedDescription field as a TEXT column to the Projects table

ALTER TABLE "Projects" 
ADD COLUMN "ExtendedDescription" TEXT;

-- Verify the column was added
SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'Projects' 
AND column_name = 'ExtendedDescription';

