-- Fix MentorChatHistory Id sequence when you get: duplicate key value violates unique constraint "MentorChatHistory_pkey"
-- This happens when the sequence is out of sync (e.g. after restore/import with explicit Ids).
-- Run this once against your database, then retry the mentor chat.

-- Reset the Id sequence to the next value after the current max Id
SELECT setval(
    pg_get_serial_sequence('"MentorChatHistory"', 'Id'),
    COALESCE((SELECT MAX("Id") FROM "MentorChatHistory"), 0) + 1
);
