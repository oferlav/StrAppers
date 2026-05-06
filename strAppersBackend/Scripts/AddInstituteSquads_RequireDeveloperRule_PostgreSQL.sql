-- Per-template bundle / developer-role rule persisted on InstituteSquads.
-- Mirrors EF migration 20260504064823_InstituteSquads_RequireDeveloperRule.

ALTER TABLE "InstituteSquads"
    ADD COLUMN IF NOT EXISTS "RequireDeveloperRule" BOOLEAN NOT NULL DEFAULT FALSE;
