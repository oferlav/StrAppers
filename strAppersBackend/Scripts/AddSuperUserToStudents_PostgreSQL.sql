-- Add SuperUser column to Students table (boolean, default false)
-- Run this on PostgreSQL if not using EF migrations

ALTER TABLE "Students"
ADD COLUMN IF NOT EXISTS "SuperUser" BOOLEAN NOT NULL DEFAULT false;

COMMENT ON COLUMN "Students"."SuperUser" IS 'Super-user flag; default false';
