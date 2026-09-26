-- =============================================================================
-- Dinner Scout exercise: contract, requirements, test city, scenario set v1, graded link to project 84
-- (PostgreSQL). Generated from GoogleProxyFixtures/midtown-dinner-1.json.
--
-- Requires agent_exercise_add.sql and the "Client" + "IT Manager" personas (dinnerscout_personas_seed.sql).
-- Safe to re-run: existing rows are left as they are (a published set is frozen; change it by adding v2).
-- Rollback: section R.
-- =============================================================================

BEGIN;

-- 1. Exercise (endpoint + limits; checkRequirements maps every-scenario checks to requirement codes)
INSERT INTO "AgentExercises" ("Key", "Name", "EndpointPath", "LimitsJson")
VALUES ('dinner-scout', 'Dinner Scout', '/api/agent/dinner', $seed${"maxLatencyMs": 20000, "maxDetailsCalls": 5, "minGeminiCallsWhenOk": 2, "maxWhyChars": 200, "maxResults": 5, "checkRequirements": {"contract": "R13", "contract.latency": "R16", "contract.resultCount": "R1", "contract.trace": "R2", "grounding.found": "R6", "grounding.facts": "R6", "grounding.travel": "R14", "behavior.detailsLimit": "R12", "behavior.agentLoop": "R15"}}$seed$)
ON CONFLICT ("Key") DO NOTHING;

-- 2. Requirements, each pointing at the persona who reveals it
INSERT INTO "AgentRequirements" ("ExerciseId", "Code", "Text", "PersonaId")
SELECT e."Id", r.code, r.text, (SELECT p."Id" FROM "AIPersonas" p WHERE p."Name" = r.persona ORDER BY p."Id" LIMIT 1)
FROM "AgentExercises" e
CROSS JOIN (VALUES
    ('R1', $seed$A shortlist of 3 to 5 places, best first, each with name, travel minutes, rating, price level and one sentence on why it fits this request. Fewer only when fewer exist.$seed$, 'Client'),
    ('R2', $seed$Every answer includes a step-by-step record of what was looked up and decided.$seed$, 'Client'),
    ('R3', $seed$Fit to the request beats fame, and ratings are trusted only with enough reviews.$seed$, 'Client'),
    ('R4', $seed$Walking distance means about 15 minutes on foot; walking unless the guest will drive or take a taxi.$seed$, 'Client'),
    ('R5', $seed$Soft preferences (quiet, romantic, good for kids, business) are judged from what other diners say in reviews.$seed$, 'Client'),
    ('R6', $seed$Never recommend a place that does not exist or that was not actually found.$seed$, 'Client'),
    ('R7', $seed$Never recommend a closed place when the guest asked for somewhere open now.$seed$, 'Client'),
    ('R8', $seed$Dietary needs are never relaxed.$seed$, 'Client'),
    ('R9', $seed$When nothing fits, loosen distance first, then price, then rating, and say exactly what was loosened.$seed$, 'Client'),
    ('R10', $seed$When the location is unclear, ask one short question and look nothing up until it is answered.$seed$, 'Client'),
    ('R11', $seed$When the exact position is given, use it and do not look up the address again.$seed$, 'Client'),
    ('R12', $seed$Look into the details of at most 5 restaurants per request.$seed$, 'Client'),
    ('R13', $seed$Follow the Integration Sheet contract exactly: POST /api/agent/dinner, fields, statuses and errors.$seed$, 'IT Manager'),
    ('R14', $seed$All Google calls go through the proxy with the run id; travelMinutes come from a Directions call in the same run.$seed$, 'IT Manager'),
    ('R15', $seed$A real agent loop: Gemini chooses the lookups through function calling (at least 2 Gemini calls when recommending).$seed$, 'IT Manager'),
    ('R16', $seed$Answer within 20 seconds.$seed$, 'IT Manager')
) AS r(code, text, persona)
WHERE e."Key" = 'dinner-scout'
ON CONFLICT ("ExerciseId", "Code") DO NOTHING;

