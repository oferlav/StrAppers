using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests the GitHub diagnostic probe.
///
/// It exists because the normal lookups swallow HTTP failures into null, making a 401, a 403, a
/// rate-limit and a genuinely missing branch indistinguishable — so an observation failure renders as
/// "the student delivered nothing". The probe must report the raw status of each endpoint and, for
/// the closed-PR scan, the head refs, since that is what identifies a merged PR the head filter
/// failed to match.
/// </summary>
public class GitHubSensorDiagnosticProbeTests
{
    private static GitHubService BuildService(
        Func<HttpRequestMessage, HttpResponseMessage> responder, string? token = "test-token")
    {
        var client = new HttpClient(new MockHttpMessageHandler(responder));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GitHub:AccessToken"] = token })
            .Build();

        return new GitHubService(client, NullLogger<GitHubService>.Instance, config, Mock.Of<IWebHostEnvironment>());
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ReportsUnauthorised_DistinctlyFromMissingBranch()
    {
        var service = BuildService(_ => Json(HttpStatusCode.Unauthorized, "{\"message\":\"Bad credentials\"}"));

        var lines = await service.DiagnoseBranchAccessAsync("acme", "web", "1-F");
        var text = string.Join("\n", lines);

        Assert.Contains("401", text, StringComparison.Ordinal);
        Assert.Contains("Bad credentials", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsMissingTokenExplicitly()
    {
        var service = BuildService(_ => Json(HttpStatusCode.OK, "[]"), token: null);

        var lines = await service.DiagnoseBranchAccessAsync("acme", "web", "1-F");

        Assert.Contains(lines, l => l.Contains("Token:", StringComparison.Ordinal)
                                    && l.Contains("MISSING", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NeverLeaksTheTokenValue()
    {
        var service = BuildService(_ => Json(HttpStatusCode.OK, "[]"));

        var lines = await service.DiagnoseBranchAccessAsync("acme", "web", "1-F");
        var text = string.Join("\n", lines);

        Assert.DoesNotContain("test-token", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The case that matters: the branch is gone after a squash merge (404) but a merged PR for it
    /// still exists in the closed list. The probe must surface that head ref, because that is the
    /// evidence the sensor should have found and didn't.
    /// </summary>
    [Fact]
    public async Task SurfacesMergedPullRequestHeadRefs_WhenBranchIsDeleted()
    {
        const string closedPrs = """
            [{"number":7,"merged_at":"2026-03-02T10:00:00Z","head":{"ref":"1-F"}},
             {"number":6,"merged_at":null,"head":{"ref":"1-B"}}]
            """;

        var service = BuildService(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("state=closed", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, closedPrs);
            if (url.Contains("/pulls?", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, "[]");           // head filter misses it
            if (url.Contains("/branches/", StringComparison.Ordinal) ||
                url.Contains("/compare/", StringComparison.Ordinal))
                return Json(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}");
            return Json(HttpStatusCode.OK, "{}");
        });

        var lines = await service.DiagnoseBranchAccessAsync("acme", "web", "1-F");
        var text = string.Join("\n", lines);

        Assert.Contains("404", text, StringComparison.Ordinal);
        Assert.Contains("PRs matched by head filter: (none)", text, StringComparison.Ordinal);
        Assert.Contains("#7 head='1-F' merged=yes", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsRateLimitRemaining_WhenGitHubSendsIt()
    {
        var service = BuildService(_ =>
        {
            var response = Json(HttpStatusCode.Forbidden, "{\"message\":\"API rate limit exceeded\"}");
            response.Headers.Add("x-ratelimit-remaining", "0");
            return response;
        });

        var lines = await service.DiagnoseBranchAccessAsync("acme", "web", "1-F");
        var text = string.Join("\n", lines);

        Assert.Contains("403", text, StringComparison.Ordinal);
        Assert.Contains("rate-limit remaining: 0", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NeverThrows_WhenTheRequestFailsOutright()
    {
        var service = BuildService(_ => throw new HttpRequestException("network down"));

        var lines = await service.DiagnoseBranchAccessAsync("acme", "web", "1-F");

        Assert.Contains(lines, l => l.Contains("EXCEPTION", StringComparison.Ordinal));
    }
}
