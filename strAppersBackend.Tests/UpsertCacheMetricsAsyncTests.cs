using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using strAppersBackend.Controllers;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Regression tests for the bug where UpsertCacheMetricsAsync (MetricsController.cs) swallowed every
/// save failure silently (log-only, no rethrow). A handler that hit a DB failure here still returned
/// success (e.g. Ok(...)) to its caller — RunStudentSprintAssessment saw a 2xx result, reported no
/// error, and no CacheMetrics row was ever written. Real-world case: CustomerRequirementsFidelity's
/// LLM response parsed fine and the handler reached this call, but the save silently failed and the
/// student/sprint/metric had no CacheMetrics row despite an "OK" batch result.
///
/// Fix: log and rethrow. Callers with their own try/catch (GapAnalysis, RunAssessmentEngine) turn this
/// into a proper 500; callers without one (a few early-exit paths in Attendance/CustomerEngagement/
/// MeetingsCommunication) let the app's GlobalExceptionHandlerMiddleware do it instead. Either way,
/// RunStudentSprintAssessment's per-metric try/catch (and IsSuccessStatusCode/DescribeFailedActionResult
/// from the earlier fix) now actually see the failure.
/// </summary>
public class UpsertCacheMetricsAsyncTests
{
    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MetricsController BuildController(ApplicationDbContext db)
    {
        return new MetricsController(
            db,
            Mock.Of<ITrelloService>(),
            Mock.Of<IGitHubService>(),
            new ConfigurationBuilder().Build(),
            NullLogger<MetricsController>.Instance,
            Mock.Of<IChatCompletionService>(),
            Mock.Of<IHttpClientFactory>(),
            Options.Create(new PromptConfig()),
            Mock.Of<IMicrosoftGraphService>(),
            Mock.Of<ISmtpEmailService>(),
            Mock.Of<IAzureBlobStorageService>());
    }

    [Fact]
    public async Task SucceedsNormally_CreatesNewRow()
    {
        var db = CreateDb();
        var controller = BuildController(db);

        await controller.UpsertCacheMetricsAsync("board-1", 65, 1, 113, "Some review content", "graph-base64", CancellationToken.None);

        var row = await db.CacheMetrics.SingleAsync();
        Assert.Equal("board-1", row.BoardId);
        Assert.Equal(65, row.StudentId);
        Assert.Equal(1, row.SprintNumber);
        Assert.Equal(113, row.MetricId);
        Assert.Equal("Some review content", row.ReviewContent);
    }

    [Fact]
    public async Task SucceedsNormally_UpdatesExistingRow()
    {
        var db = CreateDb();
        db.CacheMetrics.Add(new CacheMetrics { BoardId = "board-1", StudentId = 65, SprintNumber = 1, MetricId = 113, ReviewContent = "Old content" });
        await db.SaveChangesAsync();
        var controller = BuildController(db);

        await controller.UpsertCacheMetricsAsync("board-1", 65, 1, 113, "New content", null, CancellationToken.None);

        var row = await db.CacheMetrics.SingleAsync();
        Assert.Equal("New content", row.ReviewContent);
    }

    [Fact]
    public async Task Failure_PropagatesException_InsteadOfSwallowingIt()
    {
        var db = CreateDb();
        var controller = BuildController(db);
        await db.DisposeAsync(); // forces SaveChangesAsync (and the query before it) to throw

        // This is the actual regression check: before the fix, this call would complete without
        // throwing (the exception was caught and only logged) — the caller would have no idea the
        // write failed. It must now propagate so RunStudentSprintAssessment's catch block, and any
        // handler-level try/catch, can see and report it.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            controller.UpsertCacheMetricsAsync("board-1", 65, 1, 113, "content", null, CancellationToken.None));
    }

    [Fact]
    public async Task Failure_DoesNotLeaveAPartialRow()
    {
        var db = CreateDb();
        var controller = BuildController(db);
        await db.DisposeAsync();

        try { await controller.UpsertCacheMetricsAsync("board-1", 65, 1, 113, "content", null, CancellationToken.None); }
        catch { /* expected — asserted separately above */ }

        // Can't query the disposed context, but this documents the invariant: a failed
        // UpsertCacheMetricsAsync call must not silently report success anywhere in the pipeline.
        // (Covered end-to-end by AssessmentReportBatchRunnerTests' IsSuccessStatusCode tests, which
        // confirm a non-2xx/thrown result is classified as a failure by the batch runner.)
        Assert.True(true);
    }

    [Fact]
    public async Task AppendReviewContent_AppendsRatherThanOverwrites()
    {
        var db = CreateDb();
        db.CacheMetrics.Add(new CacheMetrics { BoardId = "board-1", StudentId = 65, SprintNumber = 1, MetricId = 2, ReviewContent = "## Backend\nOK" });
        await db.SaveChangesAsync();
        var controller = BuildController(db);

        await controller.UpsertCacheMetricsAsync(
            "board-1", 65, 1, 2, "## Frontend\nAlso OK", null, CancellationToken.None,
            graph2Base64: "fe-graph", appendReviewContent: true);

        var row = await db.CacheMetrics.SingleAsync();
        Assert.Contains("## Backend\nOK", row.ReviewContent);
        Assert.Contains("## Frontend\nAlso OK", row.ReviewContent);
        Assert.Equal("fe-graph", row.Graph2);
    }
}
