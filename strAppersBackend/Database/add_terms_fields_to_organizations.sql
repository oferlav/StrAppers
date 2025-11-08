-- Adds terms of use related columns to the Organizations table

BEGIN;

ALTER TABLE "Organizations"
    ADD COLUMN IF NOT EXISTS "TermsUse" TEXT;

ALTER TABLE "Organizations"
    ADD COLUMN IF NOT EXISTS "TermsAccepted" BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE "Organizations"
    ADD COLUMN IF NOT EXISTS "TermsAcceptedAt" TIMESTAMP WITH TIME ZONE;

-- Ensure existing rows have a deterministic value for the new boolean column
UPDATE "Organizations"
SET "TermsAccepted" = COALESCE("TermsAccepted", FALSE)
WHERE "TermsAccepted" IS NULL;

COMMIT;
