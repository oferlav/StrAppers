-- Create Figma table with proper column names
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

-- Add foreign key constraint to ProjectBoards
ALTER TABLE "Figma" 
ADD CONSTRAINT "FK_Figma_ProjectBoards_BoardId" 
FOREIGN KEY ("BoardId") REFERENCES "ProjectBoards"("BoardId") 
ON DELETE CASCADE;