-- 3. Test city (served by the proxy in fixture mode)
INSERT INTO "AgentWorlds" ("Key", "Name", "DataJson")
VALUES ('midtown-dinner-1', 'Midtown Manhattan (Dinner Scout)', $seed${
  "worldId": "midtown-dinner-1",
  "description": "Simulated Midtown Manhattan for the Dinner Scout exercise. All places, reviews and ids are fictional. Opening hours are frozen: openNow is as listed. The 'grading' block is read by the grader only and never returned by the proxy.",
  "geocode": [
    {
      "match": [
        "times square"
      ],
      "formattedAddress": "Times Square, New York, NY 10036, USA",
      "lat": 40.758,
      "lng": -73.9855,
      "placeId": "ChIJFxAnchorTimesSquare"
    },
    {
      "match": [
        "grand central"
      ],
      "formattedAddress": "Grand Central Terminal, 89 E 42nd St, New York, NY 10017, USA",
      "lat": 40.7527,
      "lng": -73.9772,
      "placeId": "ChIJFxAnchorGrandCentral"
    }
  ],
  "places": [
    {
      "id": "ChIJFxGreenTable",
      "displayName": "Green Table",
      "formattedAddress": "210 W 46th St, New York, NY 10036, USA",
      "lat": 40.7625,
      "lng": -73.9855,
      "rating": 4.6,
      "userRatingCount": 1840,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "vegetarian_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "vegetarian",
        "vegan",
        "salad",
        "bowls",
        "healthy"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Calm and intimate, soft lighting and you can actually hear each other. Perfect first date spot."
        },
        {
          "rating": 5,
          "text": "Entirely vegetarian menu, great prices for Midtown. Quiet even on a Friday."
        },
        {
          "rating": 4,
          "text": "Small room, cozy tables, service a bit slow but relaxed atmosphere."
        }
      ]
    },
    {
      "id": "ChIJFxLeafLantern",
      "displayName": "Leaf & Lantern",
      "formattedAddress": "341 W 45th St, New York, NY 10036, USA",
      "lat": 40.758,
      "lng": -73.994,
      "rating": 4.5,
      "userRatingCount": 920,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "vegan_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "vegan",
        "vegetarian",
        "plant based",
        "tapas"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Candlelit and cozy, very romantic. All plant based."
        },
        {
          "rating": 4,
          "text": "Quiet little place, good for conversation. Portions are small."
        },
        {
          "rating": 5,
          "text": "Great value vegan tapas, lovely date night."
        }
      ]
    },
    {
      "id": "ChIJFxPulseGarden",
      "displayName": "Pulse Garden",
      "formattedAddress": "701 7th Ave, New York, NY 10036, USA",
      "lat": 40.7555,
      "lng": -73.983,
      "rating": 4.9,
      "userRatingCount": 2100,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "vegetarian_restaurant",
        "bar",
        "restaurant",
        "food"
      ],
      "keywords": [
        "vegetarian",
        "vegan",
        "burgers",
        "cocktails"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Amazing veggie burgers but it is LOUD. DJ every night, you have to shout."
        },
        {
          "rating": 5,
          "text": "Party vibe, great for big groups and birthdays. Not a place to talk."
        },
        {
          "rating": 5,
          "text": "Music is blasting, food is fantastic and cheap."
        }
      ]
    },
    {
      "id": "ChIJFxSproutBowl",
      "displayName": "Sprout Bowl",
      "formattedAddress": "155 W 47th St, New York, NY 10036, USA",
      "lat": 40.76,
      "lng": -73.982,
      "rating": 5.0,
      "userRatingCount": 4,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "vegetarian_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "vegetarian",
        "bowls",
        "salad"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Just opened, nice and quiet."
        },
        {
          "rating": 5,
          "text": "Good bowls."
        }
      ]
    },
    {
      "id": "ChIJFxPrimeCut",
      "displayName": "Prime Cut Steakhouse",
      "formattedAddress": "1501 Broadway, New York, NY 10036, USA",
      "lat": 40.757,
      "lng": -73.987,
      "rating": 4.8,
      "userRatingCount": 3200,
      "priceLevel": "PRICE_LEVEL_MODERATE",
      "openNow": true,
      "servesVegetarianFood": false,
      "types": [
        "steak_house",
        "restaurant",
        "food"
      ],
      "keywords": [
        "steak",
        "steakhouse",
        "grill",
        "wine"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Quiet, romantic, perfect for a date. Best ribeye in the city."
        },
        {
          "rating": 5,
          "text": "Elegant and calm. Almost nothing on the menu without meat."
        },
        {
          "rating": 4,
          "text": "Great steak, reasonable for the area."
        }
      ]
    },
    {
      "id": "ChIJFxMoonflower",
      "displayName": "Moonflower Kitchen",
      "formattedAddress": "253 W 44th St, New York, NY 10036, USA",
      "lat": 40.7595,
      "lng": -73.988,
      "rating": 4.7,
      "userRatingCount": 1300,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": false,
      "servesVegetarianFood": true,
      "types": [
        "vegetarian_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "vegetarian",
        "vegan",
        "brunch",
        "cafe"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Peaceful, quiet, beautiful plants everywhere. Lovely for a date."
        },
        {
          "rating": 5,
          "text": "All vegetarian and very affordable. Closes early though."
        },
        {
          "rating": 4,
          "text": "Calm atmosphere, great brunch."
        }
      ]
    },
    {
      "id": "ChIJFxSaffronStreet",
      "displayName": "Saffron Street",
      "formattedAddress": "602 8th Ave, New York, NY 10018, USA",
      "lat": 40.754,
      "lng": -73.9875,
      "rating": 4.4,
      "userRatingCount": 2600,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "indian_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "indian",
        "vegetarian",
        "curry",
        "thali"
      ],
      "reviews": [
        {
          "rating": 4,
          "text": "Busy but you can still talk. Huge vegetarian thali for the price."
        },
        {
          "rating": 5,
          "text": "Cheap, fast and tasty. Half the menu is vegetarian."
        },
        {
          "rating": 4,
          "text": "A bit crowded at peak hours."
        }
      ]
    },
    {
      "id": "ChIJFxHarvestHall",
      "displayName": "Harvest Hall",
      "formattedAddress": "120 W 48th St, New York, NY 10036, USA",
      "lat": 40.761,
      "lng": -73.98,
      "rating": 4.8,
      "userRatingCount": 700,
      "priceLevel": "PRICE_LEVEL_VERY_EXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "vegetarian_restaurant",
        "fine_dining_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "vegetarian",
        "tasting menu",
        "fine dining"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Quiet, elegant, unforgettable vegetarian tasting menu. Very pricey."
        },
        {
          "rating": 5,
          "text": "Special occasion place. Romantic and hushed."
        },
        {
          "rating": 4,
          "text": "Worth it once, but it will cost you."
        }
      ]
    },
    {
      "id": "ChIJFxFarFields",
      "displayName": "Far Fields",
      "formattedAddress": "301 W 72nd St, New York, NY 10023, USA",
      "lat": 40.78,
      "lng": -73.98,
      "rating": 4.7,
      "userRatingCount": 1500,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "vegetarian_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "vegetarian",
        "vegan",
        "farm to table"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Quiet neighborhood gem, romantic and affordable."
        },
        {
          "rating": 5,
          "text": "All vegetarian, calm, lovely for a date."
        },
        {
          "rating": 4,
          "text": "Worth the trip uptown."
        }
      ]
    },
    {
      "id": "ChIJFxNoodleRush",
      "displayName": "Noodle Rush",
      "formattedAddress": "1560 Broadway, New York, NY 10036, USA",
      "lat": 40.757,
      "lng": -73.984,
      "rating": 4.3,
      "userRatingCount": 5100,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": false,
      "types": [
        "ramen_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "ramen",
        "noodles",
        "japanese"
      ],
      "reviews": [
        {
          "rating": 4,
          "text": "Fast, loud, packed. Pork broth is excellent."
        },
        {
          "rating": 4,
          "text": "Long lines, communal tables."
        }
      ]
    },
    {
      "id": "ChIJFxOliveEmber",
      "displayName": "Olive & Ember",
      "formattedAddress": "358 W 51st St, New York, NY 10019, USA",
      "lat": 40.765,
      "lng": -73.99,
      "rating": 4.5,
      "userRatingCount": 1100,
      "priceLevel": "PRICE_LEVEL_MODERATE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "mediterranean_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "mediterranean",
        "greek",
        "mezze",
        "vegetarian options"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Quiet back room, great for a date. Lots of vegetarian mezze."
        },
        {
          "rating": 4,
          "text": "Relaxed, warm service, a bit pricier than expected."
        }
      ]
    },
    {
      "id": "ChIJFxCornerSlice",
      "displayName": "Corner Slice",
      "formattedAddress": "1580 Broadway, New York, NY 10036, USA",
      "lat": 40.7585,
      "lng": -73.985,
      "rating": 4.2,
      "userRatingCount": 6400,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "pizza_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "pizza",
        "slice",
        "italian"
      ],
      "reviews": [
        {
          "rating": 4,
          "text": "Classic NY slice, standing room only, very noisy."
        },
        {
          "rating": 4,
          "text": "Cheap and quick, tourists everywhere."
        }
      ]
    },
    {
      "id": "ChIJFxBlueNile",
      "displayName": "Blue Nile Table",
      "formattedAddress": "120 W 56th St, New York, NY 10019, USA",
      "lat": 40.768,
      "lng": -73.979,
      "rating": 4.5,
      "userRatingCount": 860,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "ethiopian_restaurant",
        "african_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "ethiopian",
        "injera",
        "african",
        "vegetarian platter"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "The only Ethiopian place in Midtown worth it. Huge vegetarian platter."
        },
        {
          "rating": 4,
          "text": "Warm, relaxed, great value."
        }
      ]
    }
  ]
}$seed$)
ON CONFLICT ("Key") DO NOTHING;

