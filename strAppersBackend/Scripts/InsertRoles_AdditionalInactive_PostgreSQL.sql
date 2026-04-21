-- Insert inactive catalog roles (Type = Default / RoleTypes.Id = 0).
-- PostgreSQL. Descriptions use dollar-quoting so commas inside text cannot break parsers.
-- Requires FK "Roles"."Type" -> "RoleTypes"."Id".

BEGIN;

INSERT INTO "RoleTypes" ("Id", "Description")
VALUES (0, 'Default')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Roles" ("Name", "Description", "Category", "Type", "IsActive", "CreatedAt")
SELECT 'Data Analyst/Scientist',
       $role_desc_1$Data exploration, modeling, and analytics support for student projects.$role_desc_1$,
       'Technical',
       0,
       false,
       CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM "Roles" r WHERE r."Name" = 'Data Analyst/Scientist');

INSERT INTO "Roles" ("Name", "Description", "Category", "Type", "IsActive", "CreatedAt")
SELECT 'QA Engineer',
       $role_desc_2$Testing, quality gates, and defect tracking for delivery teams.$role_desc_2$,
       'Technical',
       0,
       false,
       CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM "Roles" r WHERE r."Name" = 'QA Engineer');

INSERT INTO "Roles" ("Name", "Description", "Category", "Type", "IsActive", "CreatedAt")
SELECT 'Project Manager',
       $role_desc_3$Plans, coordinates timelines, and keeps squads aligned with goals.$role_desc_3$,
       'Management',
       0,
       false,
       CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM "Roles" r WHERE r."Name" = 'Project Manager');

INSERT INTO "Roles" ("Name", "Description", "Category", "Type", "IsActive", "CreatedAt")
SELECT 'AI Engineer',
       $role_desc_4$Builds and integrates ML/AI features with the rest of the stack.$role_desc_4$,
       'Technical',
       0,
       false,
       CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM "Roles" r WHERE r."Name" = 'AI Engineer');

INSERT INTO "Roles" ("Name", "Description", "Category", "Type", "IsActive", "CreatedAt")
SELECT 'DevOps Engineer',
       $role_desc_5$CI/CD, environments, observability, and release automation.$role_desc_5$,
       'Technical',
       0,
       false,
       CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM "Roles" r WHERE r."Name" = 'DevOps Engineer');

INSERT INTO "Roles" ("Name", "Description", "Category", "Type", "IsActive", "CreatedAt")
SELECT 'Product Owner',
       $role_desc_6$Owns backlog priorities and bridges stakeholders with the delivery team.$role_desc_6$,
       'Management',
       0,
       false,
       CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM "Roles" r WHERE r."Name" = 'Product Owner');

COMMIT;
