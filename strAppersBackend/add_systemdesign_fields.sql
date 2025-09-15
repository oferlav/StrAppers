-- Add SystemDesign and SystemDesignDoc fields to Projects table
-- This script adds the SystemDesign (TEXT) and SystemDesignDoc (BYTEA) fields to the Projects table

ALTER TABLE "Projects" 
ADD COLUMN "SystemDesign" TEXT,
ADD COLUMN "SystemDesignDoc" BYTEA;

-- Verify the columns were added
SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'Projects' 
AND column_name IN ('SystemDesign', 'SystemDesignDoc')
ORDER BY column_name;
