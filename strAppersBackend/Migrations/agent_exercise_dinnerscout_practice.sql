-- =============================================================================
-- Dinner Scout PRACTICE set: world chicago-practice-1 + scenario set "Dinner Scout practice" v1,
-- linked to institute project 84 as its practice set (PostgreSQL).
-- Generated from GoogleProxyFixtures/chicago-practice-1.json.
--
-- Same exercise, same kinds of scenarios and traps as the graded set, different city and places,
-- so students can practice freely without learning the graded answers.
-- Requires the backend build that knows "allResultsIn". Safe to re-run. Rollback: section R.
-- =============================================================================

BEGIN;

INSERT INTO "AgentWorlds" ("Key", "Name", "DataJson")
VALUES ('chicago-practice-1', 'Downtown Chicago (Dinner Scout practice)', $seed${
  "worldId": "chicago-practice-1",
  "description": "PRACTICE world for Dinner Scout: simulated downtown Chicago around Millennium Park. Same kinds of traps as the graded world, different places. All fictional. The grading block is read by the seed generator only.",
  "geocode": [
    {
      "match": [
        "millennium park"
      ],
      "formattedAddress": "Millennium Park, Chicago, IL 60602, USA",
      "lat": 41.8826,
      "lng": -87.6226,
      "placeId": "ChIJPxAnchorMillenniumPark"
    },
    {
      "match": [
        "willis tower"
      ],
      "formattedAddress": "Willis Tower, 233 S Wacker Dr, Chicago, IL 60606, USA",
      "lat": 41.8789,
      "lng": -87.6359,
      "placeId": "ChIJPxAnchorWillisTower"
    }
  ],
  "places": [
    {
      "id": "ChIJPxSproutHouse",
      "displayName": "Sprout House",
      "formattedAddress": "150 N Michigan Ave, Chicago, IL 60601, USA",
      "lat": 41.8876,
      "lng": -87.6226,
      "rating": 4.6,
      "userRatingCount": 1500,
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
        "bowls"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Quiet room at the back, easy to talk. We hold our team lunches here."
        },
        {
          "rating": 5,
          "text": "All vegan, great prices, calm even at noon."
        },
        {
          "rating": 4,
          "text": "Simple food, relaxed atmosphere, good wifi."
        }
      ]
    },
    {
      "id": "ChIJPxQuietLeaf",
      "displayName": "Quiet Leaf",
      "formattedAddress": "32 W Randolph St, Chicago, IL 60601, USA",
      "lat": 41.8846,
      "lng": -87.6306,
      "rating": 4.5,
      "userRatingCount": 800,
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
        "salads"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Calm and bright, perfect for a business lunch."
        },
        {
          "rating": 4,
          "text": "Low music, you can have a real conversation."
        },
        {
          "rating": 5,
          "text": "Cheap and fully plant based."
        }
      ]
    },
    {
      "id": "ChIJPxLotusNoodle",
      "displayName": "Lotus Noodle House",
      "formattedAddress": "20 E Adams St, Chicago, IL 60603, USA",
      "lat": 41.8786,
      "lng": -87.6226,
      "rating": 4.3,
      "userRatingCount": 2200,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "asian_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "vegan",
        "vegetarian",
        "noodles",
        "tofu"
      ],
      "reviews": [
        {
          "rating": 4,
          "text": "Busy at lunch but you can still talk. Lots of vegan dishes."
        },
        {
          "rating": 4,
          "text": "Fast and cheap, half the menu is vegan."
        },
        {
          "rating": 4,
          "text": "A bit crowded at peak hours."
        }
      ]
    },
    {
      "id": "ChIJPxBeetClub",
      "displayName": "Beet Club",
      "formattedAddress": "11 E Lake St, Chicago, IL 60601, USA",
      "lat": 41.8841,
      "lng": -87.6206,
      "rating": 4.9,
      "userRatingCount": 2500,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "vegan_restaurant",
        "bar",
        "restaurant",
        "food"
      ],
      "keywords": [
        "vegan",
        "vegetarian",
        "burgers",
        "cocktails"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Best vegan burgers in town, but the music is so loud you have to shout."
        },
        {
          "rating": 5,
          "text": "Party every night, DJ from 6pm. Not for meetings."
        },
        {
          "rating": 5,
          "text": "Loud, fun, cheap."
        }
      ]
    },
    {
      "id": "ChIJPxTinyGreens",
      "displayName": "Tiny Greens",
      "formattedAddress": "60 E Monroe St, Chicago, IL 60603, USA",
      "lat": 41.8806,
      "lng": -87.6246,
      "rating": 5.0,
      "userRatingCount": 3,
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
        "smoothies",
        "salads"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Just opened, quiet."
        },
        {
          "rating": 5,
          "text": "Nice smoothies."
        }
      ]
    },
    {
      "id": "ChIJPxChopHouse",
      "displayName": "Lakeshore Chop House",
      "formattedAddress": "200 E Randolph St, Chicago, IL 60601, USA",
      "lat": 41.8836,
      "lng": -87.6241,
      "rating": 4.8,
      "userRatingCount": 3100,
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
        "grill"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Quiet and elegant, perfect for business dinners."
        },
        {
          "rating": 5,
          "text": "Almost nothing without meat on the menu."
        },
        {
          "rating": 4,
          "text": "Great steaks."
        }
      ]
    },
    {
      "id": "ChIJPxMorningGlory",
      "displayName": "Morning Glory Kitchen",
      "formattedAddress": "75 E Washington St, Chicago, IL 60602, USA",
      "lat": 41.8851,
      "lng": -87.6236,
      "rating": 4.7,
      "userRatingCount": 1200,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": false,
      "servesVegetarianFood": true,
      "types": [
        "vegan_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "vegan",
        "vegetarian",
        "brunch"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Peaceful and quiet, great for a meeting over lunch."
        },
        {
          "rating": 5,
          "text": "All vegan and cheap. Closes early though."
        },
        {
          "rating": 4,
          "text": "Calm, lots of light."
        }
      ]
    },
    {
      "id": "ChIJPxVerdant",
      "displayName": "Verdant",
      "formattedAddress": "30 E Wacker Dr, Chicago, IL 60601, USA",
      "lat": 41.8856,
      "lng": -87.6266,
      "rating": 4.8,
      "userRatingCount": 650,
      "priceLevel": "PRICE_LEVEL_VERY_EXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "vegan_restaurant",
        "fine_dining_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "vegan",
        "vegetarian",
        "tasting menu",
        "fine dining"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Hushed, elegant vegan tasting menu. Very expensive."
        },
        {
          "rating": 5,
          "text": "For special occasions only."
        },
        {
          "rating": 4,
          "text": "Worth it once."
        }
      ]
    },
    {
      "id": "ChIJPxFarNorthVegan",
      "displayName": "Far North Vegan",
      "formattedAddress": "2400 N Clark St, Chicago, IL 60614, USA",
      "lat": 41.9126,
      "lng": -87.6226,
      "rating": 4.7,
      "userRatingCount": 1400,
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
        "vegetarian"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "Quiet neighborhood spot, great for a chat."
        },
        {
          "rating": 5,
          "text": "Cheap and all vegan."
        },
        {
          "rating": 4,
          "text": "Worth the trip north."
        }
      ]
    },
    {
      "id": "ChIJPxDeepDishDepot",
      "displayName": "Deep Dish Depot",
      "formattedAddress": "10 S Wabash Ave, Chicago, IL 60603, USA",
      "lat": 41.8816,
      "lng": -87.6216,
      "rating": 4.4,
      "userRatingCount": 5200,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": false,
      "types": [
        "pizza_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "pizza",
        "deep dish"
      ],
      "reviews": [
        {
          "rating": 4,
          "text": "Classic deep dish, loud and packed."
        },
        {
          "rating": 4,
          "text": "Long wait, tourists everywhere."
        }
      ]
    },
    {
      "id": "ChIJPxTacoRelay",
      "displayName": "Taco Relay",
      "formattedAddress": "120 S State St, Chicago, IL 60603, USA",
      "lat": 41.8796,
      "lng": -87.6256,
      "rating": 4.2,
      "userRatingCount": 3000,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": false,
      "types": [
        "mexican_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "tacos",
        "mexican"
      ],
      "reviews": [
        {
          "rating": 4,
          "text": "Quick tacos, standing room only."
        },
        {
          "rating": 4,
          "text": "Cheap, noisy, good."
        }
      ]
    },
    {
      "id": "ChIJPxAndesTable",
      "displayName": "Andes Table",
      "formattedAddress": "400 S Wabash Ave, Chicago, IL 60605, USA",
      "lat": 41.8706,
      "lng": -87.6226,
      "rating": 4.5,
      "userRatingCount": 700,
      "priceLevel": "PRICE_LEVEL_INEXPENSIVE",
      "openNow": true,
      "servesVegetarianFood": true,
      "types": [
        "peruvian_restaurant",
        "south_american_restaurant",
        "restaurant",
        "food"
      ],
      "keywords": [
        "peruvian",
        "ceviche",
        "south american"
      ],
      "reviews": [
        {
          "rating": 5,
          "text": "The only Peruvian place downtown. Great ceviche."
        },
        {
          "rating": 4,
          "text": "Warm service, fair prices."
        }
      ]
    }
  ]
}$seed$)
ON CONFLICT ("Key") DO NOTHING;

