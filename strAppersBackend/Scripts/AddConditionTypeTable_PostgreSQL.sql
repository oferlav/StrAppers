-- Create ConditionType table for conditional MentorPrompt fragments.
-- Fragments with the same ConditionTypeId are mutually exclusive: at runtime include exactly one per type (selected by ConditionValue).
-- Run before AddConditionTypeIdAndConditionValueToMentorPrompt_PostgreSQL.sql.

CREATE TABLE IF NOT EXISTS "ConditionType" (
  "ConditionTypeId" SERIAL PRIMARY KEY,
  "Name" VARCHAR(100) NOT NULL,
  "Description" TEXT NULL,
  CONSTRAINT "UQ_ConditionType_Name" UNIQUE ("Name")
);

COMMENT ON TABLE "ConditionType" IS 'Types of conditional fragment groups. At runtime exactly one fragment per type is included (selected by ConditionValue).';

-- Seed the two condition types used by Current Mentor Context fragments (idempotent).
INSERT INTO "ConditionType" ("Name", "Description")
SELECT v."Name", v."Description"
FROM (VALUES
  ('ConversationType', 'New vs existing conversation. Resolve from hasChatHistory: NewConversation | ExistingConversation'),
  ('NextTeamMeeting', 'Next team meeting state. Resolve from context: MeetingNone | MeetingPast | MeetingFutureWithUrl | MeetingFutureNoUrl')
) AS v("Name", "Description")
WHERE NOT EXISTS (SELECT 1 FROM "ConditionType" c WHERE c."Name" = v."Name");
