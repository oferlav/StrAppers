using strAppersBackend.Controllers;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Regression tests for the CONTEXT-always-empty bug: <c>ResolveTrelloSprintCardLabel</c> preferred
/// <see cref="Role.Description"/> over <see cref="Role.Name"/> when building the Trello sprint-card
/// lookup key. Every seeded role has a non-empty Description (see CSV/Roles.csv), so the lookup used
/// e.g. "Develops server-side logic and database integration" as the search term instead of "Backend
/// Developer" — which never matches a real Trello card label. This silently emptied the Gap Analysis
/// CONTEXT block ("No context blocks could be assembled for this sprint/role") on every multi-role
/// board, for every metric that shares this helper (Gap Analysis, Meetings/Communication, Customer
/// Engagement) — confirmed via the [Metrics Debug] CONTEXT ASSEMBLY TRACE email and the live Trello
/// board's actual card labels.
/// </summary>
public class TrelloSprintCardLabelResolutionTests
{
    // Verbatim from CSV/Roles.csv (Id, Name, Description).
    private static readonly (string Name, string Description)[] SeededRoles =
    {
        ("Product Manager", "Leads product planning and execution"),
        ("Frontend Developer", "Develops user interface and user experience"),
        ("Backend Developer", "Develops server-side logic and database integration"),
        ("UI/UX Designer", "Designs user interface and user experience"),
        ("Full Stack Developer", "Develop backend + UI"),
        ("Marketing/BizDev", "Conducts research and Market analysis. Responsible for Media"),
    };

    [Theory]
    [MemberData(nameof(SeededRoleNames))]
    public void ResolveTrelloSprintCardLabel_UsesRoleName_NotDescription(string name, string description)
    {
        var role = new Role { Name = name, Description = description };

        var label = MetricsController.ResolveTrelloSprintCardLabel(role, fullStackTrackLabel: null);

        Assert.Equal(name, label);
        Assert.NotEqual(description, label);
    }

    public static IEnumerable<object[]> SeededRoleNames() =>
        SeededRoles.Select(r => new object[] { r.Name, r.Description });

    [Fact]
    public void ResolveTrelloSprintCardLabel_NullRole_FallsBackToTeamMember()
    {
        Assert.Equal("Team Member", MetricsController.ResolveTrelloSprintCardLabel(null, fullStackTrackLabel: null));
    }

    [Fact]
    public void ResolveTrelloSprintCardLabel_RoleWithNoDescription_UsesName()
    {
        var role = new Role { Name = "Backend Developer", Description = null };
        Assert.Equal("Backend Developer", MetricsController.ResolveTrelloSprintCardLabel(role, fullStackTrackLabel: null));
    }

    [Fact]
    public void ResolveTrelloSprintCardLabel_FullStackTrackLabel_Overrides()
    {
        var role = new Role { Name = "Full Stack Developer", Description = "Develop backend + UI" };
        Assert.Equal("Backend Developer", MetricsController.ResolveTrelloSprintCardLabel(role, fullStackTrackLabel: "Backend Developer"));
    }

    [Fact]
    public void ResolveTrelloSprintCardLabel_TrimsWhitespace()
    {
        var role = new Role { Name = "  Backend Developer  ", Description = "  Develops server-side logic  " };
        Assert.Equal("Backend Developer", MetricsController.ResolveTrelloSprintCardLabel(role, fullStackTrackLabel: null));
    }
}
