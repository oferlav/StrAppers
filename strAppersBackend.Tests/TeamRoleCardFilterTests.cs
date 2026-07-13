using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Sprint-merge card filtering: template cards for roles nobody on the team has are dropped
/// (same rule as CreateBoard), fixing later sprints resurrecting e.g. a Marketing/BizDev card
/// (label-less) on a team formed without that role.
/// </summary>
public class TeamRoleCardFilterTests
{
    private static Student MakeStudent(int id, string roleName, int roleIndex = 0) => new()
    {
        Id = id,
        Email = $"s{id}@test.com",
        FirstName = "F",
        LastName = "L",
        RoleIndex = roleIndex,
        StudentRoles = new List<StudentRole>
        {
            new StudentRole { RoleId = id + 100, IsActive = true, Role = new Role { Id = id + 100, Name = roleName } }
        }
    };

    private static TrelloProjectCreationRequest RequestWithCards(params (string List, string Role)[] cards) => new()
    {
        SprintPlan = new TrelloSprintPlan
        {
            Cards = cards.Select(c => new TrelloCard { Name = $"{c.Role} task", ListName = c.List, RoleName = c.Role }).ToList(),
        },
    };

    // ── BuildTeamRoleNameSet ──────────────────────────────────────────────────

    [Fact]
    public void RoleSet_ContainsAllTeamRoleNames_CaseInsensitive()
    {
        var team = new List<Student> { MakeStudent(1, "PM"), MakeStudent(2, "UI/UX Designer") };

        var set = TrelloSprintMergeService.BuildTeamRoleNameSet(team, isSingleRole: false);

        Assert.Contains("pm", set);
        Assert.Contains("UI/UX DESIGNER", set);
        Assert.DoesNotContain("Marketing/BizDev", set);
    }

    [Fact]
    public void RoleSet_FullStackTeam_AlsoCoversFrontendAndBackendCards()
    {
        var team = new List<Student> { MakeStudent(1, "Full Stack Developer") };

        var set = TrelloSprintMergeService.BuildTeamRoleNameSet(team, isSingleRole: false);

        Assert.Contains("Frontend Developer", set);
        Assert.Contains("Backend Developer", set);
    }

    [Fact]
    public void RoleSet_SingleRoleBoard_AddsIndexedVariants()
    {
        var team = new List<Student> { MakeStudent(1, "Full Stack Developer", roleIndex: 1), MakeStudent(2, "Full Stack Developer", roleIndex: 2) };

        var set = TrelloSprintMergeService.BuildTeamRoleNameSet(team, isSingleRole: true);

        Assert.Contains("Full Stack Developer 1", set);
        Assert.Contains("Full Stack Developer 2", set);
    }

    // ── FilterSprintPlanCardsToTeam ───────────────────────────────────────────

    [Fact]
    public void Filter_DropsCardForRoleNotOnTeam()
    {
        // The observed bug: Marketing/BizDev card resurrected on Sprint 2 for a team without that role.
        var request = RequestWithCards(("Sprint 2", "PM"), ("Sprint 2", "Marketing/BizDev"), ("Sprint 2", "Full Stack Developer"));
        var team = TrelloSprintMergeService.BuildTeamRoleNameSet(
            new List<Student> { MakeStudent(1, "PM"), MakeStudent(2, "Full Stack Developer") }, isSingleRole: false);

        var removed = TrelloSprintMergeService.FilterSprintPlanCardsToTeam(request, team);

        Assert.Equal(1, removed);
        Assert.DoesNotContain(request.SprintPlan.Cards, c => c.RoleName == "Marketing/BizDev");
        Assert.Equal(2, request.SprintPlan.Cards.Count);
    }

    [Fact]
    public void Filter_KeepsUserStoriesListCards_Always()
    {
        var request = RequestWithCards(("User Stories", ""), ("Sprint 2", "Marketing/BizDev"));
        var team = TrelloSprintMergeService.BuildTeamRoleNameSet(new List<Student> { MakeStudent(1, "PM") }, isSingleRole: false);

        TrelloSprintMergeService.FilterSprintPlanCardsToTeam(request, team);

        Assert.Single(request.SprintPlan.Cards);
        Assert.Equal("User Stories", request.SprintPlan.Cards[0].ListName);
    }

    [Fact]
    public void Filter_DropsEmptyRoleNameCards_OutsideUserStories()
    {
        var request = RequestWithCards(("Sprint 2", ""), ("Sprint 2", "PM"));
        var team = TrelloSprintMergeService.BuildTeamRoleNameSet(new List<Student> { MakeStudent(1, "PM") }, isSingleRole: false);

        var removed = TrelloSprintMergeService.FilterSprintPlanCardsToTeam(request, team);

        Assert.Equal(1, removed);
        Assert.Single(request.SprintPlan.Cards);
    }

    [Fact]
    public void Filter_EmptyTeamSet_IsNoOp()
    {
        // Safety: no team info → leave the template untouched.
        var request = RequestWithCards(("Sprint 2", "Marketing/BizDev"));

        var removed = TrelloSprintMergeService.FilterSprintPlanCardsToTeam(request, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(0, removed);
        Assert.Single(request.SprintPlan.Cards);
    }

    [Fact]
    public void Filter_IsCaseInsensitive_OnRoleNames()
    {
        var request = RequestWithCards(("Sprint 2", "product manager"));
        var team = TrelloSprintMergeService.BuildTeamRoleNameSet(new List<Student> { MakeStudent(1, "Product Manager") }, isSingleRole: false);

        var removed = TrelloSprintMergeService.FilterSprintPlanCardsToTeam(request, team);

        Assert.Equal(0, removed);
    }
}
