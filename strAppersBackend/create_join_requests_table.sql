-- Create JoinRequests table
CREATE TABLE IF NOT EXISTS "JoinRequests" (
    "Id" SERIAL PRIMARY KEY,
    "ChannelName" VARCHAR(100) NOT NULL,
    "ChannelId" VARCHAR(50) NOT NULL,
    "StudentId" INTEGER NOT NULL,
    "StudentEmail" VARCHAR(255) NOT NULL,
    "StudentFirstName" VARCHAR(100),
    "StudentLastName" VARCHAR(100),
    "ProjectId" INTEGER NOT NULL,
    "ProjectTitle" VARCHAR(200),
    "JoinDate" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "Added" BOOLEAN NOT NULL DEFAULT FALSE,
    "AddedDate" TIMESTAMP WITH TIME ZONE,
    "Notes" VARCHAR(500),
    "ErrorMessage" VARCHAR(1000),
    
    -- Foreign key constraints
    CONSTRAINT "FK_JoinRequests_Projects_ProjectId" 
        FOREIGN KEY ("ProjectId") REFERENCES "Projects"("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_JoinRequests_Students_StudentId" 
        FOREIGN KEY ("StudentId") REFERENCES "Students"("Id") ON DELETE SET NULL
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS "IX_JoinRequests_Added" ON "JoinRequests"("Added");
CREATE INDEX IF NOT EXISTS "IX_JoinRequests_ChannelId" ON "JoinRequests"("ChannelId");
CREATE INDEX IF NOT EXISTS "IX_JoinRequests_JoinDate" ON "JoinRequests"("JoinDate");
CREATE INDEX IF NOT EXISTS "IX_JoinRequests_ProjectId" ON "JoinRequests"("ProjectId");
CREATE INDEX IF NOT EXISTS "IX_JoinRequests_StudentEmail" ON "JoinRequests"("StudentEmail");
CREATE INDEX IF NOT EXISTS "IX_JoinRequests_StudentId" ON "JoinRequests"("StudentId");
