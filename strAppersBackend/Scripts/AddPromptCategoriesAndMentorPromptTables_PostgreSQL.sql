-- Add PromptCategories and MentorPrompt tables for PostgreSQL
-- PromptCategories: categories for organizing system prompt fragments (e.g. Platform Context, General Instructions).
-- MentorPrompt: mentor system prompt fragments scoped by role (null = all roles) and category.
-- Note: PostgreSQL converts unquoted identifiers to lowercase; quoted identifiers preserve case.

-- 1. Create PromptCategories table
CREATE TABLE IF NOT EXISTS "PromptCategories" (
    "CategoryId" SERIAL PRIMARY KEY,
    "Name" VARCHAR(200) NOT NULL,
    "Description" VARCHAR(500) NULL,
    "SortOrder" INTEGER NOT NULL DEFAULT 0
);

COMMENT ON TABLE "PromptCategories" IS 'Categories for organizing system prompt fragments (e.g. Platform Context, General Instructions, Database Rules)';
COMMENT ON COLUMN "PromptCategories"."CategoryId" IS 'Primary key identifier';
COMMENT ON COLUMN "PromptCategories"."Name" IS 'Category display name';
COMMENT ON COLUMN "PromptCategories"."Description" IS 'Optional description';
COMMENT ON COLUMN "PromptCategories"."SortOrder" IS 'Order for display and assembly';

-- 2. Create MentorPrompt table
CREATE TABLE IF NOT EXISTS "MentorPrompt" (
    "Id" SERIAL PRIMARY KEY,
    "RoleId" INTEGER NULL,
    "CategoryId" INTEGER NOT NULL,
    "PromptString" TEXT NOT NULL,
    "SortOrder" INTEGER NOT NULL DEFAULT 0,
    "IsActive" BOOLEAN NOT NULL DEFAULT true,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NULL,
    CONSTRAINT "FK_MentorPrompt_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_MentorPrompt_PromptCategories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "PromptCategories" ("CategoryId") ON DELETE RESTRICT
);

COMMENT ON TABLE "MentorPrompt" IS 'Mentor system prompt fragments. RoleId null = applies to all user roles when building the Mentor prompt';
COMMENT ON COLUMN "MentorPrompt"."Id" IS 'Primary key identifier';
COMMENT ON COLUMN "MentorPrompt"."RoleId" IS 'FK to Roles. Null = fragment applies to all roles';
COMMENT ON COLUMN "MentorPrompt"."CategoryId" IS 'FK to PromptCategories';
COMMENT ON COLUMN "MentorPrompt"."PromptString" IS 'The prompt text content';
COMMENT ON COLUMN "MentorPrompt"."SortOrder" IS 'Order within category/assembly';
COMMENT ON COLUMN "MentorPrompt"."IsActive" IS 'When false, fragment is excluded from assembly';
COMMENT ON COLUMN "MentorPrompt"."UpdatedAt" IS 'Last updated timestamp';

-- Indexes for MentorPrompt
CREATE INDEX IF NOT EXISTS "IX_MentorPrompt_RoleId"
    ON "MentorPrompt" ("RoleId");

CREATE INDEX IF NOT EXISTS "IX_MentorPrompt_CategoryId"
    ON "MentorPrompt" ("CategoryId");
