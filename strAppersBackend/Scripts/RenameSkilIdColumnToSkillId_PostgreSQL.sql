-- Rename typo column "SkilId" → "SkillId" on Roles and InstituteRoles (PostgreSQL).
-- Run once if you already applied an older migration/script that created "SkilId".
-- Safe no-op when "SkilId" does not exist.
-- Prerequisite: table "Skills" must exist (run AddSkills script or migration first).

BEGIN;

-- Roles
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'Roles'
          AND column_name = 'SkilId'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'Roles'
          AND column_name = 'SkillId'
    ) THEN
        ALTER TABLE "Roles" RENAME COLUMN "SkilId" TO "SkillId";
    END IF;
END $$;

-- Drop old FK/index names if present (PostgreSQL keeps constraints on rename in some setups;
-- here we recreate with standard EF names.)

ALTER TABLE "Roles" DROP CONSTRAINT IF EXISTS "FK_Roles_Skills_SkilId";
DROP INDEX IF EXISTS "IX_Roles_SkilId";

CREATE INDEX IF NOT EXISTS "IX_Roles_SkillId" ON "Roles" ("SkillId");

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Roles_Skills_SkillId'
    ) THEN
        ALTER TABLE "Roles"
            ADD CONSTRAINT "FK_Roles_Skills_SkillId"
            FOREIGN KEY ("SkillId") REFERENCES "Skills" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

-- InstituteRoles
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'InstituteRoles'
          AND column_name = 'SkilId'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'InstituteRoles'
          AND column_name = 'SkillId'
    ) THEN
        ALTER TABLE "InstituteRoles" RENAME COLUMN "SkilId" TO "SkillId";
    END IF;
END $$;

ALTER TABLE "InstituteRoles" DROP CONSTRAINT IF EXISTS "FK_InstituteRoles_Skills_SkilId";
DROP INDEX IF EXISTS "IX_InstituteRoles_SkilId";

CREATE INDEX IF NOT EXISTS "IX_InstituteRoles_SkillId" ON "InstituteRoles" ("SkillId");

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_InstituteRoles_Skills_SkillId'
    ) THEN
        ALTER TABLE "InstituteRoles"
            ADD CONSTRAINT "FK_InstituteRoles_Skills_SkillId"
            FOREIGN KEY ("SkillId") REFERENCES "Skills" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

COMMIT;
