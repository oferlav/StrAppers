using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using strAppersBackend.Models;
using strAppersBackend.Services.GoogleProxy;

namespace strAppersBackend.Controllers;

/// <summary>
/// Google API proxy for student backends. Contract (mirrored in the student template's Infra/googleGateway.js):
///   ANY /api/google-proxy/{service}/{upstreamPath}?{query}   headers: X-Proxy-Token (required), X-Run-Id (optional)
///       service = gemini | maps | places | speech. Only allowlisted paths are forwarded. The proxy strips the student's
///       headers, adds the real Google key, and returns Google's status, content type and body unchanged.
///   GET /api/google-proxy/runs/{runId}   header: X-Proxy-Token
///       The call log of that run, limited to the token's board.
/// Fixture mode: when X-Run-Id is a signed fixture run id (GoogleProxyFixtureRuns), Maps and Places are answered by
/// GoogleProxyFixtureEngine from a simulated world instead of Google.
/// </summary>
[ApiController]
[Route("api/google-proxy")]
[ApiExplorerSettings(IgnoreApi = true)]
public class GoogleProxyController : ControllerBase
{
    private static readonly Regex RunIdPattern = new(@"^[A-Za-z0-9._-]{1,100}$", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> Upstream = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gemini"] = "https://generativelanguage.googleapis.com",
        ["maps"] = "https://maps.googleapis.com",
        ["places"] = "https://places.googleapis.com",
        ["speech"] = "https://speech.googleapis.com"
    };

    // Everything else is refused, so a leaked token cannot reach other Google APIs or pricier Gemini models.
    private static readonly (string Service, string Method, Regex Path)[] Allowed =
    {
        ("gemini", "POST", new Regex(@"^v1beta/models/gemini-2\.5-flash:generateContent$", RegexOptions.Compiled)),
        ("maps", "GET", new Regex(@"^maps/api/geocode/json$", RegexOptions.Compiled)),
        ("maps", "GET", new Regex(@"^maps/api/directions/json$", RegexOptions.Compiled)),
        ("places", "POST", new Regex(@"^v1/places:searchText$", RegexOptions.Compiled)),
        ("places", "GET", new Regex(@"^v1/places/[A-Za-z0-9_-]{1,300}$", RegexOptions.Compiled)),
        ("speech", "POST", new Regex(@"^v1/speech:recognize$", RegexOptions.Compiled)),
    };

    // The only student request headers forwarded upstream (content type travels with the body).
    private static readonly string[] ForwardedHeaders = { "X-Goog-FieldMask" };

    private readonly GoogleProxyConfig _config;
    private readonly GoogleProxyRunStore _store;
    private readonly GoogleProxyFixtureEngine _fixtures;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleProxyController> _logger;

    public GoogleProxyController(
        IOptions<GoogleProxyConfig> config,
        GoogleProxyRunStore store,
        GoogleProxyFixtureEngine fixtures,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GoogleProxyController> logger)
    {
        _config = config.Value;
        _store = store;
        _fixtures = fixtures;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("runs/{runId}")]
    public IActionResult GetRun(string runId)
    {
        if (!TryAuthorize(out var boardId, out var failure)) return failure!;
        if (!RunIdPattern.IsMatch(runId)) return BadRequest(new { error = "Invalid run id." });

        var calls = _store.GetRun(boardId!, runId);
        if (calls == null) return NotFound(new { runId, error = "No calls recorded for this run id." });

        return Ok(new
        {
            runId,
            boardId,
            source = "proxy",
            counts = calls.GroupBy(c => c.Service + " " + c.Method + " " + PathWithoutQuery(c.Path)).ToDictionary(g => g.Key, g => g.Count()),
            calls
        });
    }