INSERT INTO "AgentScenarioSets" ("ExerciseId", "WorldId", "Name", "Version", "Status", "PublishedAt")
SELECT e."Id", w."Id", 'Dinner Scout practice', 1, 'published', now()
FROM "AgentExercises" e, "AgentWorlds" w
WHERE e."Key" = 'dinner-scout' AND w."Key" = 'chicago-practice-1'
ON CONFLICT ("ExerciseId", "Name", "Version") DO NOTHING;

INSERT INTO "AgentScenarios" ("ScenarioSetId", "Key", "Request", "PositionLat", "PositionLng", "ExpectationsJson", "SortOrder")
SELECT s."Id", v.key, v.request, v.lat, v.lng, v.expectations, v.sort
FROM "AgentScenarioSets" s JOIN "AgentExercises" e ON e."Id" = s."ExerciseId"
CROSS JOIN (VALUES
    ('practice-vegan-business', $seed$Cheap vegan place near Millennium Park, open now, quiet enough for a business chat$seed$, NULL, NULL, $seed${"expectStatus": "ok", "expectTop1In": ["ChIJPxSproutHouse", "ChIJPxQuietLeaf"], "mustNotInclude": ["ChIJPxChopHouse", "ChIJPxMorningGlory", "ChIJPxDeepDishDepot", "ChIJPxTacoRelay"], "mustNotBeInTop3": ["ChIJPxBeetClub", "ChIJPxTinyGreens", "ChIJPxVerdant", "ChIJPxFarNorthVegan"], "requirements": {"expectStatus": "R13", "expectTop1In": "R3", "mustNotInclude": "R7, R8", "mustNotBeInTop3": "R3, R4, R5"}}$seed$, 1),
    ('practice-vegan-business-with-position', $seed$Cheap vegan place near me, open now, quiet enough for a business chat$seed$, 41.8826, -87.6226, $seed${"expectStatus": "ok", "expectTop1In": ["ChIJPxSproutHouse", "ChIJPxQuietLeaf"], "mustNotInclude": ["ChIJPxChopHouse", "ChIJPxMorningGlory", "ChIJPxDeepDishDepot", "ChIJPxTacoRelay"], "mustNotBeInTop3": ["ChIJPxBeetClub", "ChIJPxTinyGreens", "ChIJPxVerdant", "ChIJPxFarNorthVegan"], "expectGeocodeCall": false, "requirements": {"expectStatus": "R13", "expectTop1In": "R3", "mustNotInclude": "R7, R8", "mustNotBeInTop3": "R3, R4, R5", "expectGeocodeCall": "R11"}}$seed$, 2),
    ('practice-peruvian-relax-distance', $seed$Peruvian food within a 5 minute walk of Millennium Park, open now$seed$, NULL, NULL, $seed${"expectStatus": "ok", "minResults": 1, "expectTop1In": ["ChIJPxAndesTable"], "expectRelaxed": ["distance"], "allResultsIn": ["ChIJPxAndesTable"], "requirements": {"expectStatus": "R13", "minResults": "R1", "expectTop1In": "R3", "expectRelaxed": "R9", "allResultsIn": "R3"}}$seed$, 3),
    ('practice-ambiguous-location', $seed$Somewhere romantic for dinner near where I'm staying tonight$seed$, NULL, NULL, $seed${"expectStatus": "needs_clarification", "expectNoMapsCalls": true, "requirements": {"expectStatus": "R10", "expectNoMapsCalls": "R10"}}$seed$, 4)
) AS v(key, request, lat, lng, expectations, sort)
WHERE e."Key" = 'dinner-scout' AND s."Name" = 'Dinner Scout practice' AND s."Version" = 1
ON CONFLICT ("ScenarioSetId", "Key") DO NOTHING;

INSERT INTO "ProjectAgentScenarioSets" ("InstituteProjectId", "ScenarioSetId", "Purpose")
SELECT 84, s."Id", 'practice'
FROM "AgentScenarioSets" s JOIN "AgentExercises" e ON e."Id" = s."ExerciseId"
WHERE e."Key" = 'dinner-scout' AND s."Name" = 'Dinner Scout practice' AND s."Version" = 1
ON CONFLICT ("InstituteProjectId", "Purpose") DO UPDATE SET "ScenarioSetId" = EXCLUDED."ScenarioSetId";

COMMIT;


-- =============================================================================
-- Verify (expect the practice set published with 4 scenarios, linked to project 84 as 'practice')
-- =============================================================================
-- SELECT l."InstituteProjectId", l."Purpose", s."Name", s."Version", s."Status", w."Key" AS world
--   FROM "ProjectAgentScenarioSets" l JOIN "AgentScenarioSets" s ON s."Id" = l."ScenarioSetId" JOIN "AgentWorlds" w ON w."Id" = s."WorldId"
--  WHERE l."InstituteProjectId" = 84;


-- =============================================================================
-- R. ROLLBACK
-- =============================================================================
-- BEGIN;
-- DELETE FROM "ProjectAgentScenarioSets" WHERE "InstituteProjectId" = 84 AND "Purpose" = 'practice';
-- DELETE FROM "AgentGradingReports" WHERE "ScenarioSetId" IN (SELECT "Id" FROM "AgentScenarioSets" WHERE "Name" = 'Dinner Scout practice');
-- DELETE FROM "AgentScenarioSets" WHERE "Name" = 'Dinner Scout practice';
-- DELETE FROM "AgentWorlds" WHERE "Key" = 'chicago-practice-1';
-- COMMIT;
