-- Add document/journey URL columns (VARCHAR 1000) and document name columns (VARCHAR 50) to ProjectBoards table
-- Run this script if you need to manually add the columns instead of using EF Core migrations

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ProjectBoards' AND column_name = 'CollectionJourneyUrl') THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "CollectionJourneyUrl" VARCHAR(1000) NULL;
        RAISE NOTICE 'Column CollectionJourneyUrl added to ProjectBoards';
    ELSE
        RAISE NOTICE 'Column CollectionJourneyUrl already exists in ProjectBoards';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ProjectBoards' AND column_name = 'DatabaseSchemaUrl') THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "DatabaseSchemaUrl" VARCHAR(1000) NULL;
        RAISE NOTICE 'Column DatabaseSchemaUrl added to ProjectBoards';
    ELSE
        RAISE NOTICE 'Column DatabaseSchemaUrl already exists in ProjectBoards';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ProjectBoards' AND column_name = 'Document1Url') THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "Document1Url" VARCHAR(1000) NULL;
        RAISE NOTICE 'Column Document1Url added to ProjectBoards';
    ELSE
        RAISE NOTICE 'Column Document1Url already exists in ProjectBoards';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ProjectBoards' AND column_name = 'Document2Url') THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "Document2Url" VARCHAR(1000) NULL;
        RAISE NOTICE 'Column Document2Url added to ProjectBoards';
    ELSE
        RAISE NOTICE 'Column Document2Url already exists in ProjectBoards';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ProjectBoards' AND column_name = 'Document3Url') THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "Document3Url" VARCHAR(1000) NULL;
        RAISE NOTICE 'Column Document3Url added to ProjectBoards';
    ELSE
        RAISE NOTICE 'Column Document3Url already exists in ProjectBoards';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ProjectBoards' AND column_name = 'Document4Url') THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "Document4Url" VARCHAR(1000) NULL;
        RAISE NOTICE 'Column Document4Url added to ProjectBoards';
    ELSE
        RAISE NOTICE 'Column Document4Url already exists in ProjectBoards';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ProjectBoards' AND column_name = 'Document1Name') THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "Document1Name" VARCHAR(50) NULL;
        RAISE NOTICE 'Column Document1Name added to ProjectBoards';
    ELSE
        RAISE NOTICE 'Column Document1Name already exists in ProjectBoards';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ProjectBoards' AND column_name = 'Document2Name') THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "Document2Name" VARCHAR(50) NULL;
        RAISE NOTICE 'Column Document2Name added to ProjectBoards';
    ELSE
        RAISE NOTICE 'Column Document2Name already exists in ProjectBoards';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ProjectBoards' AND column_name = 'Document3Name') THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "Document3Name" VARCHAR(50) NULL;
        RAISE NOTICE 'Column Document3Name added to ProjectBoards';
    ELSE
        RAISE NOTICE 'Column Document3Name already exists in ProjectBoards';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ProjectBoards' AND column_name = 'Document4Name') THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "Document4Name" VARCHAR(50) NULL;
        RAISE NOTICE 'Column Document4Name added to ProjectBoards';
    ELSE
        RAISE NOTICE 'Column Document4Name already exists in ProjectBoards';
    END IF;
END $$;
