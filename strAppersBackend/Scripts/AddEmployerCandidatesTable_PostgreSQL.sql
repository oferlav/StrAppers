-- Script to create EmployerCandidates table
-- This table stores the relationship between employers and their selected student candidates for filtering purposes

CREATE TABLE IF NOT EXISTS "EmployerCandidates" (
    "Id" SERIAL PRIMARY KEY,
    "EmployerId" INTEGER NOT NULL,
    "StudentId" INTEGER NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Unique constraint to prevent duplicate employer-student pairs
    CONSTRAINT "UQ_EmployerCandidates_EmployerId_StudentId" UNIQUE ("EmployerId", "StudentId"),
    
    -- Foreign key constraints
    CONSTRAINT "FK_EmployerCandidates_Employers_EmployerId" 
        FOREIGN KEY ("EmployerId") REFERENCES "Employers"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_EmployerCandidates_Students_StudentId" 
        FOREIGN KEY ("StudentId") REFERENCES "Students"("Id") ON DELETE CASCADE
);

-- Create indexes for query performance
CREATE INDEX IF NOT EXISTS "IX_EmployerCandidates_EmployerId" ON "EmployerCandidates"("EmployerId");
CREATE INDEX IF NOT EXISTS "IX_EmployerCandidates_StudentId" ON "EmployerCandidates"("StudentId");
CREATE INDEX IF NOT EXISTS "IX_EmployerCandidates_CreatedAt" ON "EmployerCandidates"("CreatedAt");

-- Add comments to document the table and columns
COMMENT ON TABLE "EmployerCandidates" IS 'Stores the relationship between employers and their selected student candidates for filtering purposes';
COMMENT ON COLUMN "EmployerCandidates"."Id" IS 'Primary key';
COMMENT ON COLUMN "EmployerCandidates"."EmployerId" IS 'Foreign key to Employers table';
COMMENT ON COLUMN "EmployerCandidates"."StudentId" IS 'Foreign key to Students table';
COMMENT ON COLUMN "EmployerCandidates"."CreatedAt" IS 'Timestamp when the candidate was added to the employer''s list';

-- Verification query (optional - uncomment to check results)
-- SELECT * FROM "EmployerCandidates" LIMIT 10;




