-- Adds nullable-safe admin flag for teachers.
-- Safe to run multiple times.

ALTER TABLE "Teachers"
    ADD COLUMN IF NOT EXISTS "IsAdmin" boolean NOT NULL DEFAULT false;
