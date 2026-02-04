-- Add ConditionTypeId and ConditionValue to MentorPrompt for conditional fragment selection.
-- NULL = always include. Non-null = include only when runtime resolves to this ConditionValue for this ConditionTypeId.
-- Run after AddConditionTypeTable_PostgreSQL.sql.

ALTER TABLE "MentorPrompt"
ADD COLUMN IF NOT EXISTS "ConditionTypeId" INTEGER NULL,
ADD COLUMN IF NOT EXISTS "ConditionValue" VARCHAR(50) NULL;

COMMENT ON COLUMN "MentorPrompt"."ConditionTypeId" IS 'FK to ConditionType. Same value for mutually exclusive variants; NULL = always include.';
COMMENT ON COLUMN "MentorPrompt"."ConditionValue" IS 'Value resolved at runtime (e.g. NewConversation, MeetingPast). Include fragment only when it matches.';

-- Add FK only if column was just added (avoid duplicate constraint errors on re-run).
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.table_constraints
    WHERE table_schema = 'public' AND table_name = 'MentorPrompt' AND constraint_name = 'FK_MentorPrompt_ConditionType_ConditionTypeId'
  ) THEN
    ALTER TABLE "MentorPrompt"
    ADD CONSTRAINT "FK_MentorPrompt_ConditionType_ConditionTypeId"
    FOREIGN KEY ("ConditionTypeId") REFERENCES "ConditionType"("ConditionTypeId") ON DELETE SET NULL;
  END IF;
END $$;

-- Set ConditionTypeId + ConditionValue for "Current Mentor Context" conditional fragments (RoleId IS NULL).
-- ConversationType: SortOrder 2 = NewConversation, 3 = ExistingConversation
UPDATE "MentorPrompt"
SET "ConditionTypeId" = (SELECT "ConditionTypeId" FROM "ConditionType" WHERE "Name" = 'ConversationType' LIMIT 1),
    "ConditionValue" = 'NewConversation'
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "SortOrder" = 2
  AND "RoleId" IS NULL;

UPDATE "MentorPrompt"
SET "ConditionTypeId" = (SELECT "ConditionTypeId" FROM "ConditionType" WHERE "Name" = 'ConversationType' LIMIT 1),
    "ConditionValue" = 'ExistingConversation'
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "SortOrder" = 3
  AND "RoleId" IS NULL;

-- NextTeamMeeting: SortOrder 5 = MeetingNone, 6 = MeetingPast, 7 = MeetingFutureWithUrl, 8 = MeetingFutureNoUrl
UPDATE "MentorPrompt"
SET "ConditionTypeId" = (SELECT "ConditionTypeId" FROM "ConditionType" WHERE "Name" = 'NextTeamMeeting' LIMIT 1),
    "ConditionValue" = 'MeetingNone'
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "SortOrder" = 5
  AND "RoleId" IS NULL;

UPDATE "MentorPrompt"
SET "ConditionTypeId" = (SELECT "ConditionTypeId" FROM "ConditionType" WHERE "Name" = 'NextTeamMeeting' LIMIT 1),
    "ConditionValue" = 'MeetingPast'
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "SortOrder" = 6
  AND "RoleId" IS NULL;

UPDATE "MentorPrompt"
SET "ConditionTypeId" = (SELECT "ConditionTypeId" FROM "ConditionType" WHERE "Name" = 'NextTeamMeeting' LIMIT 1),
    "ConditionValue" = 'MeetingFutureWithUrl'
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "SortOrder" = 7
  AND "RoleId" IS NULL;

UPDATE "MentorPrompt"
SET "ConditionTypeId" = (SELECT "ConditionTypeId" FROM "ConditionType" WHERE "Name" = 'NextTeamMeeting' LIMIT 1),
    "ConditionValue" = 'MeetingFutureNoUrl'
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "SortOrder" = 8
  AND "RoleId" IS NULL;
