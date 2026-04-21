-- RoleTypes lookup + FK Roles.Type -> RoleTypes.Id (matches migration 20260419120000_AddRoleTypesAndRolesTypeFk).
-- PostgreSQL. Safe to run once on a DB that already has "Roles"."Type" populated.

BEGIN;

CREATE TABLE IF NOT EXISTS "RoleTypes" (
    "Id" integer NOT NULL,
    "Description" character varying(200) NOT NULL,
    CONSTRAINT "PK_RoleTypes" PRIMARY KEY ("Id")
);

INSERT INTO "RoleTypes" ("Id", "Description") VALUES
    (0, 'Default'),
    (1, 'bundle'),
    (2, 'bundle'),
    (3, 'Required'),
    (4, 'leadership')
ON CONFLICT ("Id") DO UPDATE SET "Description" = EXCLUDED."Description";

-- Ensure every row can satisfy the FK before adding it.
UPDATE "Roles" r
SET "Type" = 0
WHERE NOT EXISTS (
    SELECT 1 FROM "RoleTypes" rt WHERE rt."Id" = r."Type"
);

CREATE INDEX IF NOT EXISTS "IX_Roles_Type" ON "Roles" ("Type");

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Roles_RoleTypes_Type'
    ) THEN
        ALTER TABLE "Roles"
            ADD CONSTRAINT "FK_Roles_RoleTypes_Type"
            FOREIGN KEY ("Type") REFERENCES "RoleTypes" ("Id") ON DELETE RESTRICT;
    END IF;
END $$;

-- If you applied this script manually (not `dotnet ef database update`), register the migration so EF stays in sync:
-- INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
-- VALUES ('20260419120000_AddRoleTypesAndRolesTypeFk', '8.0.0')
-- ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
