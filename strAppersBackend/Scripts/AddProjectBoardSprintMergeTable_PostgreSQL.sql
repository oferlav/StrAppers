-- Add ProjectBoardSprintMerge table for PostgreSQL
-- Tracks merge state per sprint for a project board (when a sprint was merged with SystemBoard).
-- One row per (board, sprint number). DueDate stores the sprint's due date.
-- Note: PostgreSQL converts unquoted identifiers to lowercase; we use quoted names to match EF.

-- Create ProjectBoardSprintMerge table
CREATE TABLE IF NOT EXISTS "ProjectBoardSprintMerge" (
    "ProjectBoardId" VARCHAR(50) NOT NULL,
    "SprintNumber" INTEGER NOT NULL,
    "MergedAt" TIMESTAMP WITH TIME ZONE NULL,
    "ListId" VARCHAR(50) NULL,
    "DueDate" TIMESTAMP WITH TIME ZONE NULL,
    CONSTRAINT "PK_ProjectBoardSprintMerge" PRIMARY KEY ("ProjectBoardId", "SprintNumber"),
    CONSTRAINT "FK_ProjectBoardSprintMerge_ProjectBoards_ProjectBoardId" 
        FOREIGN KEY ("ProjectBoardId") 
        REFERENCES "ProjectBoards" ("BoardId") 
        ON DELETE CASCADE
);

-- Add comments for documentation
COMMENT ON TABLE "ProjectBoardSprintMerge" IS 'Tracks merge state per sprint for a project board (e.g. when a sprint was merged with SystemBoard)';
COMMENT ON COLUMN "ProjectBoardSprintMerge"."ProjectBoardId" IS 'Foreign key to ProjectBoards (Trello board ID)';
COMMENT ON COLUMN "ProjectBoardSprintMerge"."SprintNumber" IS 'Sprint number (1, 2, 3, ...)';
COMMENT ON COLUMN "ProjectBoardSprintMerge"."MergedAt" IS 'When the sprint was last merged (overwrite or AI merge)';
COMMENT ON COLUMN "ProjectBoardSprintMerge"."ListId" IS 'Trello list ID for this sprint (optional)';
COMMENT ON COLUMN "ProjectBoardSprintMerge"."DueDate" IS 'Due date of the sprint';

-- Create indexes for efficient querying
CREATE INDEX IF NOT EXISTS "IX_ProjectBoardSprintMerge_ProjectBoardId" 
    ON "ProjectBoardSprintMerge" ("ProjectBoardId");

CREATE INDEX IF NOT EXISTS "IX_ProjectBoardSprintMerge_SprintNumber" 
    ON "ProjectBoardSprintMerge" ("SprintNumber");
