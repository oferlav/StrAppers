-- Add NextMeetingTeacherAttendance to ProjectBoards (boolean, default false).
-- Institute "Get Invited" sets this true for the current next meeting; reset when Teams flow updates next meeting.
-- Safe to run once on PostgreSQL.

ALTER TABLE "ProjectBoards"
    ADD COLUMN IF NOT EXISTS "NextMeetingTeacherAttendance" boolean NOT NULL DEFAULT false;
