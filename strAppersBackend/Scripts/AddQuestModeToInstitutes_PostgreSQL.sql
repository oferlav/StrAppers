-- AddQuestModeToInstitutes
-- Adds the QuestMode feature flag to the Institutes table.
-- Run this manually on the target database.

ALTER TABLE "Institutes"
  ADD COLUMN IF NOT EXISTS "QuestMode" boolean NOT NULL DEFAULT false;
