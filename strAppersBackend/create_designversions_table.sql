-- Create DesignVersions table for versioning design documents
-- This script creates the DesignVersions table to track design document versions

CREATE TABLE "DesignVersions" (
    "Id" SERIAL PRIMARY KEY,
    "ProjectId" INTEGER NOT NULL,
    "VersionNumber" INTEGER NOT NULL,
    "DesignDocument" TEXT NOT NULL,
    "DesignDocumentPdf" BYTEA,
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "CreatedBy" VARCHAR(255),
    "IsActive" BOOLEAN DEFAULT TRUE,
    FOREIGN KEY ("ProjectId") REFERENCES "Projects"("Id") ON DELETE CASCADE
);

-- Create indexes for better performance
CREATE INDEX "IX_DesignVersions_ProjectId" ON "DesignVersions"("ProjectId");
CREATE INDEX "IX_DesignVersions_VersionNumber" ON "DesignVersions"("VersionNumber");
CREATE INDEX "IX_DesignVersions_IsActive" ON "DesignVersions"("IsActive");

-- Verify the table was created
SELECT table_name, column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'DesignVersions'
ORDER BY ordinal_position;
