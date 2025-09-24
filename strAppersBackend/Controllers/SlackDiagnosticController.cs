using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers
{
    // DISABLED - Slack functionality temporarily disabled
    // [ApiController]
    // [Route("api/[controller]")]
    public class SlackDiagnosticController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<SlackDiagnosticController> _logger;

        public SlackDiagnosticController(IConfiguration cfg, IHttpClientFactory http, ILogger<SlackDiagnosticController> logger)
        {
            _cfg = cfg;
            _http = http;
            _logger = logger;
        }

        /// <summary>
        /// Comprehensive Slack diagnostic tool to test both bot and user tokens
        /// </summary>
        /// <param name="req">Optional request parameters for testing specific functionality</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Detailed diagnostic report</returns>
        [HttpPost("debug")]
        public async Task<IActionResult> DebugAsync([FromBody] SlackDiagRequest? req, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Starting comprehensive Slack diagnostic");

                var section = _cfg.GetSection("Slack");
                var botToken = section["BotToken"]?.Trim();
                var userToken = section["UserToken"]?.Trim();
                var expectedTeam = section["WorkspaceId"]?.Trim();
                var defaultChannel = section["DefaultChannel"]?.Trim();

                var diag = new SlackDiagReport
                {
                    ExpectedTeamId = expectedTeam,
                    Config = new SlackDiagConfigEcho
                    {
                        BotTokenPrefix = MaskTokenPrefix(botToken),
                        UserTokenPrefix = MaskTokenPrefix(userToken),
                        DefaultChannel = defaultChannel
                    }
                };

                // Basic presence/format checks
                diag.Checks.Add(Check("bot_token_present", !string.IsNullOrWhiteSpace(botToken)));
                diag.Checks.Add(Check("user_token_present", !string.IsNullOrWhiteSpace(userToken)));
                diag.Checks.Add(Check("bot_token_format_xoxb", botToken?.StartsWith("xoxb-") == true));
                diag.Checks.Add(Check("user_token_format_xoxp", userToken?.StartsWith("xoxp-") == true || userToken?.StartsWith("xoxc-") == true)); // xoxp=classic user, xoxc=granular

                // Early exit if tokens missing
                if (!diag.Checks.All(c => c.Ok))
                {
                    _logger.LogWarning("Token validation failed, returning early diagnostic");
                    return Ok(diag);
                }

                // --- 1) auth.test on both tokens ---
                var botAuth = await CallSlackAsync("auth.test", botToken!, null, HttpMethod.Post, ct);
                var userAuth = await CallSlackAsync("auth.test", userToken!, null, HttpMethod.Post, ct);

                diag.Bot.AuthTest = botAuth;
                diag.User.AuthTest = userAuth;

                var botTeam = botAuth.Json?.RootElement.GetPropertyOrDefault("team_id");
                var userTeam = userAuth.Json?.RootElement.GetPropertyOrDefault("team_id");

                if (!string.IsNullOrEmpty(expectedTeam))
                {
                    diag.Checks.Add(Check("bot_team_matches_expected", botTeam == expectedTeam, extra: botTeam));
                    diag.Checks.Add(Check("user_team_matches_expected", userTeam == expectedTeam, extra: userTeam));
                }

                // --- 2) token scopes for each token ---
                // auth.scopes tells you what *this token* actually has.
                var botScopes = await CallSlackAsync("auth.scopes", botToken!, null, HttpMethod.Get, ct);
                var userScopes = await CallSlackAsync("auth.scopes", userToken!, null, HttpMethod.Get, ct);
                diag.Bot.Scopes = botScopes;
                diag.User.Scopes = userScopes;

                // --- 3) non-destructive API probes ---
                // conversations.list with bot token
                diag.Bot.ConversationsList = await CallSlackAsync("conversations.list", botToken!, null, HttpMethod.Get, ct);

                // conversations.list with user token (should work with channels:read)
                diag.User.ConversationsList = await CallSlackAsync("conversations.list", userToken!, null, HttpMethod.Get, ct);

                // optional users.lookupByEmail (user token)
                if (!string.IsNullOrWhiteSpace(req?.Email))
                {
                    var qp = new Dictionary<string, string?> { ["email"] = req.Email };
                    diag.User.LookupByEmail = await CallSlackAsync("users.lookupByEmail", userToken!, qp, HttpMethod.Get, ct);
                }

                // --- 4) optional create+archive test (user token) ---
                if (req?.TestCreateChannel == true)
                {
                    var name = (req.TestChannelName ?? "diag-").Trim();
                    if (name.EndsWith("-")) name += DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");

                    var createPayload = new { name, is_private = true };
                    diag.User.ConversationsCreate = await CallSlackAsync("conversations.create", userToken!, createPayload, HttpMethod.Post, ct);

                    var channelId = diag.User.ConversationsCreate.Json?.RootElement
                        .TryGetProperty("channel", out var channelEl) == true 
                        ? channelEl.GetPropertyOrDefault("id") 
                        : null;
                    if (!string.IsNullOrEmpty(channelId))
                    {
                        // Invite the bot (needs groups:write on the user token; bot id must be known)
                        var botId = botAuth.Json?.RootElement.GetPropertyOrDefault("user_id"); // For bot tokens this is "bot user" id
                        if (!string.IsNullOrEmpty(botId))
                        {
                            var invitePayload = new { channel = channelId, users = botId };
                            diag.User.ConversationsInvite = await CallSlackAsync("conversations.invite", userToken!, invitePayload, HttpMethod.Post, ct);
                        }

                        // Clean up (archive)
                        var archivePayload = new { channel = channelId };
                        diag.User.ConversationsArchive = await CallSlackAsync("conversations.archive", userToken!, archivePayload, HttpMethod.Post, ct);
                    }
                }

                // --- 5) Hints based on common failure signatures ---
                diag.Insights = BuildInsights(diag);

                _logger.LogInformation("Slack diagnostic completed successfully");
                return Ok(diag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Slack diagnostic");
                return StatusCode(500, new { error = ex.Message, message = "Slack diagnostic failed" });
            }
        }

        // ----------------- helpers -----------------

        private static SlackDiagCheck Check(string name, bool ok, string? extra = null)
            => new() { Name = name, Ok = ok, Extra = extra };

        private static string MaskTokenPrefix(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return "(missing)";
            var parts = token.Split('-', 2);
            return parts.Length > 0 ? parts[0] + "-•••" : "•••";
        }

        private async Task<SlackCallResult> CallSlackAsync(
            string method,
            string token,
            object? bodyOrNull,
            HttpMethod httpMethod,
            CancellationToken ct,
            Dictionary<string, string?>? queryParams = null)
        {
            var client = _http.CreateClient();
            client.BaseAddress = new Uri("https://slack.com/api/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage resp;

            if (httpMethod == HttpMethod.Get)
            {
                var url = method;
                if (bodyOrNull is null && queryParams is not null && queryParams.Count > 0)
                {
                    url += "?" + string.Join("&",
                        queryParams.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                                   .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
                }
                resp = await client.GetAsync(url, ct);
            }
            else
            {
                var content = bodyOrNull is null
                    ? null
                    : new StringContent(JsonSerializer.Serialize(bodyOrNull),
                                        Encoding.UTF8, "application/json");
                resp = await client.PostAsync(method, content, ct);
            }

            var text = await resp.Content.ReadAsStringAsync(ct);

            var result = new SlackCallResult
            {
                Method = method,
                HttpStatus = (int)resp.StatusCode,
                Raw = Truncate(text, 4000),
                Ok = false
            };

            try
            {
                var doc = JsonDocument.Parse(text);
                result.Json = doc;
                var ok = doc.RootElement.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True;
                result.Ok = ok;

                if (!ok && doc.RootElement.TryGetProperty("error", out var errEl))
                {
                    result.Error = errEl.GetString();
                }
            }
            catch
            {
                // Not JSON; keep raw
            }

            return result;
        }

        private static string Truncate(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        private static List<string> BuildInsights(SlackDiagReport r)
        {
            var tips = new List<string>();

            // If auth.test fails on user token entirely → not a real user token
            if (r.User.AuthTest is { Ok: false } && (r.User.AuthTest?.Error == "invalid_auth" || r.User.AuthTest?.HttpStatus == 401))
                tips.Add("User token failed auth.test → you may not have completed OAuth for a real xoxp/xoxc token. Re-run OAuth and capture authed_user.access_token.");

            // If auth.test passes but every other user call is invalid_auth → wrong token used in calls
            if (r.User.AuthTest?.Ok == true &&
                new[] { r.User.ConversationsList, r.User.LookupByEmail, r.User.ConversationsCreate }
                    .Where(x => x != null)
                    .All(x => x!.Ok == false && x.Error == "invalid_auth"))
                tips.Add("User token passes auth.test but fails on methods → verify you're actually sending the user token in Authorization for those methods (no accidental bot token reuse).");

            // Team mismatch
            if (!string.IsNullOrWhiteSpace(r.ExpectedTeamId))
            {
                var botTeam = r.Bot.AuthTest?.Json?.RootElement.GetPropertyOrDefault("team_id");
                var userTeam = r.User.AuthTest?.Json?.RootElement.GetPropertyOrDefault("team_id");
                if (botTeam != null && botTeam != r.ExpectedTeamId)
                    tips.Add($"Bot token belongs to team {botTeam}, not expected {r.ExpectedTeamId}.");
                if (userTeam != null && userTeam != r.ExpectedTeamId)
                    tips.Add($"User token belongs to team {userTeam}, not expected {r.ExpectedTeamId}.");
            }

            // Scopes sanity (auth.scopes)
            var userScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (r.User.Scopes?.Json?.RootElement.TryGetProperty("scopes", out var userScopesEl) == true)
            {
                foreach (var scope in userScopesEl.EnumerateArray())
                {
                    if (scope.ValueKind == JsonValueKind.String)
                        userScopes.Add(scope.GetString() ?? "");
                }
            }
            if (r.User.AuthTest?.Ok == true)
            {
                if (!userScopes.Contains("channels:read"))
                    tips.Add("User token missing channels:read — conversations.list may fail.");
                if (!userScopes.Contains("users:read.email"))
                    tips.Add("User token missing users:read.email — users.lookupByEmail will fail.");
                // For create private channels
                if (!userScopes.Contains("groups:write"))
                    tips.Add("User token missing groups:write — creating private channels or inviting to them will fail.");
                // Public create/invite
                if (!userScopes.Contains("channels:write"))
                    tips.Add("User token missing channels:write — creating public channels will fail.");
                if (!userScopes.Contains("channels:write.invites"))
                    tips.Add("User token missing channels:write.invites — inviting members to public channels will fail.");
            }

            // Bot scopes sanity
            var botScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (r.Bot.Scopes?.Json?.RootElement.TryGetProperty("scopes", out var botScopesEl) == true)
            {
                foreach (var scope in botScopesEl.EnumerateArray())
                {
                    if (scope.ValueKind == JsonValueKind.String)
                        botScopes.Add(scope.GetString() ?? "");
                }
            }
            if (r.Bot.AuthTest?.Ok == true && !botScopes.Contains("conversations:read"))
                tips.Add("Bot token missing conversations:read — basic channel reads will fail.");

            // If both tokens ok but user calls still invalid_auth → header mixup
            if (r.Bot.AuthTest?.Ok == true && r.User.AuthTest?.Ok == true)
            {
                var anyInvalidAuth = new[] { r.User.ConversationsList, r.User.LookupByEmail, r.User.ConversationsCreate }
                                        .Where(x => x != null).Any(x => x!.Error == "invalid_auth");
                if (anyInvalidAuth)
                    tips.Add("Calls returning invalid_auth despite good user token → check Authorization header per request; ensure 'Bearer <xoxp/xoxc>' is used for user-only methods.");
            }

            return tips;
        }
    }

    // ----------------- DTOs -----------------

    public class SlackDiagReport
    {
        public string? ExpectedTeamId { get; set; }
        public SlackDiagConfigEcho Config { get; set; } = new();
        public List<SlackDiagCheck> Checks { get; set; } = new();
        public SlackTokenProbe Bot { get; set; } = new();
        public SlackTokenProbe User { get; set; } = new();
        public List<string> Insights { get; set; } = new();
    }

    public class SlackDiagConfigEcho
    {
        public string BotTokenPrefix { get; set; } = "";
        public string UserTokenPrefix { get; set; } = "";
        public string? DefaultChannel { get; set; }
    }

    public class SlackDiagCheck
    {
        public string Name { get; set; } = "";
        public bool Ok { get; set; }
        public string? Extra { get; set; }
    }

    public class SlackTokenProbe
    {
        public SlackCallResult? AuthTest { get; set; }
        public SlackCallResult? Scopes { get; set; }
        public SlackCallResult? ConversationsList { get; set; }
        public SlackCallResult? LookupByEmail { get; set; }
        public SlackCallResult? ConversationsCreate { get; set; }
        public SlackCallResult? ConversationsInvite { get; set; }
        public SlackCallResult? ConversationsArchive { get; set; }
    }

    public class SlackCallResult
    {
        public string Method { get; set; } = "";
        public int HttpStatus { get; set; }
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public string Raw { get; set; } = "";
        public JsonDocument? Json { get; set; }
    }

    // ----------------- JSON helpers -----------------

    internal static class JsonExtensions
    {
        public static string? GetPropertyOrDefault(this JsonElement el, string name)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;
            if (!el.TryGetProperty(name, out var found)) return null;
            return found.ValueKind switch
            {
                JsonValueKind.String => found.GetString(),
                JsonValueKind.Number => found.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => found.ToString()
            };
        }
    }

    public class SlackDiagRequest
    {
        public string? Email { get; set; }
        public bool TestCreateChannel { get; set; } = false;
        public string? TestChannelName { get; set; } // e.g., "diag-"
    }
}
