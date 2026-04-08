-- Add CreatedAt to Stakeholders (PostgreSQL)
-- Nullable: existing rows keep NULL until backfilled or new API writes set UtcNow.

ALTER TABLE "Stakeholders"
    ADD COLUMN IF NOT EXISTS "CreatedAt" TIMESTAMPTZ NULL;

COMMENT ON COLUMN "Stakeholders"."CreatedAt" IS 'UTC timestamp when the stakeholder row was created; used to scope CRM review to sprint windows.';
