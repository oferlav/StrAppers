-- Backfill missing ConditionTypeId + ConditionValue for SortOrder 3 (ExistingConversation) and SortOrder 5 (MeetingNone).
-- Run if AddConditionTypeIdAndConditionValueToMentorPrompt_PostgreSQL.sql left those rows NULL (e.g. rows didn't exist at first run).
-- Run after AddConditionTypeIdAndConditionValueToMentorPrompt_PostgreSQL.sql.

-- Optional: show current rows for "Current Mentor Context" (RoleId NULL) before fix
-- SELECT "Id", "SortOrder", "ConditionTypeId", "ConditionValue" FROM "MentorPrompt" m
-- WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1) AND "RoleId" IS NULL ORDER BY "SortOrder";

-- ConversationType: SortOrder 3 = ExistingConversation
UPDATE "MentorPrompt"
SET "ConditionTypeId" = (SELECT "ConditionTypeId" FROM "ConditionType" WHERE "Name" = 'ConversationType' LIMIT 1),
    "ConditionValue" = 'ExistingConversation'
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "SortOrder" = 3
  AND "RoleId" IS NULL;

-- NextTeamMeeting: SortOrder 5 = MeetingNone
UPDATE "MentorPrompt"
SET "ConditionTypeId" = (SELECT "ConditionTypeId" FROM "ConditionType" WHERE "Name" = 'NextTeamMeeting' LIMIT 1),
    "ConditionValue" = 'MeetingNone'
WHERE "CategoryId" = (SELECT "CategoryId" FROM "PromptCategories" WHERE "Name" = 'Current Mentor Context' LIMIT 1)
  AND "SortOrder" = 5
  AND "RoleId" IS NULL;
