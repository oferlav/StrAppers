-- Run on PostgreSQL when catalog activation fails with:
--   value too long for type character varying(1000)  -> Description
--   value too long for type character varying(2000)  -> Mission, ShortBrief, or SystemDesignFormatted

ALTER TABLE "InstituteProjects" ALTER COLUMN "Description" TYPE text;
ALTER TABLE "InstituteProjects" ALTER COLUMN "Mission" TYPE text;
ALTER TABLE "InstituteProjects" ALTER COLUMN "ShortBrief" TYPE text;
ALTER TABLE "InstituteProjects" ALTER COLUMN "SystemDesignFormatted" TYPE text;

-- Optional: if catalog rows in "Projects" already exceed varchar limits (same activate source data):
-- ALTER TABLE "Projects" ALTER COLUMN "Description" TYPE text;
-- ALTER TABLE "Projects" ALTER COLUMN "Mission" TYPE text;
-- ALTER TABLE "Projects" ALTER COLUMN "ShortBrief" TYPE text;
-- ALTER TABLE "Projects" ALTER COLUMN "SystemDesignFormatted" TYPE text;
