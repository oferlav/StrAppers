using strAppersBackend.Controllers;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests for BoardsController.ResolveBoardProjectIdentity — the title / brief / logo shown for a board.
///
/// A board's ProjectId points at the base Projects row. For a custom institute project that row is a
/// placeholder created by CreateEmptyProjectDesign holding only a title ("New Project Design"), so the
/// board room showed the default name, an empty Project Brief and no logo while the real values sat on
/// the InstituteProject.
///
/// Two rules matter and are both covered below: the switch applies only to InstituteId > 1 (institute 1
/// is the default B2C institute), and every field falls back to the base project independently, so the
/// change can only add a value — never blank one that used to display.
/// </summary>
public class BoardProjectIdentityTests
{
    private static Project BaseProject(
        string? title = "New Project Design",
        string? description = null,
        string? logo = null)
        => new() { Id = 173, Title = title!, Description = description, Logo = logo };

    private static InstituteProject Ip(
        int instituteId,
        string? title = "Chapter1",
        string? description = "The real brief",
        string? logo = "data:image/png;base64,IP")
        => new() { Id = 78, InstituteId = instituteId, Title = title!, Description = description, Logo = logo };

    [Fact]
    public void InstituteIdAbove1_PrefersTheInstituteProject()
    {
        var (title, brief, logo) = BoardsController.ResolveBoardProjectIdentity(BaseProject(), Ip(4));

        Assert.Equal("Chapter1", title);
        Assert.Equal("The real brief", brief);
        Assert.Equal("data:image/png;base64,IP", logo);
    }

    [Theory]
    [InlineData(1)]  // default B2C institute — behaviour must be unchanged
    [InlineData(0)]
    public void InstituteId1OrBelow_KeepsTheBaseProject(int instituteId)
    {
        var baseProject = BaseProject(title: "FoodRoute", description: "Base brief", logo: "BASE");

        var (title, brief, logo) = BoardsController.ResolveBoardProjectIdentity(baseProject, Ip(instituteId));

        Assert.Equal("FoodRoute", title);
        Assert.Equal("Base brief", brief);
        Assert.Equal("BASE", logo);
    }

    [Fact]
    public void NoInstituteProject_KeepsTheBaseProject()
    {
        var baseProject = BaseProject(title: "FoodRoute", description: "Base brief", logo: "BASE");

        var (title, brief, logo) = BoardsController.ResolveBoardProjectIdentity(baseProject, null);

        Assert.Equal("FoodRoute", title);
        Assert.Equal("Base brief", brief);
        Assert.Equal("BASE", logo);
    }

    [Fact]
    public void EachFieldFallsBackIndependently_NeverBlanksAValueThatUsedToShow()
    {
        // The one real institute>1 board we saw had a populated base description and logo. An
        // InstituteProject that fills only the title must not wipe them.
        var baseProject = BaseProject(title: "FoodRoute", description: "Base brief", logo: "BASE");
        var sparse = Ip(4, title: "FoodSquad", description: null, logo: null);

        var (title, brief, logo) = BoardsController.ResolveBoardProjectIdentity(baseProject, sparse);

        Assert.Equal("FoodSquad", title);
        Assert.Equal("Base brief", brief);
        Assert.Equal("BASE", logo);
    }

    [Fact]
    public void PlaceholderBaseProject_SurfacesTheInstituteValues()
    {
        // The reported symptom: placeholder base row, so brief and logo were empty and the name
        // was the CreateEmptyProjectDesign default.
        var (title, brief, logo) = BoardsController.ResolveBoardProjectIdentity(BaseProject(), Ip(4));

        Assert.NotEqual("New Project Design", title);
        Assert.False(string.IsNullOrEmpty(brief));
        Assert.False(string.IsNullOrEmpty(logo));
    }

    [Fact]
    public void NoProjectAndNoInstituteProject_ReturnsNulls()
    {
        var (title, brief, logo) = BoardsController.ResolveBoardProjectIdentity(null, null);

        Assert.Null(title);
        Assert.Null(brief);
        Assert.Null(logo);
    }
}
