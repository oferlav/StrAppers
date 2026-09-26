using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Services.GoogleProxy
{
    /// <summary>
    /// One scenario as the grader uses it: AgentScenarios.Key/Request/Position plus the parsed ExpectationsJson.
    /// <see cref="Requirements"/> maps an expectation name (e.g. "mustNotInclude") to the requirement code it enforces.
    /// </summary>
    public class GradingScenario
    {
        public string Id { get; set; } = "";
        public string Request { get; set; } = "";
        public GradingPosition? Position { get; set; }
        public string ExpectStatus { get; set; } = "ok";
        /// <summary>Fewest results accepted when status is ok. The contract says 3, fewer only when fewer genuinely exist in the world.</summary>
        public int MinResults { get; set; } = 3;
        public List<string> ExpectTop1In { get; set; } = new();
        public List<string> MustNotInclude { get; set; } = new();
        public List<string> MustNotBeInTop3 { get; set; } = new();
        public List<string> ExpectRelaxed { get; set; } = new();
        public bool? ExpectGeocodeCall { get; set; }
        public bool? ExpectNoMapsCalls { get; set; }
        public Dictionary<string, string> Requirements { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>AgentExercises.LimitsJson. Defaults are the Dinner Scout Integration Sheet values.</summary>
    public class ExerciseLimits
    {
        public int MaxLatencyMs { get; set; } = 20_000;
        public int MaxDetailsCalls { get; set; } = 5;
        public int MinGeminiCallsWhenOk { get; set; } = 2;
        public int MaxWhyChars { get; set; } = 200;
        public int MaxResults { get; set; } = 5;
        /// <summary>Check-id prefix (e.g. "grounding", "behavior.agentLoop") to requirement code, for checks every scenario runs.</summary>
        public Dictionary<string, string> CheckRequirements { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Everything one grading needs, resolved from the DB: board, project, scenario set, exercise, world, scenarios.</summary>
    public class GradingPlan
    {
        public string BoardId { get; set; } = "";
        public int InstituteProjectId { get; set; }
        public string Purpose { get; set; } = "";
        public int ScenarioSetId { get; set; }
        public string ScenarioSetName { get; set; } = "";
        public int ScenarioSetVersion { get; set; }
        public string ExerciseKey { get; set; } = "";
        public string EndpointPath { get; set; } = "";
        public ExerciseLimits Limits { get; set; } = new();
        public FixtureWorld World { get; set; } = new();
        public List<GradingScenario> Scenarios { get; set; } = new();
    }

    public class GradingPosition
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    public class GradingCheck
    {
        public string Id { get; set; } = "";
        public string Category { get; set; } = "";
        /// <summary>A failed critical check (grounding, hard rules, broken contract) fails the whole scenario, whatever the other runs did.</summary>
        public bool Critical { get; set; }
        public bool Passed { get; set; }
        public string Detail { get; set; } = "";
        /// <summary>The requirement code this check enforces, so a failure traces back to what a persona said.</summary>
        public string? Requirement { get; set; }
    }

    public class GradingRunResult
    {
        public string RunId { get; set; } = "";
        public int? HttpStatus { get; set; }
        public long LatencyMs { get; set; }
        public bool Passed { get; set; }
        public bool CriticalFailure { get; set; }
        public Dictionary<string, int> CallCounts { get; set; } = new();
        public List<GradingCheck> Checks { get; set; } = new();
        public string? Response { get; set; }
    }

    public class GradingScenarioResult
    {
        public string Id { get; set; } = "";
        public string Request { get; set; } = "";
        public bool Passed { get; set; }
        public int PassedRuns { get; set; }
        public List<GradingRunResult> Runs { get; set; } = new();
    }

    public class GradingReport
    {
        public string GradingId { get; set; } = "";
        public string BoardId { get; set; } = "";
        public int InstituteProjectId { get; set; }
        public string Purpose { get; set; } = "";
        public int ScenarioSetId { get; set; }
        public string ScenarioSetName { get; set; } = "";
        public int ScenarioSetVersion { get; set; }
        public string ExerciseKey { get; set; } = "";
        public string WorldKey { get; set; } = "";
        public string BackendUrl { get; set; } = "";
        /// <summary>running | completed | failed</summary>
        public string Status { get; set; } = "running";
        public string? Error { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public int TotalScenarios { get; set; }
        public int PassedScenarios { get; set; }
        public int Score { get; set; }
        public bool CriticalFailure { get; set; }
        public List<GradingScenarioResult> Scenarios { get; set; } = new();
    }

    /// <summary>
    /// Grades a student's agent backend at the API level. The board's project resolves to its graded (or practice)
    /// scenario set in the DB; each scenario runs against POST {backend}{AgentExercises.EndpointPath} with a fresh signed
    /// fixture run id, then the response is checked against the exercise contract and against the proxy's log of that
    /// same run. Implementation-independent: only the HTTP contract and the Google calls the proxy saw are graded.
    /// Runs in the background; running reports live in memory and every report is saved to AgentGradingReports.
    /// </summary>
    public class AgentGrader
    {
        public static readonly JsonSerializerOptions ReportJsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

        // Scenario expectations and the checks that enforce them (for requirement tagging).
        private static readonly (string CheckPrefix, string Expectation)[] ExpectationChecks =
        {
            ("hardRule.mustNotInclude", "mustNotInclude"), ("behavior.status", "expectStatus"), ("behavior.geocode", "expectGeocodeCall"),
            ("behavior.noMapsCalls", "expectNoMapsCalls"), ("behavior.relaxed", "expectRelaxed"), ("ranking.top1", "expectTop1In"),
            ("ranking.trapsOutOfTop3", "mustNotBeInTop3"), ("contract.resultCount", "minResults")
        };

        private const int MaxStoredResponseChars = 20_000;
        private static readonly TimeSpan ReportRetention = TimeSpan.FromHours(24);
        private static readonly HashSet<string> Statuses = new() { "ok", "needs_clarification", "no_results" };
        private static readonly HashSet<string> RelaxableConstraints = new() { "distance", "price", "rating" };
        private static readonly HashSet<string> TravelModes = new() { "walking", "driving" };

        private readonly GoogleProxyConfig _config;
        private readonly GoogleProxyRunStore _runStore;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AgentGrader> _logger;
        private readonly ConcurrentDictionary<string, GradingReport> _reports = new();

        public AgentGrader(IOptions<GoogleProxyConfig> config, GoogleProxyRunStore runStore, IServiceScopeFactory scopeFactory,
            IHttpClientFactory httpClientFactory, ILogger<AgentGrader> logger)
        {
            _config = config.Value;
            _runStore = runStore;
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Resolves board → institute project → its scenario set for <paramref name="purpose"/> → exercise, world and scenarios.
        /// Only published sets are graded. Throws ArgumentException with a readable reason when anything is missing.
        /// </summary>
        public static async Task<GradingPlan> ResolvePlanAsync(ApplicationDbContext db, string boardId, string purpose,
            IReadOnlyCollection<string>? scenarioKeys, CancellationToken ct = default)
        {
            var board = await db.ProjectBoards.AsNoTracking().Where(b => b.Id == boardId)
                .Select(b => new { b.InstituteProjectId }).FirstOrDefaultAsync(ct)
                ?? throw new ArgumentException($"Board '{boardId}' not found.");
            if (board.InstituteProjectId == null)
                throw new ArgumentException($"Board '{boardId}' has no institute project.");

            var link = await db.ProjectAgentScenarioSets.AsNoTracking()
                .Include(l => l.ScenarioSet).ThenInclude(s => s.Exercise)
                .Include(l => l.ScenarioSet).ThenInclude(s => s.World)
                .Include(l => l.ScenarioSet).ThenInclude(s => s.Scenarios)
                .FirstOrDefaultAsync(l => l.InstituteProjectId == board.InstituteProjectId && l.Purpose == purpose, ct)
                ?? throw new ArgumentException($"Institute project {board.InstituteProjectId} has no {purpose} scenario set.");
            var set = link.ScenarioSet;
            if (set.Status != AgentScenarioSet.StatusPublished)
                throw new ArgumentException($"Scenario set '{set.Name}' v{set.Version} is '{set.Status}'; only published sets can be graded.");

            var world = GoogleProxyFixtureEngine.ParseWorld(set.World.Key, set.World.DataJson)
                ?? throw new ArgumentException($"World '{set.World.Key}' has invalid DataJson.");
            var limits = string.IsNullOrWhiteSpace(set.Exercise.LimitsJson)
                ? new ExerciseLimits()
                : JsonSerializer.Deserialize<ExerciseLimits>(set.Exercise.LimitsJson, ReadOptions) ?? new ExerciseLimits();
            limits.CheckRequirements = new Dictionary<string, string>(limits.CheckRequirements, StringComparer.OrdinalIgnoreCase);

            var scenarios = set.Scenarios
                .Where(s => scenarioKeys == null || scenarioKeys.Count == 0 || scenarioKeys.Contains(s.Key))
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                .Select(ToGradingScenario)
                .ToList();
            if (scenarios.Count == 0) throw new ArgumentException("No grading scenarios selected.");

            return new GradingPlan
            {
                BoardId = boardId,
                InstituteProjectId = board.InstituteProjectId.Value,
                Purpose = purpose,
                ScenarioSetId = set.Id,
                ScenarioSetName = set.Name,
                ScenarioSetVersion = set.Version,
                ExerciseKey = set.Exercise.Key,
                EndpointPath = "/" + set.Exercise.EndpointPath.TrimStart('/'),
                Limits = limits,
                World = world,
                Scenarios = scenarios
            };
        }

        private static GradingScenario ToGradingScenario(AgentScenario row)
        {
            GradingScenario scenario;
            try { scenario = JsonSerializer.Deserialize<GradingScenario>(row.ExpectationsJson, ReadOptions) ?? new GradingScenario(); }
            catch (JsonException ex) { throw new ArgumentException($"Scenario '{row.Key}' has invalid ExpectationsJson: {ex.Message}"); }
            scenario.Id = row.Key;
            scenario.Request = row.Request;
            scenario.Position = row.PositionLat != null && row.PositionLng != null
                ? new GradingPosition { Lat = row.PositionLat.Value, Lng = row.PositionLng.Value }
                : null;
            scenario.Requirements = new Dictionary<string, string>(scenario.Requirements, StringComparer.OrdinalIgnoreCase);
            return scenario;
        }

        /// <summary>The report as JSON: from memory while it exists there (serialized under its lock), else from AgentGradingReports.</summary>
        public async Task<string?> GetReportJsonAsync(string gradingId, CancellationToken ct = default)
        {
            if (_reports.TryGetValue(gradingId, out var report))
                lock (report) return JsonSerializer.Serialize(report, ReportJsonOptions);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.AgentGradingReports.AsNoTracking().FirstOrDefaultAsync(r => r.GradingId == gradingId, ct);
            if (row == null) return null;
            return row.ReportJson ?? JsonSerializer.Serialize(new { gradingId = row.GradingId, boardId = row.BoardId, status = row.Status }, ReportJsonOptions);
        }

        /// <summary>Saves a "running" report row, then grades in the background.</summary>
        public async Task<GradingReport> StartAsync(GradingPlan plan, string backendUrl, int repetitions, CancellationToken ct = default)
        {
            foreach (var old in _reports.Where(r => r.Value.StartedAt < DateTime.UtcNow - ReportRetention).Select(r => r.Key).ToList())
                _reports.TryRemove(old, out _);

            var report = new GradingReport
            {
                GradingId = Guid.NewGuid().ToString("N"),
                BoardId = plan.BoardId,
                InstituteProjectId = plan.InstituteProjectId,
                Purpose = plan.Purpose,
                ScenarioSetId = plan.ScenarioSetId,
                ScenarioSetName = plan.ScenarioSetName,
                ScenarioSetVersion = plan.ScenarioSetVersion,
                ExerciseKey = plan.ExerciseKey,
                WorldKey = plan.World.WorldId,
                BackendUrl = backendUrl,
                StartedAt = DateTime.UtcNow,
                TotalScenarios = plan.Scenarios.Count
            };

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.AgentGradingReports.Add(new AgentGradingReport
                {
                    GradingId = report.GradingId, BoardId = report.BoardId, ScenarioSetId = report.ScenarioSetId,
                    Status = report.Status, StartedAt = report.StartedAt
                });
                await db.SaveChangesAsync(ct);
            }
            _reports[report.GradingId] = report;

            _ = Task.Run(async () =>
            {
                try { await RunAsync(report, plan, Math.Clamp(repetitions, 1, 5)); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AgentGrader] Grading {GradingId} crashed", report.GradingId);
                    lock (report)
                    {
                        report.Status = "failed";
                        report.Error = ex.Message;
                        report.FinishedAt = DateTime.UtcNow;
                    }
                }
                await SaveReportAsync(report);
            });
            return report;
        }

        /// <summary>Writes the finished (or failed) report to its AgentGradingReports row. Never throws: the in-memory copy stays readable.</summary>
        private async Task SaveReportAsync(GradingReport report)
        {
            try
            {
                string json;
                lock (report) json = JsonSerializer.Serialize(report, ReportJsonOptions);
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var row = await db.AgentGradingReports.FirstOrDefaultAsync(r => r.GradingId == report.GradingId);
                if (row == null) return;
                row.Status = report.Status;
                row.Score = report.Status == "completed" ? report.Score : null;
                row.CriticalFailure = report.Status == "completed" ? report.CriticalFailure : null;
                row.ReportJson = json;
                row.FinishedAt = report.FinishedAt;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AgentGrader] Could not save report {GradingId}", report.GradingId);
            }
        }

        private async Task RunAsync(GradingReport report, GradingPlan plan, int repetitions)
        {
            var client = _httpClientFactory.CreateClient("AgentGrader");

            // Preflight: a backend that is not in proxy mode calls Google directly, so the proxy sees nothing and grounding cannot be checked.
            var mode = await GetBackendModeAsync(client, report.BackendUrl);
            if (mode != "proxy")
            {
                lock (report)
                {
                    report.Status = "failed";
                    report.Error = $"The backend is not using the platform proxy (GET /api/google/status mode = '{mode ?? "unreachable"}').";
                    report.FinishedAt = DateTime.UtcNow;
                }
                return;
            }

            foreach (var scenario in plan.Scenarios)
            {
                var runs = new List<GradingRunResult>();
                for (var i = 0; i < repetitions; i++)
                    runs.Add(await GradeRunAsync(client, report, plan, scenario));

                var passedRuns = runs.Count(r => r.Passed);
                lock (report)
                {
                    report.Scenarios.Add(new GradingScenarioResult
                    {
                        Id = scenario.Id,
                        Request = scenario.Request,
                        Runs = runs,
                        PassedRuns = passedRuns,
                        // Majority of runs must pass (LLM output varies), and no run may break a critical rule.
                        Passed = passedRuns * 2 > runs.Count && runs.All(r => !r.CriticalFailure)
                    });
                }
            }

            lock (report)
            {
                report.PassedScenarios = report.Scenarios.Count(s => s.Passed);
                report.Score = (int)Math.Round(100.0 * report.PassedScenarios / report.TotalScenarios);
                report.CriticalFailure = report.Scenarios.Any(s => s.Runs.Any(r => r.CriticalFailure));
                report.Status = "completed";
                report.FinishedAt = DateTime.UtcNow;
            }
        }

        private static async Task<string?> GetBackendModeAsync(HttpClient client, string backendUrl)
        {
            try
            {
                var text = await client.GetStringAsync(backendUrl + "/api/google/status");
                return JsonNode.Parse(text)?["mode"]?.GetValue<string>() ?? "direct";
            }
            catch (Exception) { return null; }
        }

        private async Task<GradingRunResult> GradeRunAsync(HttpClient client, GradingReport report, GradingPlan plan, GradingScenario scenario)
        {
            var runId = GoogleProxyFixtureRuns.Create(report.BoardId, plan.World.WorldId, _config.TokenSecret, DateTimeOffset.UtcNow);
            var result = new GradingRunResult { RunId = runId };
            var checks = result.Checks;

            var payload = new JsonObject { ["request"] = scenario.Request };
            if (scenario.Position != null) payload["position"] = new JsonObject { ["lat"] = scenario.Position.Lat, ["lng"] = scenario.Position.Lng };

            string? body = null;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, report.BackendUrl + plan.EndpointPath)
                {
                    Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
                };
                request.Headers.TryAddWithoutValidation("X-Run-Id", runId);
                using var response = await client.SendAsync(request);
                result.HttpStatus = (int)response.StatusCode;
                body = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                checks.Add(Check("contract.reachable", "contract", true, false, "The endpoint did not respond: " + ex.Message));
            }
            stopwatch.Stop();
            result.LatencyMs = stopwatch.ElapsedMilliseconds;
            result.Response = body == null ? null : body.Length > MaxStoredResponseChars ? body[..MaxStoredResponseChars] + "...[truncated]" : body;

            var calls = _runStore.GetRun(report.BoardId, runId) ?? new List<GoogleProxyCallLog>();
            result.CallCounts = calls.GroupBy(c => c.Service + " " + c.Method + " " + PathOnly(c.Path)).ToDictionary(g => g.Key, g => g.Count());

            if (body != null) GradeResponse(result, body, plan.World, plan.Limits, scenario, calls);
            TagRequirements(checks, scenario, plan.Limits);
            result.CriticalFailure = checks.Any(c => c.Critical && !c.Passed);
            result.Passed = checks.All(c => c.Passed);
            return result;
        }

        private static void GradeResponse(GradingRunResult result, string body, FixtureWorld world, ExerciseLimits limits, GradingScenario scenario, List<GoogleProxyCallLog> calls)
        {
            var checks = result.Checks;
            checks.Add(Check("contract.http200", "contract", true, result.HttpStatus == 200, $"HTTP {result.HttpStatus}"));
            checks.Add(Check("contract.latency", "contract", false, result.LatencyMs <= limits.MaxLatencyMs, $"{result.LatencyMs} ms (limit {limits.MaxLatencyMs})"));

            JsonObject? json = null;
            try { json = JsonNode.Parse(body) as JsonObject; } catch (JsonException) { }
            checks.Add(Check("contract.json", "contract", true, json != null, json == null ? "Response is not a JSON object." : "ok"));
            if (json == null || result.HttpStatus != 200) return;

            // ---- contract ----
            var status = Str(json["status"]);
            checks.Add(Check("contract.runId", "contract", false, Str(json["runId"]) == result.RunId, $"runId = '{Str(json["runId"])}'"));
            checks.Add(Check("contract.status", "contract", true, status != null && Statuses.Contains(status), $"status = '{status}'"));
            checks.Add(Check("contract.message", "contract", false, !string.IsNullOrWhiteSpace(Str(json["message"])), "message must be a non-empty string"));
            var question = Str(json["question"]);
            checks.Add(Check("contract.question", "contract", false,
                status == "needs_clarification" ? !string.IsNullOrWhiteSpace(question) : json["question"] == null,
                "question is a string only when status is needs_clarification, otherwise null"));

            var relaxed = json["relaxed"] as JsonArray;
            var relaxedNames = relaxed?.Select(r => Str(r?["constraint"])).ToList() ?? new List<string?>();
            checks.Add(Check("contract.relaxed", "contract", false, relaxed != null && relaxedNames.All(n => n != null && RelaxableConstraints.Contains(n)),
                relaxed == null ? "relaxed must be an array" : "constraints: " + string.Join(",", relaxedNames)));

            var results = json["results"] as JsonArray;
            checks.Add(Check("contract.results", "contract", true, results != null, results == null ? "results must be an array" : $"{results.Count} results"));
            if (results == null) return;
            var items = results.OfType<JsonObject>().ToList();

            if (status == "ok")
            {
                checks.Add(Check("contract.resultCount", "contract", false, items.Count >= scenario.MinResults && items.Count <= limits.MaxResults, $"{items.Count} results (expected {scenario.MinResults} to {limits.MaxResults})"));
                var trace = json["trace"] as JsonArray;
                checks.Add(Check("contract.trace", "contract", false, trace != null && trace.Count > 0, "trace must be a non-empty array when status is ok"));
            }
            else
            {
                checks.Add(Check("contract.resultCount", "contract", false, items.Count == 0, $"{items.Count} results (expected none when status is {status})"));
            }

            var ranks = items.Select(i => Int(i["rank"])).ToList();
            checks.Add(Check("contract.ranks", "contract", false, ranks.SequenceEqual(Enumerable.Range(1, items.Count).Select(n => (int?)n)),
                "ranks: " + string.Join(",", ranks.Select(r => r?.ToString() ?? "null"))));

            var fieldProblems = new List<string>();
            foreach (var item in items)
            {
                var id = Str(item["placeId"]) ?? "?";
                if (string.IsNullOrWhiteSpace(Str(item["placeId"]))) fieldProblems.Add("missing placeId");
                if (string.IsNullOrWhiteSpace(Str(item["name"]))) fieldProblems.Add(id + ": missing name");
                if (Int(item["travelMinutes"]) == null) fieldProblems.Add(id + ": travelMinutes must be an integer");
                if (!TravelModes.Contains(Str(item["travelMode"]) ?? "")) fieldProblems.Add(id + ": travelMode must be walking or driving");
                var why = Str(item["why"]);
                if (string.IsNullOrWhiteSpace(why) || why.Length > limits.MaxWhyChars) fieldProblems.Add(id + $": why must be 1 to {limits.MaxWhyChars} characters");
            }
            checks.Add(Check("contract.resultFields", "contract", false, fieldProblems.Count == 0, fieldProblems.Count == 0 ? "ok" : string.Join("; ", fieldProblems)));

            // ---- grounding: every recommendation must come from this run's Google calls ----
            var placesResponses = calls.Where(c => c.Service == "places" && c.Status == 200).Select(c => c.ResponseBody).ToList();
            var directions = calls.Where(c => c.Service == "maps" && PathOnly(c.Path) == "/maps/api/directions/json" && c.Status == 200)
                                  .Select(ParseDirections).Where(d => d != null).Select(d => d!.Value).ToList();

            foreach (var item in items)
            {
                var id = Str(item["placeId"]);
                if (id == null) continue;
                var seen = placesResponses.Any(r => r.Contains("\"" + id + "\"", StringComparison.Ordinal));
                checks.Add(Check($"grounding.found[{id}]", "grounding", true, seen, seen ? "returned by a Places call in this run" : "never returned by any Places call in this run"));

                var place = world.Places.FirstOrDefault(p => p.Id == id);
                if (place == null) continue;

                var mismatches = new List<string>();
                if (Str(item["name"]) != place.DisplayName) mismatches.Add($"name '{Str(item["name"])}' vs '{place.DisplayName}'");
                if (!SameNumber(Dbl(item["rating"]), place.Rating)) mismatches.Add($"rating {Dbl(item["rating"])} vs {place.Rating}");
                if (Int(item["userRatingCount"]) != place.UserRatingCount) mismatches.Add($"userRatingCount {Int(item["userRatingCount"])} vs {place.UserRatingCount}");
                if (Str(item["priceLevel"]) != place.PriceLevel) mismatches.Add($"priceLevel {Str(item["priceLevel"])} vs {place.PriceLevel}");
                checks.Add(Check($"grounding.facts[{id}]", "grounding", true, mismatches.Count == 0, mismatches.Count == 0 ? "matches Places data" : string.Join("; ", mismatches)));

                var minutes = Int(item["travelMinutes"]);
                var mode = Str(item["travelMode"]);
                var legs = directions.Where(d => Math.Abs(d.EndLat - place.Lat) < 0.0001 && Math.Abs(d.EndLng - place.Lng) < 0.0001 && d.Mode == mode).ToList();
                var travelOk = minutes != null && legs.Any(d => Math.Abs(minutes.Value - d.Minutes) <= Math.Max(1.0, d.Minutes * 0.2));
                checks.Add(Check($"grounding.travel[{id}]", "grounding", true, travelOk,
                    legs.Count == 0 ? $"no {mode} Directions call to this place in this run"
                                    : $"travelMinutes {minutes} vs Directions {string.Join("/", legs.Select(l => l.Minutes.ToString("0.#", CultureInfo.InvariantCulture)))}"));
            }

            // ---- hard rules ----
            var ids = items.Select(i => Str(i["placeId"])).Where(i => i != null).Select(i => i!).ToList();
            if (scenario.MustNotInclude.Count > 0)
            {
                var forbidden = ids.Intersect(scenario.MustNotInclude).ToList();
                checks.Add(Check("hardRule.mustNotInclude", "hardRule", true, forbidden.Count == 0, forbidden.Count == 0 ? "ok" : "recommended: " + string.Join(",", forbidden)));
            }

            // ---- behavior, from the proxy log ----
            checks.Add(Check("behavior.status", "behavior", false, status == scenario.ExpectStatus, $"status '{status}', expected '{scenario.ExpectStatus}'"));
            var geocodeCalls = calls.Count(c => c.Service == "maps" && PathOnly(c.Path) == "/maps/api/geocode/json");
            if (scenario.ExpectGeocodeCall != null)
                checks.Add(Check("behavior.geocode", "behavior", false, (geocodeCalls > 0) == scenario.ExpectGeocodeCall,
                    $"{geocodeCalls} geocode calls, expected {(scenario.ExpectGeocodeCall == true ? "at least one" : "none")}"));
            var mapsCalls = calls.Count(c => c.Service == "maps" || c.Service == "places");
            if (scenario.ExpectNoMapsCalls == true)
                checks.Add(Check("behavior.noMapsCalls", "behavior", false, mapsCalls == 0, $"{mapsCalls} Maps/Places calls, expected none"));
            var detailsCalls = calls.Count(c => c.Service == "places" && c.Method == "GET");
            checks.Add(Check("behavior.detailsLimit", "behavior", false, detailsCalls <= limits.MaxDetailsCalls, $"{detailsCalls} Place Details calls (limit {limits.MaxDetailsCalls})"));
            if (scenario.ExpectStatus == "ok")
            {
                var geminiCalls = calls.Count(c => c.Service == "gemini");
                checks.Add(Check("behavior.agentLoop", "behavior", false, geminiCalls >= limits.MinGeminiCallsWhenOk, $"{geminiCalls} Gemini calls (at least {limits.MinGeminiCallsWhenOk} expected)"));
            }
            foreach (var constraint in scenario.ExpectRelaxed)
                checks.Add(Check($"behavior.relaxed[{constraint}]", "behavior", false, relaxedNames.Contains(constraint), "relaxed: " + string.Join(",", relaxedNames)));

            // ---- ranking ----
            if (scenario.ExpectTop1In.Count > 0)
                checks.Add(Check("ranking.top1", "ranking", false, ids.Count > 0 && scenario.ExpectTop1In.Contains(ids[0]), $"top pick '{ids.FirstOrDefault()}'"));
            if (scenario.MustNotBeInTop3.Count > 0)
            {
                var traps = ids.Take(3).Intersect(scenario.MustNotBeInTop3).ToList();
                checks.Add(Check("ranking.trapsOutOfTop3", "ranking", false, traps.Count == 0, traps.Count == 0 ? "ok" : "in top 3: " + string.Join(",", traps)));
            }
        }

        /// <summary>
        /// Sets each check's requirement code: the scenario's tag for the expectation it enforces, else the exercise's
        /// code for the longest matching check-id prefix (checks every scenario runs: contract, grounding, limits).
        /// </summary>
        internal static void TagRequirements(List<GradingCheck> checks, GradingScenario scenario, ExerciseLimits limits)
        {
            foreach (var check in checks)
            {
                var expectation = ExpectationChecks.FirstOrDefault(e => check.Id.StartsWith(e.CheckPrefix, StringComparison.Ordinal)).Expectation;
                if (expectation != null && scenario.Requirements.TryGetValue(expectation, out var code))
                {
                    check.Requirement = code;
                    continue;
                }
                check.Requirement = limits.CheckRequirements
                    .Where(kv => check.Id.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(kv => kv.Key.Length)
                    .Select(kv => kv.Value)
                    .FirstOrDefault();
            }
        }

        private static (double EndLat, double EndLng, string Mode, double Minutes)? ParseDirections(GoogleProxyCallLog call)
        {
            try
            {
                var leg = JsonNode.Parse(call.ResponseBody)?["routes"]?[0]?["legs"]?[0];
                var end = leg?["end_location"];
                var seconds = leg?["duration"]?["value"]?.GetValue<double>();
                if (end == null || seconds == null) return null;
                var mode = QueryParam(call.Path, "mode")?.ToLowerInvariant() ?? "driving";
                return (end["lat"]!.GetValue<double>(), end["lng"]!.GetValue<double>(), mode, seconds.Value / 60);
            }
            catch (Exception) { return null; }
        }

        private static string? QueryParam(string path, string name)
        {
            var q = path.IndexOf('?');
            if (q < 0) return null;
            foreach (var pair in path[(q + 1)..].Split('&'))
            {
                var eq = pair.IndexOf('=');
                if (eq > 0 && Uri.UnescapeDataString(pair[..eq]).Equals(name, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
            return null;
        }

        private static string PathOnly(string path)
        {
            var q = path.IndexOf('?');
            return q < 0 ? path : path[..q];
        }

        private static GradingCheck Check(string id, string category, bool critical, bool passed, string detail) =>
            new() { Id = id, Category = category, Critical = critical, Passed = passed, Detail = detail };

        private static string? Str(JsonNode? node) =>
            node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

        private static int? Int(JsonNode? node)
        {
            if (node is not JsonValue v) return null;
            if (v.TryGetValue<int>(out var i)) return i;
            return v.TryGetValue<double>(out var d) && d == Math.Floor(d) && Math.Abs(d) < int.MaxValue ? (int)d : null;
        }

        private static double? Dbl(JsonNode? node) =>
            node is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;

        private static bool SameNumber(double? a, double? b) =>
            a == null || b == null ? a == b : Math.Abs(a.Value - b.Value) < 0.01;
    }
}
