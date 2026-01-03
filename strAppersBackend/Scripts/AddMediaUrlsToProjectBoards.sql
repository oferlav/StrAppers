-- Add media URL columns to ProjectBoards table
-- This script adds Facebook, Presentation, LinkedIn, Instagram, and YouTube URLs

-- Add FacebookUrl column
ALTER TABLE "ProjectBoards" ADD COLUMN IF NOT EXISTS "FacebookUrl" VARCHAR(1000);

-- Add PresentationUrl column
ALTER TABLE "ProjectBoards" ADD COLUMN IF NOT EXISTS "PresentationUrl" VARCHAR(1000);

-- Add LinkedInUrl column
ALTER TABLE "ProjectBoards" ADD COLUMN IF NOT EXISTS "LinkedInUrl" VARCHAR(1000);

-- Add InstagramUrl column
ALTER TABLE "ProjectBoards" ADD COLUMN IF NOT EXISTS "InstagramUrl" VARCHAR(1000);

-- Add YoutubeUrl column
ALTER TABLE "ProjectBoards" ADD COLUMN IF NOT EXISTS "YoutubeUrl" VARCHAR(1000);

-- Add comments for documentation
COMMENT ON COLUMN "ProjectBoards"."FacebookUrl" IS 'Facebook URL for the project';
COMMENT ON COLUMN "ProjectBoards"."PresentationUrl" IS 'Presentation URL for the project';
COMMENT ON COLUMN "ProjectBoards"."LinkedInUrl" IS 'LinkedIn URL for the project';
COMMENT ON COLUMN "ProjectBoards"."InstagramUrl" IS 'Instagram URL for the project';
COMMENT ON COLUMN "ProjectBoards"."YoutubeUrl" IS 'YouTube URL for the project';


