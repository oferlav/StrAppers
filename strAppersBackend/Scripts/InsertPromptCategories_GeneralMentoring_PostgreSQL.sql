-- Insert General Mentoring categories into PromptCategories.
-- Run after AddPromptCategoriesAndMentorPromptTables_PostgreSQL.sql.
-- RoleId NULL in MentorPrompt means these categories apply to all user roles when building the Mentor prompt.

-- General Mentoring categories (SortOrder 10–40 so they can be mixed with Sprint Planning / Developers later)
-- Idempotent: only inserts when a category with the same Name does not exist.
INSERT INTO "PromptCategories" ("Name", "Description", "SortOrder")
SELECT v."Name", v."Description", v."SortOrder"
FROM (VALUES
  (
    'Platform Context and Vision',
    'Overall platform mission, how Skill-In works, and the Mentor''s role. From MentorController.GetPlatformContextAndLimitations().',
    10
  ),
  (
    'Base Mentor Persona and General Instructions',
    'Core Mentor persona, key guidelines, collaboration rules, knowledge limitations, code review honesty, code formatting, GitHub assumptions (developer vs non-developer). From PromptConfig:Mentor:SystemPrompt (excluding Trello sections).',
    20
  ),
  (
    'Trello Knowledge',
    'Trello card custom fields and dependency rules the Mentor is aware of. From PromptConfig:Mentor:SystemPrompt.',
    30
  ),
  (
    'Current Mentor Context',
    'User prompt template, context summary (mentor intro, team/task labels), current date, next team meeting, and new vs existing conversation instructions. From UserPromptTemplate and MentorController.',
    40
  )
) AS v("Name", "Description", "SortOrder")
WHERE NOT EXISTS (SELECT 1 FROM "PromptCategories" c WHERE c."Name" = v."Name");
