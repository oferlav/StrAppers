namespace strAppersBackend.Tests;

/// <summary>
/// Staff dashboard visibility rule.
///
/// The bug: an institute board carries both an InstituteProjectId (its real project) and a
/// ProjectId inherited from the catalog row it was built from. All three staff-dashboard queries
/// filtered on the CATALOG row's IsAvailable, so deactivating that catalog project silently emptied
/// the staff squad list - the endpoints return an empty list rather than an error, so nothing
/// surfaced.
///
/// Institute 1 (base/B2C) keeps the original check. For institutes > 1 the predicate can only ever
/// widen the result set, which is what makes the change safe: no board visible today can disappear.
/// </summary>
public class StaffSquadsAvailabilityTests
{
    /// <summary>Mirrors BoardsController.RequiresCatalogProjectAvailable.</summary>
    private static bool RequiresCatalogProjectAvailable(int instituteId) => instituteId <= 1;

    /// <summary>Mirrors the shared Where clause in all three staff-dashboard queries.</summary>
    private static bool BoardIsVisible(int instituteId, bool isSystemBoard, int boardInstituteId, bool catalogProjectAvailable)
    {
        var requireCatalogAvailable = RequiresCatalogProjectAvailable(instituteId);
        return !isSystemBoard
               && boardInstituteId == instituteId
               && (!requireCatalogAvailable || catalogProjectAvailable);
    }

    [Fact]
    public void RealInstitute_SeesItsBoard_EvenWhenTheCatalogProjectIsDeactivated()
    {
        // The reported bug, directly.
        Assert.True(BoardIsVisible(instituteId: 3, isSystemBoard: false, boardInstituteId: 3, catalogProjectAvailable: false));
    }

    [Fact]
    public void RealInstitute_StillSeesItsBoard_WhenTheCatalogProjectIsAvailable()
    {
        Assert.True(BoardIsVisible(instituteId: 3, isSystemBoard: false, boardInstituteId: 3, catalogProjectAvailable: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Institute1_KeepsTheOriginalCatalogCheck(bool catalogProjectAvailable)
    {
        // Base/B2C institute is deliberately untouched, so legacy behaviour cannot drift.
        Assert.Equal(catalogProjectAvailable,
            BoardIsVisible(instituteId: 1, isSystemBoard: false, boardInstituteId: 1, catalogProjectAvailable: catalogProjectAvailable));
    }

    [Fact]
    public void SystemBoards_StayHidden_ForEveryInstitute()
    {
        Assert.False(BoardIsVisible(instituteId: 3, isSystemBoard: true, boardInstituteId: 3, catalogProjectAvailable: true));
        Assert.False(BoardIsVisible(instituteId: 1, isSystemBoard: true, boardInstituteId: 1, catalogProjectAvailable: true));
    }

    [Fact]
    public void OtherInstitutesBoards_StayHidden()
    {
        Assert.False(BoardIsVisible(instituteId: 3, isSystemBoard: false, boardInstituteId: 4, catalogProjectAvailable: true));
    }

    [Fact]
    public void ForInstitutesAboveOne_TheChangeOnlyEverWidens()
    {
        // The safety property the whole scoping argument rests on: anything visible under the old
        // predicate is still visible under the new one, for every input combination.
        foreach (var instituteId in new[] { 1, 2, 3, 99 })
        foreach (var isSystemBoard in new[] { true, false })
        foreach (var available in new[] { true, false })
        {
            var oldVisible = !isSystemBoard && available;
            var newVisible = BoardIsVisible(instituteId, isSystemBoard, instituteId, available);
            if (oldVisible)
                Assert.True(newVisible, $"institute {instituteId} lost a board that was visible before");
        }
    }
}
