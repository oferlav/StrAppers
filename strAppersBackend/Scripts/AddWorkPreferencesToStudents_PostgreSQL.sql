-- Add Work Preferences and CV fields to Students table for PostgreSQL
-- This script adds the work preference fields and CV field to the existing Students table
-- Note: PostgreSQL converts unquoted identifiers to lowercase

-- Add CV column (text type for base64 encoded CV file)
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'CV'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "CV" TEXT NULL;
        
        COMMENT ON COLUMN "Students"."CV" IS 'Base64 encoded CV file (PDF/document)';
    END IF;
END $$;

-- Add MinutesToWork column (integer, nullable)
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'MinutesToWork'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "MinutesToWork" INTEGER NULL;
        
        COMMENT ON COLUMN "Students"."MinutesToWork" IS 'Number of minutes available to work';
    END IF;
END $$;

-- Add HybridWork column (boolean, default false)
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'HybridWork'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "HybridWork" BOOLEAN NOT NULL DEFAULT FALSE;
        
        COMMENT ON COLUMN "Students"."HybridWork" IS 'Willing to work in hybrid mode';
    END IF;
END $$;

-- Add HomeWork column (boolean, default false)
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'HomeWork'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "HomeWork" BOOLEAN NOT NULL DEFAULT FALSE;
        
        COMMENT ON COLUMN "Students"."HomeWork" IS 'Willing to work from home';
    END IF;
END $$;

-- Add FullTimeWork column (boolean, default false)
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'FullTimeWork'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "FullTimeWork" BOOLEAN NOT NULL DEFAULT FALSE;
        
        COMMENT ON COLUMN "Students"."FullTimeWork" IS 'Willing to work full-time';
    END IF;
END $$;

-- Add PartTimeWork column (boolean, default false)
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'PartTimeWork'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "PartTimeWork" BOOLEAN NOT NULL DEFAULT FALSE;
        
        COMMENT ON COLUMN "Students"."PartTimeWork" IS 'Willing to work part-time';
    END IF;
END $$;

-- Add FreelanceWork column (boolean, default false)
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'FreelanceWork'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "FreelanceWork" BOOLEAN NOT NULL DEFAULT FALSE;
        
        COMMENT ON COLUMN "Students"."FreelanceWork" IS 'Willing to work as freelancer';
    END IF;
END $$;

-- Add TravelWork column (boolean, default false)
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'TravelWork'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "TravelWork" BOOLEAN NOT NULL DEFAULT FALSE;
        
        COMMENT ON COLUMN "Students"."TravelWork" IS 'Willing to travel for work';
    END IF;
END $$;

-- Add NightShiftWork column (boolean, default false)
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'NightShiftWork'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "NightShiftWork" BOOLEAN NOT NULL DEFAULT FALSE;
        
        COMMENT ON COLUMN "Students"."NightShiftWork" IS 'Willing to work night shifts';
    END IF;
END $$;

-- Add RelocationWork column (boolean, default false)
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'RelocationWork'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "RelocationWork" BOOLEAN NOT NULL DEFAULT FALSE;
        
        COMMENT ON COLUMN "Students"."RelocationWork" IS 'Willing to relocate for work';
    END IF;
END $$;

-- Add StudentWork column (boolean, default false)
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'StudentWork'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "StudentWork" BOOLEAN NOT NULL DEFAULT FALSE;
        
        COMMENT ON COLUMN "Students"."StudentWork" IS 'Willing to work as student';
    END IF;
END $$;

-- Add MultilingualWork column (boolean, default false)
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Students' 
        AND column_name = 'MultilingualWork'
    ) THEN
        ALTER TABLE "Students" 
        ADD COLUMN "MultilingualWork" BOOLEAN NOT NULL DEFAULT FALSE;
        
        COMMENT ON COLUMN "Students"."MultilingualWork" IS 'Willing to work in multilingual environment';
    END IF;
END $$;

-- Verify all columns were added
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'Students' 
AND column_name IN (
    'CV', 
    'MinutesToWork', 
    'HybridWork', 
    'HomeWork', 
    'FullTimeWork', 
    'PartTimeWork', 
    'FreelanceWork', 
    'TravelWork', 
    'NightShiftWork', 
    'RelocationWork', 
    'StudentWork', 
    'MultilingualWork'
)
ORDER BY column_name;


