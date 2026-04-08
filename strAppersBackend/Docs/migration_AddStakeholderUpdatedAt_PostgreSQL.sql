-- Add UpdatedAt to Stakeholders (PostgreSQL)
-- Nullable: existing rows keep NULL until updated via API (CRMController sets UtcNow on create/update).

ALTER TABLE "Stakeholders"
    ADD COLUMN IF NOT EXISTS "UpdatedAt" TIMESTAMPTZ NULL;

COMMENT ON COLUMN "Stakeholders"."UpdatedAt" IS 'UTC timestamp when the stakeholder row was last updated; CRM review includes rows where CreatedAt or UpdatedAt falls in the sprint window.';
