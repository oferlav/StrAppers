-- Add IsAvailable column to Projects table for PostgreSQL
-- This script adds the IsAvailable field to the existing Projects table
-- Note: PostgreSQL converts unquoted identifiers to lowercase

-- Add IsAvailable column (BIT type in PostgreSQL is BOOLEAN)
DO $$ 
BEGIN
    -- Add IsAvailable column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Projects' 
        AND column_name = 'IsAvailable'
    ) THEN
        ALTER TABLE "Projects" 
        ADD COLUMN "IsAvailable" BOOLEAN NOT NULL DEFAULT TRUE;
        
        -- Add comment for documentation
        COMMENT ON COLUMN "Projects"."IsAvailable" IS 'Whether the project is available for allocation (default: TRUE)';
    END IF;
END $$;

-- Create index on IsAvailable for better performance
CREATE INDEX IF NOT EXISTS "IX_Projects_IsAvailable" ON "Projects"("IsAvailable");

-- Add comment for documentation
COMMENT ON COLUMN "Projects"."IsAvailable" IS 'Whether the project is available for allocation (default: TRUE)';





