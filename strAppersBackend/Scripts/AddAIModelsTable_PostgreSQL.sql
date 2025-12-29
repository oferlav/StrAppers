-- Add AIModels table for PostgreSQL
-- This script creates the AIModels table and inserts records for existing AI models
-- Note: PostgreSQL converts unquoted identifiers to lowercase

-- Create AIModels table
CREATE TABLE IF NOT EXISTS "AIModels" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL,
    "Provider" VARCHAR(50) NOT NULL,
    "BaseUrl" VARCHAR(500) NULL,
    "ApiVersion" VARCHAR(50) NULL,
    "MaxTokens" INTEGER NULL,
    "DefaultTemperature" DOUBLE PRECISION NULL,
    "Description" VARCHAR(1000) NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NULL
);

-- Add comments for documentation
COMMENT ON TABLE "AIModels" IS 'AI models available in the system (only models with API keys)';
COMMENT ON COLUMN "AIModels"."Id" IS 'Primary key identifier';
COMMENT ON COLUMN "AIModels"."Name" IS 'Model name (e.g., gpt-4o-mini, claude-sonnet-4-5-20250929)';
COMMENT ON COLUMN "AIModels"."Provider" IS 'AI provider (e.g., OpenAI, Anthropic)';
COMMENT ON COLUMN "AIModels"."BaseUrl" IS 'API base URL for the model';
COMMENT ON COLUMN "AIModels"."ApiVersion" IS 'API version (e.g., 2023-06-01 for Anthropic)';
COMMENT ON COLUMN "AIModels"."MaxTokens" IS 'Default maximum tokens for this model';
COMMENT ON COLUMN "AIModels"."DefaultTemperature" IS 'Default temperature setting for this model';
COMMENT ON COLUMN "AIModels"."Description" IS 'Description of the model';
COMMENT ON COLUMN "AIModels"."IsActive" IS 'Whether this model is currently available';
COMMENT ON COLUMN "AIModels"."CreatedAt" IS 'Record creation timestamp';
COMMENT ON COLUMN "AIModels"."UpdatedAt" IS 'Record update timestamp';

-- Insert existing AI models (only insert if they don't already exist)
-- OpenAI GPT-4o Mini
INSERT INTO "AIModels" ("Id", "Name", "Provider", "BaseUrl", "ApiVersion", "MaxTokens", "DefaultTemperature", "Description", "IsActive", "CreatedAt")
SELECT 1, 'gpt-4o-mini', 'OpenAI', 'https://api.openai.com/v1', NULL, 16384, 0.2, 'OpenAI GPT-4o Mini model - fast and cost-effective', TRUE, CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM "AIModels" WHERE "Id" = 1);

-- Anthropic Claude Sonnet 4.5
INSERT INTO "AIModels" ("Id", "Name", "Provider", "BaseUrl", "ApiVersion", "MaxTokens", "DefaultTemperature", "Description", "IsActive", "CreatedAt")
SELECT 2, 'claude-sonnet-4-5-20250929', 'Anthropic', 'https://api.anthropic.com/v1', '2023-06-01', 200000, 0.3, 'Anthropic Claude Sonnet 4.5 model - powerful for complex tasks', TRUE, CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM "AIModels" WHERE "Id" = 2);

-- Create index on Provider for faster lookups
CREATE INDEX IF NOT EXISTS "IX_AIModels_Provider" ON "AIModels" ("Provider");

-- Create index on IsActive for filtering active models
CREATE INDEX IF NOT EXISTS "IX_AIModels_IsActive" ON "AIModels" ("IsActive");

-- Create unique index on Name to prevent duplicate model names
CREATE UNIQUE INDEX IF NOT EXISTS "IX_AIModels_Name" ON "AIModels" ("Name");

