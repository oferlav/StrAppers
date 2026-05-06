-- Mirrors Projects.CourseName semantics for institute-owned rows (varchar 100, nullable).
-- Apply after deploying model changes; align with EF migration InstituteProjects_BuiltInCourseName.

ALTER TABLE "InstituteProjects" ADD COLUMN IF NOT EXISTS "BuiltInCourseName" character varying(100) NULL;

-- Optional: backfill from the linked catalog project id (run once if you want existing rows populated).
-- UPDATE "InstituteProjects" ip
-- SET "BuiltInCourseName" = LEFT(TRIM(p."CourseName"), 100)
-- FROM "Projects" p
-- WHERE ip."BaseProjectId" = p."Id"
--   AND (ip."BuiltInCourseName" IS NULL OR TRIM(COALESCE(ip."BuiltInCourseName", '')) = '')
--   AND p."CourseName" IS NOT NULL
--   AND TRIM(p."CourseName") <> '';
