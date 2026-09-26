-- =============================================================================
-- Dinner Scout scenario set v2  (PostgreSQL). Generated from GoogleProxyFixtures/midtown-dinner-1.json.
--
-- Fixes found in grader calibration:
--   1. vegetarian-quiet-date-with-position: traps must stay out of the top 3 (as in scenario 1).
--   2. ethiopian-relax-distance: every result must be Ethiopian (allResultsIn), tagged R3.
--      Needs the backend build that knows "allResultsIn" (grader check ranking.allResultsFit).
--
-- Creates v2 (published), moves every project graded on v1 to v2, archives v1.
-- v1 and its grading reports stay in the DB. Safe to re-run. Rollback: section R.
-- =============================================================================

BEGIN;

-- 1. Set v2, published
INSERT INTO "AgentScenarioSets" ("ExerciseId", "WorldId", "Name", "Version", "Status", "PublishedAt")
SELECT s."ExerciseId", s."WorldId", s."Name", 2, 'published', now()
FROM "AgentScenarioSets" s JOIN "AgentExercises" e ON e."Id" = s."ExerciseId"
WHERE e."Key" = 'dinner-scout' AND s."Name" = 'Dinner Scout acceptance' AND s."Version" = 1
ON CONFLICT ("ExerciseId", "Name", "Version") DO NOTHING;

-- 2. Its scenarios
INSERT INTO "AgentScenarios" ("ScenarioSetId", "Key", "Request", "PositionLat", "PositionLng", "ExpectationsJson", "SortOrder")
SELECT s."Id", v.key, v.request, v.lat, v.lng, v.expectations, v.sort
FROM "AgentScenarioSets" s JOIN "AgentExercises" e ON e."Id" = s."ExerciseId"
CROSS JOIN (VALUES
    ('vegetarian-quiet-date', $seed$Cheap vegetarian place near Times Square, open now, quiet enough for a date$seed$, NULL, NULL, $seed${"expectStatus": "ok", "expectTop1In": ["ChIJFxGreenTable", "ChIJFxLeafLantern"], "mustNotInclude": ["ChIJFxPrimeCut", "ChIJFxMoonflower", "ChIJFxNoodleRush"], "mustNotBeInTop3": ["ChIJFxPulseGarden", "ChIJFxSproutBowl", "ChIJFxHarvestHall", "ChIJFxFarFields"], "requirements": {"expectStatus": "R13", "expectTop1In": "R3", "mustNotInclude": "R7, R8", "mustNotBeInTop3": "R3, R4, R5"}}$seed$, 1),
    ('vegetarian-quiet-date-with-position', $seed$Cheap vegetarian place near me, open now, quiet enough for a date$seed$, 40.758, -73.9855, $seed${"expectStatus": "ok", "expectTop1In": ["ChIJFxGreenTable", "ChIJFxLeafLantern"], "mustNotInclude": ["ChIJFxPrimeCut", "ChIJFxMoonflower", "ChIJFxNoodleRush"], "expectGeocodeCall": false, "requirements": {"expectStatus": "R13", "expectTop1In": "R3", "mustNotInclude": "R7, R8", "expectGeocodeCall": "R11", "mustNotBeInTop3": "R3, R4, R5"}, "mustNotBeInTop3": ["ChIJFxPulseGarden", "ChIJFxSproutBowl", "ChIJFxHarvestHall", "ChIJFxFarFields"]}$seed$, 2),
    ('ethiopian-relax-distance', $seed$Ethiopian food within a 5 minute walk of Times Square, open now$seed$, NULL, NULL, $seed${"expectStatus": "ok", "minResults": 1, "expectTop1In": ["ChIJFxBlueNile"], "expectRelaxed": ["distance"], "requirements": {"expectStatus": "R13", "minResults": "R1", "expectTop1In": "R3", "expectRelaxed": "R9", "allResultsIn": "R3"}, "allResultsIn": ["ChIJFxBlueNile"]}$seed$, 3),
    ('ambiguous-location', $seed$Somewhere quiet for dinner near my office$seed$, NULL, NULL, $seed${"expectStatus": "needs_clarification", "expectNoMapsCalls": true, "requirements": {"expectStatus": "R10", "expectNoMapsCalls": "R10"}}$seed$, 4)
) AS v(key, request, lat, lng, expectations, sort)
WHERE e."Key" = 'dinner-scout' AND s."Name" = 'Dinner Scout acceptance' AND s."Version" = 2
ON CONFLICT ("ScenarioSetId", "Key") DO NOTHING;

-- 3. Every project linked to v1 now uses v2
UPDATE "ProjectAgentScenarioSets" l
SET "ScenarioSetId" = v2."Id"
FROM "AgentScenarioSets" v1
JOIN "AgentScenarioSets" v2 ON v2."ExerciseId" = v1."ExerciseId" AND v2."Name" = v1."Name" AND v2."Version" = 2
JOIN "AgentExercises" e ON e."Id" = v1."ExerciseId"
WHERE e."Key" = 'dinner-scout' AND v1."Name" = 'Dinner Scout acceptance' AND v1."Version" = 1
  AND l."ScenarioSetId" = v1."Id";

-- 4. Archive v1 (kept for its reports; archived sets cannot be graded)
UPDATE "AgentScenarioSets" s SET "Status" = 'archived'
FROM "AgentExercises" e
WHERE e."Id" = s."ExerciseId" AND e."Key" = 'dinner-scout' AND s."Name" = 'Dinner Scout acceptance' AND s."Version" = 1;

COMMIT;


-- =============================================================================
-- Verify (expect v1 archived, v2 published with 4 scenarios, project 84 on v2)
-- =============================================================================
-- SELECT s."Version", s."Status", count(sc."Id") AS scenarios FROM "AgentScenarioSets" s
--   LEFT JOIN "AgentScenarios" sc ON sc."ScenarioSetId" = s."Id" WHERE s."Name" = 'Dinner Scout acceptance' GROUP BY s."Id" ORDER BY s."Version";
-- SELECT l."InstituteProjectId", l."Purpose", s."Version" FROM "ProjectAgentScenarioSets" l JOIN "AgentScenarioSets" s ON s."Id" = l."ScenarioSetId";


-- =============================================================================
-- R. ROLLBACK  (back to v1; v2 is kept, only unlinked and archived)
-- =============================================================================
-- BEGIN;
-- UPDATE "AgentScenarioSets" SET "Status" = 'published' WHERE "Name" = 'Dinner Scout acceptance' AND "Version" = 1;
-- UPDATE "ProjectAgentScenarioSets" l SET "ScenarioSetId" = v1."Id"
--   FROM "AgentScenarioSets" v1, "AgentScenarioSets" v2
--  WHERE v1."Name" = 'Dinner Scout acceptance' AND v1."Version" = 1 AND v2."Name" = v1."Name" AND v2."Version" = 2 AND l."ScenarioSetId" = v2."Id";
-- UPDATE "AgentScenarioSets" SET "Status" = 'archived' WHERE "Name" = 'Dinner Scout acceptance' AND "Version" = 2;
-- COMMIT;
