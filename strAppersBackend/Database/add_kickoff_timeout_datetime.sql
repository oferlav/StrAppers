-- Kickoff deadline extension: lets a squad propose a kickoff meeting further out than the default
-- KickoffConfig2:BoardTimeout window (3 days from board creation) without the reset job disbanding
-- them before the meeting happens.
--
-- Run this script manually (no EF Core migration for this feature, matching add_kickoff_dispute_fields.sql).
--
-- NULL = never extended; the board is still held to CreatedAt + KickoffConfig2:BoardTimeout. Only
-- boards where someone proposed a meeting past that deadline get a value here, set to the proposed
-- meeting time + 12 hours grace. Extend-only: the backend never writes an earlier value than the
-- one already stored, so a later proposal or a rejection cannot shorten the window again.
--
-- Existing rows are intentionally left NULL — every board in flight keeps exactly the deadline it
-- has today.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'ProjectBoards' AND column_name = 'KickoffTimeoutDateTime'
    ) THEN
        ALTER TABLE "ProjectBoards" ADD COLUMN "KickoffTimeoutDateTime" timestamp with time zone NULL;
        RAISE NOTICE 'Column KickoffTimeoutDateTime added successfully to ProjectBoards table';
    ELSE
        RAISE NOTICE 'Column KickoffTimeoutDateTime already exists in ProjectBoards table';
    END IF;
END $$;

COMMENT ON COLUMN "ProjectBoards"."KickoffTimeoutDateTime" IS 'Absolute UTC deadline for unanimous kickoff agreement once extended past CreatedAt + KickoffConfig2:BoardTimeout by a later meeting proposal (proposed time + 12h grace). NULL = never extended, configured timeout applies. Extend-only.';

-- Verify
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_name = 'ProjectBoards' AND column_name = 'KickoffTimeoutDateTime';
