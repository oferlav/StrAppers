-- Adds BoardURL to InstituteTemplates (same shape as ProjectBoards.BoardURL).
ALTER TABLE "InstituteTemplates"
ADD COLUMN IF NOT EXISTS "BoardURL" character varying(500);
