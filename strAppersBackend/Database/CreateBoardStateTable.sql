-- =============================================
-- BoardState Table Creation Script
-- =============================================
-- This script creates the BoardState table to track board states
-- from various sources (GitHub, Railway)
-- =============================================

-- Create BoardStates table
CREATE TABLE IF NOT EXISTS "BoardStates" (
    "Id" SERIAL PRIMARY KEY,
    "BoardId" VARCHAR(50) NOT NULL,
    "Source" VARCHAR(50) NOT NULL,
    "Webhook" BOOLEAN,
    
    -- Railway-specific fields (nullable)
    "ServiceName" VARCHAR(255),
    "ErrorMessage" TEXT,
    "File" VARCHAR(500),
    "Line" INTEGER,
    "StackTrace" TEXT,
    "RequestUrl" VARCHAR(500),
    "RequestMethod" VARCHAR(10),
    "Timestamp" TIMESTAMP WITH TIME ZONE,
    "LastBuildStatus" VARCHAR(50),
    "LastBuildOutput" TEXT,
    "LatestErrorSummary" TEXT,
    
    -- GitHub-specific fields (nullable)
    "SprintNumber" INTEGER,
    "BranchName" VARCHAR(255),
    "BranchUrl" VARCHAR(500),
    "LatestCommitId" VARCHAR(100),
    "LatestCommitDescription" TEXT,
    "LatestCommitDate" TIMESTAMP WITH TIME ZONE,
    "LastMergeDate" TIMESTAMP WITH TIME ZONE,
    "LatestEvent" VARCHAR(100),
    "PRStatus" VARCHAR(50),
    "BranchStatus" VARCHAR(50),
    
    -- Timestamps
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Foreign key constraint
    CONSTRAINT "FK_BoardStates_ProjectBoards_BoardId" 
        FOREIGN KEY ("BoardId") 
        REFERENCES "ProjectBoards"("BoardId") 
        ON DELETE CASCADE
);

-- Create indexes for better query performance
CREATE INDEX IF NOT EXISTS "IX_BoardStates_BoardId" ON "BoardStates"("BoardId");
CREATE INDEX IF NOT EXISTS "IX_BoardStates_Source" ON "BoardStates"("Source");
-- Unique constraint to prevent duplicate records (BoardId + Source must be unique)
CREATE UNIQUE INDEX IF NOT EXISTS "IX_BoardStates_BoardId_Source" ON "BoardStates"("BoardId", "Source");
CREATE INDEX IF NOT EXISTS "IX_BoardStates_CreatedAt" ON "BoardStates"("CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_BoardStates_UpdatedAt" ON "BoardStates"("UpdatedAt");
CREATE INDEX IF NOT EXISTS "IX_BoardStates_LastBuildStatus" ON "BoardStates"("LastBuildStatus");
CREATE INDEX IF NOT EXISTS "IX_BoardStates_LatestCommitId" ON "BoardStates"("LatestCommitId");
CREATE INDEX IF NOT EXISTS "IX_BoardStates_BranchName" ON "BoardStates"("BranchName");
CREATE INDEX IF NOT EXISTS "IX_BoardStates_PRStatus" ON "BoardStates"("PRStatus");
CREATE INDEX IF NOT EXISTS "IX_BoardStates_BranchStatus" ON "BoardStates"("BranchStatus");

-- Add comment to table
COMMENT ON TABLE "BoardStates" IS 'Tracks board state information from various sources (GitHub, Railway)';
COMMENT ON COLUMN "BoardStates"."Source" IS 'Source of the state: Github or Railway';
COMMENT ON COLUMN "BoardStates"."Webhook" IS 'Indicates if this state was set by a webhook (true) or API call (false/null)';
COMMENT ON COLUMN "BoardStates"."LastBuildStatus" IS 'Railway build status: SUCCESS or FAILED';
COMMENT ON COLUMN "BoardStates"."PRStatus" IS 'GitHub PR status: None, Requested, or Approved';
COMMENT ON COLUMN "BoardStates"."BranchStatus" IS 'GitHub branch status: Active or Merged';
