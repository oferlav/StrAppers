-- Apply after deploying model changes; aligns with EF migration InstituteProjects_IsBuiltIn.

ALTER TABLE "InstituteProjects"
    ADD COLUMN IF NOT EXISTS "IsBuiltIn" boolean NOT NULL DEFAULT FALSE;

-- Optional backfill: mark direct built-in mirrors as built-in when title matches the base built-in title.
UPDATE "InstituteProjects" ip
SET "IsBuiltIn" = TRUE
FROM "Projects" p
WHERE ip."BaseProjectId" = p."Id"
  AND p."InstituteId" IS NULL
  AND p."IsAvailable" = TRUE
  AND lower(trim(ip."Title")) = lower(trim(p."Title"));
