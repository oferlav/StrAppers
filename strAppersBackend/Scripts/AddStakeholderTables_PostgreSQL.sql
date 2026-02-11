-- Create StakeholderCategory, StakeholderStatus, and Stakeholders tables for PostgreSQL
-- Run in pgAdmin or psql. Uses quoted identifiers to match EF Core.

-- 1. StakeholderCategories
CREATE TABLE IF NOT EXISTS "StakeholderCategories" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(200) NOT NULL
);
COMMENT ON TABLE "StakeholderCategories" IS 'Lookup table for stakeholder category (e.g. type of stakeholder)';
COMMENT ON COLUMN "StakeholderCategories"."Id" IS 'Primary key';
COMMENT ON COLUMN "StakeholderCategories"."Name" IS 'Category name';

-- 2. StakeholderStatuses
CREATE TABLE IF NOT EXISTS "StakeholderStatuses" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(200) NOT NULL
);
COMMENT ON TABLE "StakeholderStatuses" IS 'Lookup table for stakeholder status';
COMMENT ON COLUMN "StakeholderStatuses"."Id" IS 'Primary key';
COMMENT ON COLUMN "StakeholderStatuses"."Name" IS 'Status name';

-- 3. Stakeholders
CREATE TABLE IF NOT EXISTS "Stakeholders" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(500) NOT NULL,
    "CategoryId" INTEGER NOT NULL,
    "StatusId" INTEGER NOT NULL,
    "V1AlignmentScore" INTEGER NOT NULL DEFAULT 0,
    "Delta" TEXT NULL,
    CONSTRAINT "FK_Stakeholders_StakeholderCategories_CategoryId"
        FOREIGN KEY ("CategoryId") REFERENCES "StakeholderCategories" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Stakeholders_StakeholderStatuses_StatusId"
        FOREIGN KEY ("StatusId") REFERENCES "StakeholderStatuses" ("Id") ON DELETE RESTRICT
);
COMMENT ON TABLE "Stakeholders" IS 'Stakeholders with category, status, alignment score and delta notes';
COMMENT ON COLUMN "Stakeholders"."Id" IS 'Primary key';
COMMENT ON COLUMN "Stakeholders"."Name" IS 'Stakeholder name';
COMMENT ON COLUMN "Stakeholders"."CategoryId" IS 'FK to StakeholderCategories.Id';
COMMENT ON COLUMN "Stakeholders"."StatusId" IS 'FK to StakeholderStatuses.Id';
COMMENT ON COLUMN "Stakeholders"."V1AlignmentScore" IS 'V1 alignment score (integer)';
COMMENT ON COLUMN "Stakeholders"."Delta" IS 'Delta / notes (free text)';

CREATE INDEX IF NOT EXISTS "IX_Stakeholders_CategoryId" ON "Stakeholders" ("CategoryId");
CREATE INDEX IF NOT EXISTS "IX_Stakeholders_StatusId" ON "Stakeholders" ("StatusId");