    [AcceptVerbs("GET", "POST")]
    [Route("{service}/{**path}")]
    public async Task<IActionResult> Forward(string service, string? path, CancellationToken cancellationToken)
    {
        if (!TryAuthorize(out var boardId, out var failure)) return failure!;

        path = (path ?? "").TrimStart('/');
        var method = Request.Method.ToUpperInvariant();
        if (!Upstream.TryGetValue(service, out var upstreamHost))
            return StatusCode(403, new { error = $"Unknown service '{service}'. Use gemini, maps, places or speech." });
        if (!Allowed.Any(a => a.Service.Equals(service, StringComparison.OrdinalIgnoreCase) && a.Method == method && a.Path.IsMatch(path)))
            return StatusCode(403, new { error = $"{method} {service}/{path} is not allowed through the proxy." });
        service = service.ToLowerInvariant();

        string? runId = null;
        if (Request.Headers.TryGetValue("X-Run-Id", out var runIdHeader) && !string.IsNullOrWhiteSpace(runIdHeader))
        {
            runId = runIdHeader.ToString().Trim();
            if (!RunIdPattern.IsMatch(runId)) return BadRequest(new { error = "Invalid X-Run-Id." });
        }

        // Fixture runs (signed "fx." run ids created by the grader) answer Maps and Places from a simulated world
        // (GoogleProxyFixtures/{worldId}.json) so every student is graded on the same fixed data. Gemini and Speech stay live.
        FixtureWorld? fixtureWorld = null;
        switch (GoogleProxyFixtureRuns.Parse(runId, boardId!, _config.TokenSecret, _config.FixtureRunTtlMinutes, DateTimeOffset.UtcNow, out var worldId))
        {
            case GoogleProxyFixtureRuns.Kind.Invalid:
                return StatusCode(403, new { error = "Invalid or expired fixture run id." });
            case GoogleProxyFixtureRuns.Kind.Valid:
                fixtureWorld = _fixtures.GetWorld(worldId!);
                if (fixtureWorld == null)
                {
                    _logger.LogError("[GoogleProxy] Fixture world {WorldId} not found or invalid", worldId);
                    return StatusCode(503, new { error = "Fixture world not available." });
                }
                break;
        }
        var simulated = fixtureWorld != null && (service == "maps" || service == "places");

        string? apiKey = null;
        if (!simulated)
        {
            apiKey = service == "gemini"
                ? FirstNonEmpty(_configuration["GoogleApis:StudentBackendApiKey"], Environment.GetEnvironmentVariable("GOOGLE_API_KEY"))
                : FirstNonEmpty(_configuration["GoogleApis:StudentMapsApiKey"], Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY"),
                                _configuration["GoogleApis:StudentBackendApiKey"], Environment.GetEnvironmentVariable("GOOGLE_API_KEY"));
            if (apiKey == null)
            {
                _logger.LogWarning("[GoogleProxy] No Google key configured for service {Service}", service);
                return StatusCode(503, new { error = "The proxy has no Google key configured for this service." });
            }
        }

        byte[] body = Array.Empty<byte>();
        if (method == "POST")
        {
            if (Request.ContentLength > _config.MaxRequestBodyBytes) return StatusCode(413, new { error = "Request body too large." });
            using var buffer = new MemoryStream();
            await Request.Body.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length > _config.MaxRequestBodyBytes) return StatusCode(413, new { error = "Request body too large." });
            body = buffer.ToArray();
        }

        switch (_store.TryReserve(boardId!, runId))
        {
            case GoogleProxyRunStore.ReserveResult.RunLimit:
                return StatusCode(429, new { error = $"This run already made {_config.MaxCallsPerRun} Google calls, the per-run limit." });
            case GoogleProxyRunStore.ReserveResult.DailyLimit:
                return StatusCode(429, new { error = "This project reached its daily Google call limit." });
        }

