-- PostgreSQL: add SprintNumber to Resources (nullable int).
-- Run once on environments that apply schema manually instead of `dotnet ef database update`.
-- Safe to run if column already exists: use IF NOT EXISTS pattern below.

-- Option A: simple add (fails if column exists)
-- ALTER TABLE "Resources"
--     ADD COLUMN "SprintNumber" integer NULL;

-- Option B: idempotent (PostgreSQL 9.1+)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'Resources'
          AND column_name = 'SprintNumber'
    ) THEN
        ALTER TABLE "Resources"
            ADD COLUMN "SprintNumber" integer NULL;
    END IF;
END $$;
