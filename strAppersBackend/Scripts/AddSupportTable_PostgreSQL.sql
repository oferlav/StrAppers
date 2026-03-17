-- Add Support table for PostgreSQL
-- Support request / ticket log: Id, Name, Email, Description (varchar 500), Priority (int, default 3)

CREATE TABLE IF NOT EXISTS "Support" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(200) NOT NULL,
    "Email" VARCHAR(255) NOT NULL,
    "Description" VARCHAR(500) NULL,
    "Priority" INTEGER NOT NULL DEFAULT 3
);

COMMENT ON TABLE "Support" IS 'Support request / ticket log';
COMMENT ON COLUMN "Support"."Id" IS 'Primary key';
COMMENT ON COLUMN "Support"."Name" IS 'Name of the requester';
COMMENT ON COLUMN "Support"."Email" IS 'Email of the requester';
COMMENT ON COLUMN "Support"."Description" IS 'Support request description (max 500 chars)';
COMMENT ON COLUMN "Support"."Priority" IS 'Priority (default 3)';

CREATE INDEX IF NOT EXISTS "IX_Support_Priority" ON "Support" ("Priority");
CREATE INDEX IF NOT EXISTS "IX_Support_Email" ON "Support" ("Email");
