-- ALTER TABLE script to add ProgrammingLanguageId field to Students table
-- This script adds the ProgrammingLanguageId column and creates a foreign key constraint

-- Add ProgrammingLanguageId column to Students table
ALTER TABLE "Students"
ADD COLUMN IF NOT EXISTS "ProgrammingLanguageId" INTEGER;

-- Create foreign key constraint
DO $$
BEGIN
    -- Check if the constraint doesn't already exist
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.table_constraints 
        WHERE constraint_name = 'FK_Students_ProgrammingLanguages_ProgrammingLanguageId'
        AND table_name = 'Students'
    ) THEN
        ALTER TABLE "Students"
        ADD CONSTRAINT "FK_Students_ProgrammingLanguages_ProgrammingLanguageId"
        FOREIGN KEY ("ProgrammingLanguageId")
        REFERENCES "ProgrammingLanguages"("Id")
        ON DELETE SET NULL
        ON UPDATE CASCADE;
    END IF;
END $$;

-- Create index for better performance
CREATE INDEX IF NOT EXISTS "IX_Students_ProgrammingLanguageId" 
ON "Students"("ProgrammingLanguageId");

-- Verify the column was added successfully
SELECT 
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'Students'
AND column_name = 'ProgrammingLanguageId';





