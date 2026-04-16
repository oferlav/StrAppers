-- Students: optional 1:1 help meeting (Teams), aligned with ProjectBoards NextMeeting* types.
-- Safe to run once on PostgreSQL.

ALTER TABLE "Students" ADD COLUMN IF NOT EXISTS "NextMeetingTime" timestamp with time zone NULL;
ALTER TABLE "Students" ADD COLUMN IF NOT EXISTS "NextMeetingUrl" character varying(1000) NULL;
