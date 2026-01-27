-- Add EarlyBirds table for PostgreSQL
-- This script creates the EarlyBirds table to store early bird registrations for the company website
-- Note: PostgreSQL converts unquoted identifiers to lowercase

-- Create EarlyBirds table
CREATE TABLE IF NOT EXISTS "EarlyBirds" (
    "Id" SERIAL PRIMARY KEY,
    "Type" VARCHAR(50) NOT NULL,
    "Name" VARCHAR(200) NOT NULL,
    "Email" VARCHAR(255) NOT NULL,
    "OrgName" VARCHAR(200) NULL,
    "FutureRole" VARCHAR(200) NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Add comments for documentation
COMMENT ON TABLE "EarlyBirds" IS 'Stores early bird registrations for the company website';
COMMENT ON COLUMN "EarlyBirds"."Id" IS 'Primary key identifier';
COMMENT ON COLUMN "EarlyBirds"."Type" IS 'Registration type: "Junior" or "Employer"';
COMMENT ON COLUMN "EarlyBirds"."Name" IS 'Full name of the registrant';
COMMENT ON COLUMN "EarlyBirds"."Email" IS 'Email address of the registrant';
COMMENT ON COLUMN "EarlyBirds"."OrgName" IS 'Organization name (optional)';
COMMENT ON COLUMN "EarlyBirds"."FutureRole" IS 'Future role or position (optional)';
COMMENT ON COLUMN "EarlyBirds"."CreatedAt" IS 'Registration timestamp';

-- Create index for efficient querying by email
CREATE INDEX IF NOT EXISTS "IX_EarlyBirds_Email" 
    ON "EarlyBirds" ("Email");

-- Create index for efficient querying by type
CREATE INDEX IF NOT EXISTS "IX_EarlyBirds_Type" 
    ON "EarlyBirds" ("Type");