        // The student's query string minus any 'key' they sent; the real key is added below for key-in-query services.
        var queryPairs = Request.Query
            .Where(q => !q.Key.Equals("key", StringComparison.OrdinalIgnoreCase))
            .SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string>(q.Key, v ?? "")))
            .ToList();
        var query = queryPairs.Select(q => Uri.EscapeDataString(q.Key) + "=" + Uri.EscapeDataString(q.Value)).ToList();
        var loggedPath = "/" + path + (query.Count > 0 ? "?" + string.Join("&", query) : "");

        var stopwatch = Stopwatch.StartNew();
        int status;
        string contentType = "application/json";
        byte[] responseBody;
        if (simulated)
        {
            var fixture = _fixtures.Handle(fixtureWorld!, service, method, path, queryPairs, body, Request.Headers["X-Goog-FieldMask"].ToString());
            status = fixture?.Status ?? 501;
            responseBody = Encoding.UTF8.GetBytes(fixture?.Json ?? "{\"error\":\"This call is not simulated in fixture mode.\"}");
        }
        else
        {
            if (service == "maps" || service == "speech") query.Add("key=" + Uri.EscapeDataString(apiKey!));
            var upstreamUrl = upstreamHost + "/" + path + (query.Count > 0 ? "?" + string.Join("&", query) : "");

            using var upstreamRequest = new HttpRequestMessage(new HttpMethod(method), upstreamUrl);
            if (method == "POST")
            {
                upstreamRequest.Content = new ByteArrayContent(body);
                upstreamRequest.Content.Headers.TryAddWithoutValidation("Content-Type", Request.ContentType ?? "application/json");
            }
            foreach (var header in ForwardedHeaders)
                if (Request.Headers.TryGetValue(header, out var value)) upstreamRequest.Headers.TryAddWithoutValidation(header, value.ToString());
            if (service == "gemini" || service == "places") upstreamRequest.Headers.TryAddWithoutValidation("X-Goog-Api-Key", apiKey);

            try
            {
                var client = _httpClientFactory.CreateClient("GoogleProxy");
                using var upstreamResponse = await client.SendAsync(upstreamRequest, cancellationToken);
                status = (int)upstreamResponse.StatusCode;
                contentType = upstreamResponse.Content.Headers.ContentType?.ToString() ?? "application/json";
                responseBody = await upstreamResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                _logger.LogWarning(ex, "[GoogleProxy] Upstream call failed: {Service} {Method} /{Path}", service, method, path);
                status = 502;
                responseBody = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new { error = "Google did not respond.", message = ex.Message }));
            }
        }
        stopwatch.Stop();

        if (runId != null)
        {
            _store.Record(boardId!, runId, new GoogleProxyCallLog
            {
                At = DateTime.UtcNow,
                Service = service,
                Method = method,
                Path = loggedPath,
                Status = status,
                DurationMs = stopwatch.ElapsedMilliseconds,
                FixtureWorld = simulated ? fixtureWorld!.WorldId : null,
                RequestBody = Truncate(Encoding.UTF8.GetString(body)),
                ResponseBody = Truncate(Encoding.UTF8.GetString(responseBody))
            });
        }

        return new RawUpstreamResult(responseBody, contentType, status);
    }

    private bool TryAuthorize(out string? boardId, out IActionResult? failure)
    {
        boardId = null;
        failure = null;
        if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.TokenSecret))
        {
            failure = StatusCode(503, new { error = "The Google proxy is not enabled." });
            return false;
        }
        boardId = GoogleProxyTokens.Validate(Request.Headers["X-Proxy-Token"].ToString(), _config.TokenSecret);
        if (boardId == null)
        {
            failure = Unauthorized(new { error = "Missing or invalid X-Proxy-Token." });
            return false;
        }
        return true;
    }

    private string Truncate(string text) =>
        text.Length > _config.MaxLoggedBodyChars ? text[.._config.MaxLoggedBodyChars] + "...[truncated]" : text;

    private static string PathWithoutQuery(string path)
    {
        var q = path.IndexOf('?');
        return q < 0 ? path : path[..q];
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>Returns Google's raw bytes with Google's status code and content type.</summary>
    private sealed class RawUpstreamResult : IActionResult
    {
        private readonly byte[] _body;
        private readonly string _contentType;
        private readonly int _status;

        public RawUpstreamResult(byte[] body, string contentType, int status)
        {
            _body = body;
            _contentType = contentType;
            _status = status;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = _status;
            response.ContentType = _contentType;
            response.ContentLength = _body.Length;
            await response.Body.WriteAsync(_body);
        }
    }
}