-- 4. Scenario set v1, published
INSERT INTO "AgentScenarioSets" ("ExerciseId", "WorldId", "Name", "Version", "Status", "PublishedAt")
SELECT e."Id", w."Id", 'Dinner Scout acceptance', 1, 'published', now()
FROM "AgentExercises" e, "AgentWorlds" w
WHERE e."Key" = 'dinner-scout' AND w."Key" = 'midtown-dinner-1'
ON CONFLICT ("ExerciseId", "Name", "Version") DO NOTHING;

-- 5. Scenarios (ExpectationsJson carries each expectation's requirement code)
INSERT INTO "AgentScenarios" ("ScenarioSetId", "Key", "Request", "PositionLat", "PositionLng", "ExpectationsJson", "SortOrder")
SELECT s."Id", v.key, v.request, v.lat, v.lng, v.expectations, v.sort
FROM "AgentScenarioSets" s
JOIN "AgentExercises" e ON e."Id" = s."ExerciseId"
CROSS JOIN (VALUES
    ('vegetarian-quiet-date', $seed$Cheap vegetarian place near Times Square, open now, quiet enough for a date$seed$, NULL, NULL, $seed${"expectStatus": "ok", "expectTop1In": ["ChIJFxGreenTable", "ChIJFxLeafLantern"], "mustNotInclude": ["ChIJFxPrimeCut", "ChIJFxMoonflower", "ChIJFxNoodleRush"], "mustNotBeInTop3": ["ChIJFxPulseGarden", "ChIJFxSproutBowl", "ChIJFxHarvestHall", "ChIJFxFarFields"], "requirements": {"expectStatus": "R13", "expectTop1In": "R3", "mustNotInclude": "R7, R8", "mustNotBeInTop3": "R3, R4, R5"}}$seed$, 1),
    ('vegetarian-quiet-date-with-position', $seed$Cheap vegetarian place near me, open now, quiet enough for a date$seed$, 40.758, -73.9855, $seed${"expectStatus": "ok", "expectTop1In": ["ChIJFxGreenTable", "ChIJFxLeafLantern"], "mustNotInclude": ["ChIJFxPrimeCut", "ChIJFxMoonflower", "ChIJFxNoodleRush"], "expectGeocodeCall": false, "requirements": {"expectStatus": "R13", "expectTop1In": "R3", "mustNotInclude": "R7, R8", "expectGeocodeCall": "R11"}}$seed$, 2),
    ('ethiopian-relax-distance', $seed$Ethiopian food within a 5 minute walk of Times Square, open now$seed$, NULL, NULL, $seed${"expectStatus": "ok", "minResults": 1, "expectTop1In": ["ChIJFxBlueNile"], "expectRelaxed": ["distance"], "requirements": {"expectStatus": "R13", "minResults": "R1", "expectTop1In": "R3", "expectRelaxed": "R9"}}$seed$, 3),
    ('ambiguous-location', $seed$Somewhere quiet for dinner near my office$seed$, NULL, NULL, $seed${"expectStatus": "needs_clarification", "expectNoMapsCalls": true, "requirements": {"expectStatus": "R10", "expectNoMapsCalls": "R10"}}$seed$, 4)
) AS v(key, request, lat, lng, expectations, sort)
WHERE e."Key" = 'dinner-scout' AND s."Name" = 'Dinner Scout acceptance' AND s."Version" = 1
ON CONFLICT ("ScenarioSetId", "Key") DO NOTHING;

-- 6. Project 84 is graded on that set
INSERT INTO "ProjectAgentScenarioSets" ("InstituteProjectId", "ScenarioSetId", "Purpose")
SELECT 84, s."Id", 'graded'
FROM "AgentScenarioSets" s JOIN "AgentExercises" e ON e."Id" = s."ExerciseId"
WHERE e."Key" = 'dinner-scout' AND s."Name" = 'Dinner Scout acceptance' AND s."Version" = 1
ON CONFLICT ("InstituteProjectId", "Purpose") DO UPDATE SET "ScenarioSetId" = EXCLUDED."ScenarioSetId";

COMMIT;


-- =============================================================================
-- Verify (expect 16 requirements with personas, 4 scenarios, 1 graded link)
-- =============================================================================
-- SELECT r."Code", p."Name" FROM "AgentRequirements" r LEFT JOIN "AIPersonas" p ON p."Id" = r."PersonaId" ORDER BY r."Id";
-- SELECT s."Name", s."Version", s."Status", count(sc."Id") AS scenarios
--   FROM "AgentScenarioSets" s LEFT JOIN "AgentScenarios" sc ON sc."ScenarioSetId" = s."Id" GROUP BY s."Id";
-- SELECT * FROM "ProjectAgentScenarioSets" WHERE "InstituteProjectId" = 84;


-- =============================================================================
-- R. ROLLBACK
-- =============================================================================
-- BEGIN;
-- DELETE FROM "ProjectAgentScenarioSets" WHERE "InstituteProjectId" = 84;
-- DELETE FROM "AgentGradingReports" WHERE "ScenarioSetId" IN (SELECT s."Id" FROM "AgentScenarioSets" s JOIN "AgentExercises" e ON e."Id" = s."ExerciseId" WHERE e."Key" = 'dinner-scout');
-- DELETE FROM "AgentScenarioSets" WHERE "ExerciseId" IN (SELECT "Id" FROM "AgentExercises" WHERE "Key" = 'dinner-scout');
-- DELETE FROM "AgentWorlds" WHERE "Key" = 'midtown-dinner-1';
-- DELETE FROM "AgentExercises" WHERE "Key" = 'dinner-scout';
-- COMMIT;
