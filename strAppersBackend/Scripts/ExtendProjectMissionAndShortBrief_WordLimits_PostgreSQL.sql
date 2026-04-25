-- Widen project header text columns to support 45 / 150 word limits (PostgreSQL hand-run if needed)
ALTER TABLE "Projects" ALTER COLUMN "Mission" TYPE character varying(2000);
ALTER TABLE "Projects" ALTER COLUMN "ShortBrief" TYPE character varying(2000);
