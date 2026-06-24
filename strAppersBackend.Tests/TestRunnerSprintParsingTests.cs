using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests ParseSprintNumber — the logic that maps a GitHub branch name to a sprint
/// number stored in BoardStates. Convention: "{sprint}-B" / "{sprint}-F" for
/// numbered sprints, "Bugs-B" / "Bugs-F" for bug-fix branches (sprint 0),
/// "{sprint}-B-{roleIndex}" for single-role boards.
/// </summary>
public class TestRunnerSprintParsingTests
{
    [Theory]
    [InlineData("1-B",   1)]
    [InlineData("2-B",   2)]
    [InlineData("10-B",  10)]
    [InlineData("1-F",   1)]
    [InlineData("3-F",   3)]
    public void SprintBranch_ReturnsSprintNumber(string branch, int expected)
    {
        Assert.Equal(expected, MentorController.ParseSprintNumber(branch));
    }

    [Theory]
    [InlineData("1-B-1",  1)]
    [InlineData("2-B-2",  2)]
    [InlineData("1-F-1",  1)]
    public void SingleRoleBranch_ReturnsSprintNumber(string branch, int expected)
    {
        Assert.Equal(expected, MentorController.ParseSprintNumber(branch));
    }

    [Theory]
    [InlineData("Bugs-B")]
    [InlineData("Bugs-F")]
    [InlineData("bugs-b")]
    [InlineData("BUGS-B")]
    public void BugsBranch_ReturnsZero(string branch)
    {
        Assert.Equal(0, MentorController.ParseSprintNumber(branch));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("main")]
    [InlineData("feature/my-feature")]
    [InlineData("dev")]
    public void UnknownOrEmpty_ReturnsNull(string? branch)
    {
        Assert.Null(MentorController.ParseSprintNumber(branch));
    }
}
