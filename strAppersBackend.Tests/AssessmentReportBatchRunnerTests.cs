using Microsoft.AspNetCore.Mvc;
using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests for the bug fix in RunStudentSprintAssessment (MetricsController.AssessmentReport.cs): the
/// per-metric loop previously called each metric handler and discarded its returned ActionResult
/// entirely — so a handler returning e.g. UnprocessableEntity (invalid LLM JSON) was silently logged
/// as "OK", never added to `errors`, and left no CacheMetrics row. IsSuccessStatusCode and
/// DescribeFailedActionResult are the two pieces that fix this: classify the real outcome, and
/// describe it for the errors list.
/// </summary>
public class AssessmentReportBatchRunnerTests
{
    // ── IsSuccessStatusCode ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, true)]
    [InlineData(299, true)]
    [InlineData(300, false)]
    [InlineData(400, false)]
    [InlineData(404, false)]
    [InlineData(422, false)]  // UnprocessableEntity — the exact case that was silently swallowed before
    [InlineData(500, false)]
    [InlineData(199, false)]
    public void IsSuccessStatusCode_ClassifiesOnly2xxAsSuccess(int statusCode, bool expected)
    {
        Assert.Equal(expected, MetricsController.IsSuccessStatusCode(statusCode));
    }

    [Fact]
    public void IsSuccessStatusCode_NullStatusCode_IsNotSuccess()
    {
        // No determinable status code (e.g. an unexpected ActionResult type) must not be treated as OK.
        Assert.False(MetricsController.IsSuccessStatusCode(null));
    }

    // ── DescribeFailedActionResult ───────────────────────────────────────────────

    [Fact]
    public void DescribeFailedActionResult_SerializesObjectResultValue()
    {
        var result = new UnprocessableEntityObjectResult(new
        {
            success = false,
            message = "Metric 'X' assessment did not return valid JSON. Nothing was saved to CacheMetrics.",
        });

        var description = MetricsController.DescribeFailedActionResult(result);

        Assert.Contains("did not return valid JSON", description);
        Assert.Contains("\"success\":false", description);
    }

    [Fact]
    public void DescribeFailedActionResult_NullResult_ReturnsPlaceholder()
    {
        Assert.Equal("(null result)", MetricsController.DescribeFailedActionResult(null));
    }

    [Fact]
    public void DescribeFailedActionResult_ObjectResultWithNullValue_FallsBackToTypeName()
    {
        var result = new ObjectResult(null) { StatusCode = 500 };

        var description = MetricsController.DescribeFailedActionResult(result);

        Assert.Equal(nameof(ObjectResult), description);
    }

    [Fact]
    public void DescribeFailedActionResult_NonObjectResult_ReturnsTypeName()
    {
        var result = new NotFoundResult(); // no Value payload, just a bare status result

        var description = MetricsController.DescribeFailedActionResult(result);

        Assert.Equal(nameof(NotFoundResult), description);
    }
}
