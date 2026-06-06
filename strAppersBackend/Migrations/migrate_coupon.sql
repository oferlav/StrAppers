-- =============================================================================
-- Coupon Migration: Add Coupon column to InstituteTemplates
--
-- InstituteTemplates.Coupon: auto-generated when roles config is saved
--   Formula: Institute.Name (no spaces) + "-" + SquadId
--
-- Note: InstituteProjects.Coupon already exists (migration 20260601140000).
--
-- Database: PostgreSQL
-- Safe to run: idempotent (IF NOT EXISTS)
-- =============================================================================

ALTER TABLE "InstituteTemplates"
    ADD COLUMN IF NOT EXISTS "Coupon" VARCHAR(100);
