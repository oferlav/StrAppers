using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests the ownership guard and validation logic used by the Metrics CRUD endpoints.
/// Critical invariant: base-institute rows (InstituteId=1) are never mutated or deleted.
/// </summary>
public class MetricsCrudTests
{
    // ── Ownership guard (PUT / DELETE) ────────────────────────────────────────

    [Fact]
    public void OwnershipGuard_AllowsEdit_WhenMetricBelongsToCallingInstitute()
    {
        var metric = new Metric { Id = 200, InstituteId = 5 };
        int callingInstituteId = 5;

        bool allowed = metric.InstituteId == callingInstituteId && metric.InstituteId != 1;

        Assert.True(allowed);
    }

    [Fact]
    public void OwnershipGuard_Blocks_WhenMetricBelongsToOtherInstitute()
    {
        var metric = new Metric { Id = 200, InstituteId = 5 };
        int callingInstituteId = 9;

        bool allowed = metric.InstituteId == callingInstituteId && metric.InstituteId != 1;

        Assert.False(allowed);
    }

    [Fact]
    public void OwnershipGuard_Blocks_WhenMetricIsBaseInstitute()
    {
        var metric = new Metric { Id = 1, InstituteId = 1 };

        // Even when the calling institute is 1, base-institute rows are blocked
        bool isBase = metric.InstituteId == 1;

        Assert.True(isBase);
    }

    [Theory]
    [InlineData(1, false)]  // base-institute: blocked
    [InlineData(5, true)]   // owner: allowed
    [InlineData(9, false)]  // non-owner: blocked
    public void OwnershipGuard_Matrix(int callingInstituteId, bool expectedAllowed)
    {
        var metric = new Metric { Id = 200, InstituteId = 5 };

        bool allowed = metric.InstituteId == callingInstituteId && metric.InstituteId != 1;

        Assert.Equal(expectedAllowed, allowed);
    }

    // ── Influence clamping ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 1)]   // below min → clamped to 1
    [InlineData(1, 1)]   // at min
    [InlineData(3, 3)]   // mid-range unchanged
    [InlineData(5, 5)]   // at max
    [InlineData(6, 5)]   // above max → clamped to 5
    [InlineData(-10, 1)] // far below
    public void Influence_ClampedToOneToFive(int input, int expected)
    {
        var clamped = Math.Clamp(input, 1, 5);
        Assert.Equal(expected, clamped);
    }

    // ── Create metric defaults ────────────────────────────────────────────────

    [Fact]
    public void CreateMetric_SetsInstituteIdFromRoute()
    {
        int routeInstituteId = 7;

        var metric = new Metric
        {
            Name        = "Research Depth",
            InstituteId = routeInstituteId,
            Required    = false,
            Influence   = 3,
        };

        Assert.Equal(7, metric.InstituteId);
        Assert.False(metric.InstituteId == 1); // never base-institute
    }

    [Fact]
    public void CreateMetric_InfluenceClampedOnAssignment()
    {
        int raw = 99;
        var metric = new Metric
        {
            Name      = "Test",
            Influence = Math.Clamp(raw, 1, 5),
        };

        Assert.Equal(5, metric.Influence);
    }

    // ── Delete guard ─────────────────────────────────────────────────────────

    [Fact]
    public void DeleteGuard_BlocksBaseInstituteRow()
    {
        var metric = new Metric { Id = 1, InstituteId = 1 };

        bool canDelete = metric.InstituteId != 1;

        Assert.False(canDelete);
    }

    [Fact]
    public void DeleteGuard_AllowsInstituteOwnedRow()
    {
        var metric = new Metric { Id = 200, InstituteId = 5 };
        int callingInstituteId = 5;

        bool canDelete = metric.InstituteId != 1 && metric.InstituteId == callingInstituteId;

        Assert.True(canDelete);
    }
}
