using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace strAppersBackend.Services.GoogleProxy
{
    public class FixtureWorld
    {
        public string WorldId { get; set; } = "";
        public string? Description { get; set; }
        public List<FixtureGeocode> Geocode { get; set; } = new();
        public List<FixturePlace> Places { get; set; } = new();
        // "grading" (expected answers per scenario) is read by the grader only; the proxy never returns it.
    }

    public class FixtureGeocode
    {
        public List<string> Match { get; set; } = new();
        public string FormattedAddress { get; set; } = "";
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string PlaceId { get; set; } = "";
    }

    public class FixturePlace
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string FormattedAddress { get; set; } = "";
        public double Lat { get; set; }
        public double Lng { get; set; }
        public double? Rating { get; set; }
        public int? UserRatingCount { get; set; }
        public string? PriceLevel { get; set; }
        public bool OpenNow { get; set; }
        public bool? ServesVegetarianFood { get; set; }
        public List<string> Types { get; set; } = new();
        public List<string> Keywords { get; set; } = new();
        public List<FixtureReview> Reviews { get; set; } = new();
    }

    public class FixtureReview
    {
        public int Rating { get; set; }
        public string Text { get; set; } = "";
        public string RelativeTime { get; set; } = "a month ago";
    }

    /// <summary>
    /// Answers Geocoding, Directions and Places (New) calls from a fixture world instead of Google, in the same response shapes,
    /// so student code behaves identically. Supports exactly the request fields listed in the Integration Sheet.
    /// Travel times are computed from straight-line distance (x1.3 route factor): walking 80 m/min, driving 250 m/min + 2 min.
    /// </summary>
    public class GoogleProxyFixtureEngine
    {
        private const double RouteFactor = 1.3;
        private const double WalkMetersPerMinute = 80;
        private const double DriveMetersPerMinute = 250;
        private const double DriveOverheadMinutes = 2;

        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };
        private static readonly Regex LatLngPattern = new(@"^\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*$", RegexOptions.Compiled);

        // Words that say nothing about what kind of place is wanted; everything else in textQuery must match a place's keywords, types or name.
        private static readonly HashSet<string> IgnoredQueryWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "of", "in", "at", "on", "to", "for", "with", "by", "me", "my", "some", "any",
            "near", "nearby", "around", "close", "closest", "walking", "distance", "within", "from",
            "restaurant", "restaurants", "food", "place", "places", "spot", "spots", "eat", "eating", "dinner", "lunch", "breakfast", "meal",
            "best", "good", "great", "top", "nice", "cheap", "affordable", "inexpensive", "expensive", "fancy", "open", "now", "tonight", "today"
        };

        private readonly string _fixturesPath;
        private readonly ILogger<GoogleProxyFixtureEngine> _logger;
        private readonly ConcurrentDictionary<string, FixtureWorld?> _worlds = new();

        public GoogleProxyFixtureEngine(ILogger<GoogleProxyFixtureEngine> logger)
        {
            _fixturesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoogleProxyFixtures");
            _logger = logger;
        }

        public FixtureWorld? GetWorld(string worldId) => _worlds.GetOrAdd(worldId, LoadWorld);

        /// <summary>The fixture response, or null when this service is not simulated (Gemini, Speech stay live).</summary>
        public (int Status, string Json)? Handle(FixtureWorld world, string service, string method, string path, IEnumerable<KeyValuePair<string, string>> query, byte[] body, string? fieldMask)
        {
            if (service == "maps" && path == "maps/api/geocode/json") return Geocode(world, query);
            if (service == "maps" && path == "maps/api/directions/json") return Directions(world, query);
            if (service == "places" && method == "POST" && path == "v1/places:searchText") return SearchText(world, body, fieldMask);
            if (service == "places" && method == "GET" && path.StartsWith("v1/places/", StringComparison.Ordinal)) return Details(world, path["v1/places/".Length..], fieldMask);
            return null;
        }

        private FixtureWorld? LoadWorld(string worldId)
        {
            var file = Path.Combine(_fixturesPath, worldId + ".json");
            if (!File.Exists(file)) return null;
            try { return JsonSerializer.Deserialize<FixtureWorld>(File.ReadAllText(file), ReadOptions); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GoogleProxy] Fixture world {WorldId} is not valid JSON", worldId);
                return null;
            }
        }

        // ---------- Geocoding ----------

        private static (int, string) Geocode(FixtureWorld world, IEnumerable<KeyValuePair<string, string>> query)
        {
            var address = QueryValue(query, "address");
            if (string.IsNullOrWhiteSpace(address))
                return (200, Json(new JsonObject { ["results"] = new JsonArray(), ["status"] = "INVALID_REQUEST", ["error_message"] = "Missing the 'address' parameter." }));

            var hit = MatchGeocode(world, address);
            if (hit == null) return (200, Json(new JsonObject { ["results"] = new JsonArray(), ["status"] = "ZERO_RESULTS" }));

            return (200, Json(new JsonObject
            {
                ["results"] = new JsonArray(new JsonObject
                {
                    ["formatted_address"] = hit.FormattedAddress,
                    ["geometry"] = new JsonObject { ["location"] = LatLng(hit.Lat, hit.Lng), ["location_type"] = "APPROXIMATE" },
                    ["place_id"] = hit.PlaceId,
                    ["types"] = new JsonArray("point_of_interest")
                }),
                ["status"] = "OK"
            }));
        }

        private static FixtureGeocode? MatchGeocode(FixtureWorld world, string address)
        {
            var normalized = Normalize(address);
            return world.Geocode.FirstOrDefault(g => g.Match.Any(m => normalized.Contains(Normalize(m))));
        }

        // ---------- Directions ----------

        private static (int, string) Directions(FixtureWorld world, IEnumerable<KeyValuePair<string, string>> query)
        {
            var mode = (QueryValue(query, "mode") ?? "driving").ToLowerInvariant();
            if (mode != "walking" && mode != "driving")
                return (200, Json(new JsonObject { ["routes"] = new JsonArray(), ["status"] = "INVALID_REQUEST", ["error_message"] = "Only mode=walking and mode=driving are supported." }));

            var origin = ResolvePoint(world, QueryValue(query, "origin"));
            var destination = ResolvePoint(world, QueryValue(query, "destination"));
            if (origin == null || destination == null)
                return (200, Json(new JsonObject { ["routes"] = new JsonArray(), ["status"] = "NOT_FOUND" }));

            var routeMeters = DistanceMeters(origin.Value.Lat, origin.Value.Lng, destination.Value.Lat, destination.Value.Lng) * RouteFactor;
            var minutes = mode == "walking" ? routeMeters / WalkMetersPerMinute : routeMeters / DriveMetersPerMinute + DriveOverheadMinutes;
            var seconds = (int)Math.Round(minutes * 60);
            var displayMinutes = Math.Max(1, (int)Math.Round(minutes));

            return (200, Json(new JsonObject
            {
                ["geocoded_waypoints"] = new JsonArray(),
                ["routes"] = new JsonArray(new JsonObject
                {
                    ["summary"] = "Simulated route",
                    ["legs"] = new JsonArray(new JsonObject
                    {
                        ["distance"] = new JsonObject { ["text"] = (routeMeters / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " km", ["value"] = (int)Math.Round(routeMeters) },
                        ["duration"] = new JsonObject { ["text"] = displayMinutes + (displayMinutes == 1 ? " min" : " mins"), ["value"] = seconds },
                        ["start_address"] = origin.Value.Address,
                        ["end_address"] = destination.Value.Address,
                        ["start_location"] = LatLng(origin.Value.Lat, origin.Value.Lng),
                        ["end_location"] = LatLng(destination.Value.Lat, destination.Value.Lng)
                    })
                }),
                ["status"] = "OK"
            }));
        }

        private static (double Lat, double Lng, string Address)? ResolvePoint(FixtureWorld world, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (value.StartsWith("place_id:", StringComparison.OrdinalIgnoreCase))
            {
                var id = value["place_id:".Length..].Trim();
                var place = world.Places.FirstOrDefault(p => p.Id == id);
                if (place != null) return (place.Lat, place.Lng, place.FormattedAddress);
                var anchor = world.Geocode.FirstOrDefault(g => g.PlaceId == id);
                return anchor == null ? null : (anchor.Lat, anchor.Lng, anchor.FormattedAddress);
            }
            var m = LatLngPattern.Match(value);
            if (m.Success)
            {
                var lat = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var lng = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                return (lat, lng, value.Trim());
            }
            var byName = world.Places.FirstOrDefault(p => Normalize(value).Contains(Normalize(p.DisplayName)));
            if (byName != null) return (byName.Lat, byName.Lng, byName.FormattedAddress);
            var hit = MatchGeocode(world, value);
            return hit == null ? null : (hit.Lat, hit.Lng, hit.FormattedAddress);
        }

        // ---------- Places (New) ----------

        private static (int, string) SearchText(FixtureWorld world, byte[] body, string? fieldMask)
        {
            if (string.IsNullOrWhiteSpace(fieldMask)) return FieldMaskMissing();
            try { return SearchTextCore(world, JsonNode.Parse(Encoding.UTF8.GetString(body)), fieldMask); }
            // Malformed JSON or a field of the wrong type ("openNow": "yes"): Google answers 400, so do we.
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException || ex is FormatException)
            {
                return PlacesError(400, "INVALID_ARGUMENT", "Invalid JSON payload received. " + ex.Message);
            }
        }

        private static (int, string) SearchTextCore(FixtureWorld world, JsonNode? request, string fieldMask)
        {
            var textQuery = request?["textQuery"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(textQuery)) return PlacesError(400, "INVALID_ARGUMENT", "Empty text_query.");

            IEnumerable<FixturePlace> candidates = world.Places;

            var circle = request?["locationBias"]?["circle"];
            if (circle != null)
            {
                var lat = circle["center"]?["latitude"]?.GetValue<double>();
                var lng = circle["center"]?["longitude"]?.GetValue<double>();
                var radius = circle["radius"]?.GetValue<double>() ?? 0;
                if (lat != null && lng != null && radius > 0)
                    candidates = candidates.Where(p => DistanceMeters(lat.Value, lng.Value, p.Lat, p.Lng) <= radius);
            }

            var includedType = request?["includedType"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(includedType))
                candidates = candidates.Where(p => p.Types.Contains(includedType, StringComparer.OrdinalIgnoreCase));

            if (request?["openNow"]?.GetValue<bool>() == true)
                candidates = candidates.Where(p => p.OpenNow);

            var priceLevels = request?["priceLevels"]?.AsArray().Select(n => n?.GetValue<string>()).Where(s => s != null).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (priceLevels != null && priceLevels.Count > 0)
                candidates = candidates.Where(p => p.PriceLevel != null && priceLevels.Contains(p.PriceLevel));

            var minRating = request?["minRating"]?.GetValue<double>();
            if (minRating != null)
                candidates = candidates.Where(p => (p.Rating ?? 0) >= minRating.Value);

            // Relevance: count query words found in the place's keywords, types or name. If no word matches any
            // candidate, the query is generic ("food near X") and every remaining candidate is returned.
            var words = Tokenize(textQuery).Where(w => !IgnoredQueryWords.Contains(w)).Distinct().ToList();
            var scored = candidates.Select(p => (Place: p, Score: words.Count(w => PlaceTokens(p).Contains(w)))).ToList();
            if (scored.Any(s => s.Score > 0)) scored = scored.Where(s => s.Score > 0).ToList();

            var pageSize = Math.Clamp(request?["pageSize"]?.GetValue<int>() ?? 20, 1, 20);
            var results = scored
                .OrderByDescending(s => s.Score).ThenByDescending(s => s.Place.Rating ?? 0).ThenBy(s => s.Place.Id, StringComparer.Ordinal)
                .Take(pageSize)
                .Select(s => (JsonNode)BuildPlace(s.Place, ParseFieldMask(fieldMask, "places.")))
                .ToArray();

            return (200, Json(results.Length == 0 ? new JsonObject() : new JsonObject { ["places"] = new JsonArray(results) }));
        }

        private static (int, string) Details(FixtureWorld world, string placeId, string? fieldMask)
        {
            if (string.IsNullOrWhiteSpace(fieldMask)) return FieldMaskMissing();
            var place = world.Places.FirstOrDefault(p => p.Id == placeId);
            if (place == null) return PlacesError(404, "NOT_FOUND", $"Place '{placeId}' not found.");
            return (200, Json(BuildPlace(place, ParseFieldMask(fieldMask, ""))));
        }

        /// <summary>Top-level field names from an X-Goog-FieldMask; null means all fields ("*").</summary>
        private static HashSet<string>? ParseFieldMask(string fieldMask, string prefix)
        {
            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in fieldMask.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (raw == "*" || raw == prefix + "*") return null;
                if (prefix.Length > 0 && !raw.StartsWith(prefix, StringComparison.Ordinal)) continue;
                fields.Add(raw[prefix.Length..].Split('.')[0]);
            }
            return fields;
        }

        private static JsonObject BuildPlace(FixturePlace p, HashSet<string>? fields)
        {
            bool Want(string f) => fields == null || fields.Contains(f);
            var o = new JsonObject();
            if (Want("name")) o["name"] = "places/" + p.Id;
            if (Want("id")) o["id"] = p.Id;
            if (Want("types")) o["types"] = new JsonArray(p.Types.Select(t => (JsonNode)t).ToArray());
            if (Want("formattedAddress")) o["formattedAddress"] = p.FormattedAddress;
            if (Want("location")) o["location"] = new JsonObject { ["latitude"] = p.Lat, ["longitude"] = p.Lng };
            if (Want("rating") && p.Rating != null) o["rating"] = p.Rating;
            if (Want("userRatingCount") && p.UserRatingCount != null) o["userRatingCount"] = p.UserRatingCount;
            if (Want("priceLevel") && p.PriceLevel != null) o["priceLevel"] = p.PriceLevel;
            if (Want("displayName")) o["displayName"] = new JsonObject { ["text"] = p.DisplayName, ["languageCode"] = "en" };
            if (Want("currentOpeningHours")) o["currentOpeningHours"] = new JsonObject { ["openNow"] = p.OpenNow };
            if (Want("servesVegetarianFood") && p.ServesVegetarianFood != null) o["servesVegetarianFood"] = p.ServesVegetarianFood;
            if (Want("reviews") && p.Reviews.Count > 0)
            {
                o["reviews"] = new JsonArray(p.Reviews.Select((r, i) => (JsonNode)new JsonObject
                {
                    ["name"] = $"places/{p.Id}/reviews/r{i + 1}",
                    ["relativePublishTimeDescription"] = r.RelativeTime,
                    ["rating"] = r.Rating,
                    ["text"] = new JsonObject { ["text"] = r.Text, ["languageCode"] = "en" },
                    ["originalText"] = new JsonObject { ["text"] = r.Text, ["languageCode"] = "en" }
                }).ToArray());
            }
            return o;
        }

        private static (int, string) FieldMaskMissing() =>
            PlacesError(400, "INVALID_ARGUMENT", "FieldMask is a required parameter. See how to provide it at https://developers.google.com/maps/documentation/places/web-service/choose-fields");

        private static (int, string) PlacesError(int code, string status, string message) =>
            (code, Json(new JsonObject { ["error"] = new JsonObject { ["code"] = code, ["message"] = message, ["status"] = status } }));

        // ---------- helpers ----------

        private static HashSet<string> PlaceTokens(FixturePlace p) =>
            p.Keywords.Concat(p.Types).Append(p.DisplayName).SelectMany(Tokenize).ToHashSet(StringComparer.OrdinalIgnoreCase);

        private static IEnumerable<string> Tokenize(string text) =>
            Regex.Split(text.ToLowerInvariant(), "[^a-z0-9]+").Where(t => t.Length > 0).Select(t => t.Length > 3 && t.EndsWith('s') ? t[..^1] : t);

        private static string Normalize(string text) => string.Join(" ", Tokenize(text));

        private static string? QueryValue(IEnumerable<KeyValuePair<string, string>> query, string name) =>
            query.FirstOrDefault(q => q.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

        private static JsonObject LatLng(double lat, double lng) => new() { ["lat"] = lat, ["lng"] = lng };

        private static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371000;
            double ToRad(double d) => d * Math.PI / 180;
            var dLat = ToRad(lat2 - lat1);
            var dLng = ToRad(lng2 - lng1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return 2 * R * Math.Asin(Math.Sqrt(a));
        }

        private static string Json(JsonNode node) => node.ToJsonString();
    }
}
