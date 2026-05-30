using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests the role-course Trello label resolution pattern used across MentorController and MetricsController.
/// Pattern: if IsSingleRole && RoleIndex > 0, the Trello card label is "{roleName} {roleIndex}".
/// Otherwise fall back to the base role name (squad boards).
/// </summary>
public class TrelloRoleLabelTests
{
    // ── Helper replicating the guard used in all fixed locations ──────────

    private static string ResolveLabel(ProjectBoard? board, string roleName, int roleIndex)
    {
        return board?.IsSingleRole == true && roleIndex > 0
            ? $"{roleName} {roleIndex}"
            : roleName;
    }

    private static IReadOnlyList<string> ResolveLabelList(ProjectBoard? board, string roleName, int roleIndex)
    {
        return board?.IsSingleRole == true && roleIndex > 0
            ? new[] { $"{roleName} {roleIndex}" }
            : new[] { roleName };
    }

    // ── Single-label resolution ───────────────────────────────────────────

    [Fact]
    public void RoleCourseBoard_IndexOne_ReturnsIndexedLabel()
    {
        var board = new ProjectBoard { IsSingleRole = true };
        var result = ResolveLabel(board, "Full Stack Developer", 1);
        Assert.Equal("Full Stack Developer 1", result);
    }

    [Fact]
    public void RoleCourseBoard_IndexThree_ReturnsIndexedLabel()
    {
        var board = new ProjectBoard { IsSingleRole = true };
        var result = ResolveLabel(board, "Full Stack Developer", 3);
        Assert.Equal("Full Stack Developer 3", result);
    }

    [Fact]
    public void SquadBoard_IgnoresIndex_ReturnsBaseName()
    {
        var board = new ProjectBoard { IsSingleRole = false };
        var result = ResolveLabel(board, "Frontend Developer", 2);
        Assert.Equal("Frontend Developer", result);
    }

    [Fact]
    public void RoleCourseBoard_RoleIndexZero_ReturnsBaseName()
    {
        var board = new ProjectBoard { IsSingleRole = true };
        var result = ResolveLabel(board, "Full Stack Developer", 0);
        Assert.Equal("Full Stack Developer", result);
    }

    [Fact]
    public void NullBoard_ReturnsBaseName()
    {
        var result = ResolveLabel(null, "Backend Developer", 2);
        Assert.Equal("Backend Developer", result);
    }

    [Fact]
    public void RoleCourseBoard_NonDeveloperRole_ReturnsIndexedLabel()
    {
        var board = new ProjectBoard { IsSingleRole = true };
        var result = ResolveLabel(board, "Product Manager", 2);
        Assert.Equal("Product Manager 2", result);
    }

    // ── List resolution (used in CrmReview, ResourceReview, MeetingsCommunication) ──

    [Fact]
    public void RoleCourseBoard_LabelList_ContainsSingleIndexedEntry()
    {
        var board = new ProjectBoard { IsSingleRole = true };
        var labels = ResolveLabelList(board, "Full Stack Developer", 2);
        Assert.Single(labels);
        Assert.Equal("Full Stack Developer 2", labels[0]);
    }

    [Fact]
    public void SquadBoard_LabelList_ContainsBaseName()
    {
        var board = new ProjectBoard { IsSingleRole = false };
        var labels = ResolveLabelList(board, "Backend Developer", 1);
        Assert.Single(labels);
        Assert.Equal("Backend Developer", labels[0]);
    }

    // ── Team-member label (path 1 and path 2 in MentorController) ────────

    [Fact]
    public void TeamMember_RoleCourseBoard_UsesOwnRoleIndex()
    {
        var board = new ProjectBoard { IsSingleRole = true };

        // Student 1 has RoleIndex=1, Student 2 has RoleIndex=2 — both on the same board
        var label1 = ResolveLabel(board, "Full Stack Developer", 1);
        var label2 = ResolveLabel(board, "Full Stack Developer", 2);

        Assert.Equal("Full Stack Developer 1", label1);
        Assert.Equal("Full Stack Developer 2", label2);
        Assert.NotEqual(label1, label2);
    }

    [Fact]
    public void TeamMember_SquadBoard_AllMembersUseBaseName()
    {
        var board = new ProjectBoard { IsSingleRole = false };

        // On a squad board every member uses their own base role name
        Assert.Equal("Frontend Developer", ResolveLabel(board, "Frontend Developer", 1));
        Assert.Equal("Backend Developer", ResolveLabel(board, "Backend Developer", 1));
    }

    // ── CustomerEngagement inline label (MetricsController) ─────────────

    [Fact]
    public void CustomerEngagement_RoleCourseBoard_UsesIndexedLabel()
    {
        var board = new ProjectBoard { IsSingleRole = true };
        var role = new Role { Name = "Full Stack Developer" };
        var roleIndex = 2;

        var trelloLabel = board.IsSingleRole && roleIndex > 0
            ? $"{role.Name?.Trim() ?? string.Empty} {roleIndex}"
            : role.Name ?? string.Empty;

        Assert.Equal("Full Stack Developer 2", trelloLabel);
    }

    [Fact]
    public void CustomerEngagement_SquadBoard_UsesBaseName()
    {
        var board = new ProjectBoard { IsSingleRole = false };
        var role = new Role { Name = "Frontend Developer" };
        var roleIndex = 1;

        var trelloLabel = board.IsSingleRole && roleIndex > 0
            ? $"{role.Name?.Trim() ?? string.Empty} {roleIndex}"
            : role.Name ?? string.Empty;

        Assert.Equal("Frontend Developer", trelloLabel);
    }
}
