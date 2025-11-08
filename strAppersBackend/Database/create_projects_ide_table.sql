-- Create ProjectsIDE table for IDE generation tracking
-- This script creates the ProjectsIDE table to track IDE code generation chunks

CREATE TABLE IF NOT EXISTS "ProjectsIDE" (
    "id" SERIAL PRIMARY KEY,
    "project_id" INT NOT NULL,
    
    -- Chunk identification
    "chunk_id" VARCHAR(100) NOT NULL,
    "chunk_type" VARCHAR(50) NOT NULL,
    "chunk_description" TEXT,
    "generation_order" INT NOT NULL,
    
    -- Generation status
    "status" VARCHAR(50) DEFAULT 'pending',
    
    -- Generated content stored as JSON array of files
    "files_json" JSONB,
    "files_count" INT DEFAULT 0,
    
    -- Metadata
    "dependencies" TEXT[],
    "error_message" TEXT,
    "tokens_used" INT,
    "generation_time_ms" INT,
    
    -- Timestamps
    "created_at" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "generated_at" TIMESTAMP,
    
    FOREIGN KEY ("project_id") REFERENCES "Projects"("Id") ON DELETE CASCADE,
    CONSTRAINT "unique_project_chunk" UNIQUE("project_id", "chunk_id")
);

CREATE INDEX IF NOT EXISTS "idx_projects_ide_project_id" ON "ProjectsIDE"("project_id");
CREATE INDEX IF NOT EXISTS "idx_projects_ide_status" ON "ProjectsIDE"("status");
CREATE INDEX IF NOT EXISTS "idx_projects_ide_generation_order" ON "ProjectsIDE"("generation_order");

-- Add to Projects table
ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "deployment_manifest" TEXT;
ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "ide_generation_status" VARCHAR(50) DEFAULT 'not_started';
ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "total_chunks" INT DEFAULT 0;
ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "completed_chunks" INT DEFAULT 0;
ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "mock_records_count" INT DEFAULT 10;


