-- Add MarketingImages table for PostgreSQL
-- This script creates the MarketingImages table to store base64-encoded images for the company website
-- Note: PostgreSQL converts unquoted identifiers to lowercase

-- Create MarketingImages table
CREATE TABLE IF NOT EXISTS "MarketingImages" (
    "Id" SERIAL PRIMARY KEY,
    "Base64" TEXT NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Add comments for documentation
COMMENT ON TABLE "MarketingImages" IS 'Stores marketing images for the company website';
COMMENT ON COLUMN "MarketingImages"."Id" IS 'Primary key identifier';
COMMENT ON COLUMN "MarketingImages"."Base64" IS 'Base64-encoded image data';
COMMENT ON COLUMN "MarketingImages"."CreatedAt" IS 'Image creation timestamp';

-- Create index for efficient querying by Id (primary key already has index, but explicit for clarity)
-- Note: Primary key automatically creates an index, but we can add additional indexes if needed
