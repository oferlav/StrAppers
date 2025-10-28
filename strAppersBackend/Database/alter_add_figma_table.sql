-- ALTER TABLE script to add Figma table to existing database
-- This script creates the Figma table with proper column names and constraints

-- Create the Figma table
CREATE TABLE IF NOT EXISTS "Figma" (
    "Id" SERIAL PRIMARY KEY,
    "BoardId" VARCHAR(50) NOT NULL,
    "FigmaAccessToken" VARCHAR(512),
    "FigmaRefreshToken" VARCHAR(512),
    "FigmaTokenExpiry" TIMESTAMP,
    "FigmaUserId" VARCHAR(64),
    "FigmaFileUrl" VARCHAR(1024),
    "FigmaFileKey" VARCHAR(64),
    "FigmaLastSync" TIMESTAMP,
    "CreatedAt" TIMESTAMP DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP DEFAULT NOW()
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS "IX_Figma_BoardId" ON "Figma" ("BoardId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Figma_FigmaFileKey" ON "Figma" ("FigmaFileKey") WHERE "FigmaFileKey" IS NOT NULL;

-- Add foreign key constraint to ProjectBoards (if ProjectBoards table exists)
-- Note: This will fail if ProjectBoards table doesn't exist or has different column names
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ProjectBoards') THEN
        ALTER TABLE "Figma" 
        ADD CONSTRAINT "FK_Figma_ProjectBoards_BoardId" 
        FOREIGN KEY ("BoardId") REFERENCES "ProjectBoards"("BoardId") 
        ON DELETE CASCADE;
    END IF;
END $$;

-- Verify the table was created successfully
SELECT 
    table_name, 
    column_name, 
    data_type, 
    is_nullable
FROM information_schema.columns 
WHERE table_name = 'Figma' 
ORDER BY ordinal_position;
