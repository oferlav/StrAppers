using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests the pure resolution logic of the Codebase &amp; GitHub sensor: which repository tracks a
/// student owns, and the sprint branch name for each track.
/// </summary>
public class GitHubSensorTrackResolutionTests
{
    // ------------------------------------------------------------------ track selection

    [Theory]
    [InlineData("Backend Developer")]
    [InlineData("backend developer")]
    [InlineData("Senior Backend Developer")]
    public void BackendRole_SelectsBackendOnly(string roleName)
    {
        var selection = MetricsController.ResolveGitHubTrackSelection(roleName, null);
        Assert.True(selection.Backend);
        Assert.False(selection.Frontend);
    }

    [Theory]
    [InlineData("Frontend Developer")]
    [InlineData("frontend developer")]
    public void FrontendRole_SelectsFrontendOnly(string roleName)
    {
        var selection = MetricsController.ResolveGitHubTrackSelection(roleName, null);
        Assert.False(selection.Backend);
        Assert.True(selection.Frontend);
    }

    [Theory]
    [InlineData("Full Stack Developer")]
    [InlineData("Fullstack Developer")]
    public void FullStackRole_WithNoCardId_SelectsBoth(string roleName)
    {
        var selection = MetricsController.ResolveGitHubTrackSelection(roleName, null);
        Assert.True(selection.Backend);
        Assert.True(selection.Frontend);
    }

    [Theory]
    [InlineData("1-B", true, false)]
    [InlineData("1-F", false, true)]
    [InlineData("1-B,1-F", true, true)]
    public void FullStackRole_CardIdScopesTracks(string cardId, bool expectBackend, bool expectFrontend)
    {
        var selection = MetricsController.ResolveGitHubTrackSelection("Full Stack Developer", cardId);
        Assert.Equal(expectBackend, selection.Backend);
        Assert.Equal(expectFrontend, selection.Frontend);
    }

    [Fact]
    public void FullStackRole_CardIdWithoutTrackLetters_FallsBackToBoth()
    {
        var selection = MetricsController.ResolveGitHubTrackSelection("Full Stack Developer", "12345");
        Assert.True(selection.Backend);
        Assert.True(selection.Frontend);
    }

    [Fact]
    public void GenericDeveloper_SelectsBoth()
    {
        var selection = MetricsController.ResolveGitHubTrackSelection("Developer", null);
        Assert.True(selection.Backend);
        Assert.True(selection.Frontend);
    }

    [Theory]
    [InlineData("Product Manager")]
    [InlineData("UX Designer")]
    [InlineData("Marketing")]
    [InlineData("BizDev")]
    [InlineData("")]
    [InlineData(null)]
    public void NonDeveloperRoles_SelectNothing(string? roleName)
    {
        var selection = MetricsController.ResolveGitHubTrackSelection(roleName, null);
        Assert.False(selection.Any);
    }

    // ------------------------------------------------------------------ branch naming

    [Theory]
    [InlineData(1, true, 0, "1-B")]
    [InlineData(1, false, 0, "1-F")]
    [InlineData(12, true, 0, "12-B")]
    public void SprintBranch_FollowsConvention(int sprint, bool isBackend, int roleIndex, string expected)
    {
        Assert.Equal(expected, MetricsController.BuildSprintBranchName(sprint, isBackend, roleIndex));
    }

    [Theory]
    [InlineData(1, true, 3, "1-B-3")]
    [InlineData(2, false, 1, "2-F-1")]
    public void RoleIndexedBoards_AppendIndexSuffix(int sprint, bool isBackend, int roleIndex, string expected)
    {
        Assert.Equal(expected, MetricsController.BuildSprintBranchName(sprint, isBackend, roleIndex));
    }

    [Theory]
    [InlineData(true, 0, "Bugs-B")]
    [InlineData(false, 0, "Bugs-F")]
    [InlineData(true, 2, "Bugs-B-2")]
    public void SprintZero_UsesBugsBranch(bool isBackend, int roleIndex, string expected)
    {
        Assert.Equal(expected, MetricsController.BuildSprintBranchName(0, isBackend, roleIndex));
    }

    /// <summary>
    /// The branch name the sensor builds must be the one ParseSprintNumber maps back to the same
    /// sprint — otherwise CI rows written from the branch name would not join to the sensor's scope.
    /// </summary>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(7, 2)]
    [InlineData(13, 0)]
    public void BranchName_RoundTripsThroughSprintParser(int sprint, int roleIndex)
    {
        foreach (var isBackend in new[] { true, false })
        {
            var branch = MetricsController.BuildSprintBranchName(sprint, isBackend, roleIndex);
            Assert.Equal(sprint, MentorController.ParseSprintNumber(branch));
        }
    }
}
