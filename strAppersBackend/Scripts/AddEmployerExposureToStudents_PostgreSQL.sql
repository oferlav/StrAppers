-- Add EmployerExposure column to Students table for PostgreSQL
-- Type: boolean, default true. When true, student is exposed to employers (e.g. in candidate lists).

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Students'
          AND column_name = 'EmployerExposure'
    ) THEN
        ALTER TABLE "Students"
        ADD COLUMN "EmployerExposure" boolean NOT NULL DEFAULT true;

        COMMENT ON COLUMN "Students"."EmployerExposure" IS 'When true (default), student is exposed to employers (e.g. in candidate lists).';
    END IF;
END $$;
