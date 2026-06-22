using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// T1-T12, T19-T20 — Unit tests for User Story board separation feature (TrelloService logic).
/// Tests replicate the logic inline rather than invoking TrelloService directly, following the
/// project convention established in BoardStatesBranchFilterTests and TrelloRoleLabelTests.
/// </summary>
public class UserStoryBoardTrelloServiceTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static string BuildUserStoryCardName(ProjectModuleInfo module) =>
        $"User Story: {module.Title}";

    private static string BuildModuleId(ProjectModuleInfo module) =>
        module.Id.ToString();

    private static TrelloCard BuildUserStoryCard(ProjectModuleInfo module) =>
        new TrelloCard
        {
            Name          = BuildUserStoryCardName(module),
            ModuleId      = BuildModuleId(module),
            ChecklistName = "Acceptance Criteria",
            ChecklistItems = new List<string> { "Add Acceptance Criteria here" },
            RequiredSkillData    = false,
            RequiredResourceData = false,
        };

    // Replicates the stripping logic in TrelloService.CreateProjectWithSprintsAsync
    private static void StripUserStoryFromRequest(TrelloProjectCreationRequest request, bool createUserStoryBoard)
    {
        if (createUserStoryBoard)
        {
            request.SprintPlan?.Lists?.RemoveAll(l =>
                string.Equals(l.Name, "User Stories", StringComparison.OrdinalIgnoreCase));
            request.SprintPlan?.Cards?.RemoveAll(c =>
                string.Equals(c.ListName, "User Stories", StringComparison.OrdinalIgnoreCase));
        }
    }

    // Replicates the PM-filter logic in TrelloService.CreateUserStoryBoardAsync
    private static List<TrelloTeamMember> FilterPMMembers(
        List<TrelloTeamMember> members, bool sendInvitationToPMOnly) =>
        sendInvitationToPMOnly
            ? members.Where(m => !string.IsNullOrWhiteSpace(m.RoleName) &&
                  (m.RoleName.Contains("Product Manager", StringComparison.OrdinalIgnoreCase) ||
                   m.RoleName.Contains("PM", StringComparison.OrdinalIgnoreCase))).ToList()
            : members;

    private static TrelloProjectCreationRequest MakeRequest(
        bool hasUserStoriesList = true, bool hasUserStoriesCard = true)
    {
        var req = new TrelloProjectCreationRequest
        {
            SprintPlan = new TrelloSprintPlan()
        };
        req.SprintPlan.Lists.Add(new TrelloList { Name = "Sprint 1" });
        req.SprintPlan.Lists.Add(new TrelloList { Name = "Sprint 2" });
        if (hasUserStoriesList)
            req.SprintPlan.Lists.Add(new TrelloList { Name = "User Stories" });

        req.SprintPlan.Cards.Add(new TrelloCard { ListName = "Sprint 1", Name = "Backend task" });
        if (hasUserStoriesCard)
            req.SprintPlan.Cards.Add(new TrelloCard { ListName = "User Stories", Name = "User Story Sprint 1" });

        return req;
    }

    // ── T1: Card name format ───────────────────────────────────────────────

    [Fact]
    public void T01_CardName_FollowsUserStoryPrefix()
    {
        var module = new ProjectModuleInfo { Id = 42, Title = "Authentication" };
        Assert.Equal("User Story: Authentication", BuildUserStoryCardName(module));
    }

    // ── T2: Card name with special characters ──────────────────────────────

    [Fact]
    public void T02_CardName_PreservesSpecialCharactersInTitle()
    {
        var module = new ProjectModuleInfo { Id = 7, Title = "API & Webhooks (v2)" };
        Assert.Equal("User Story: API & Webhooks (v2)", BuildUserStoryCardName(module));
    }

    // ── T3: ModuleId is module.Id.ToString() ──────────────────────────────

    [Fact]
    public void T03_ModuleId_IsStringifiedIntId()
    {
        var module = new ProjectModuleInfo { Id = 99, Title = "Payments" };
        Assert.Equal("99", BuildModuleId(module));
    }

    // ── T4: Checklist name is "Acceptance Criteria" ───────────────────────

    [Fact]
    public void T04_ChecklistName_IsAcceptanceCriteria()
    {
        var module = new ProjectModuleInfo { Id = 1, Title = "Login" };
        var card = BuildUserStoryCard(module);
        Assert.Equal("Acceptance Criteria", card.ChecklistName);
    }

    // ── T5: RequiredSkillData and RequiredResourceData are false ──────────

    [Fact]
    public void T05_UserStoryCard_RequiredFieldsAreFalse()
    {
        var module = new ProjectModuleInfo { Id = 3, Title = "Dashboard" };
        var card = BuildUserStoryCard(module);
        Assert.False(card.RequiredSkillData);
        Assert.False(card.RequiredResourceData);
    }

    // ── T6: PM filter when SendInvitationToPMOnly=true ────────────────────

    [Fact]
    public void T06_PMFilter_WhenFlagTrue_OnlyPMRolesPass()
    {
        var members = new List<TrelloTeamMember>
        {
            new() { Email = "pm@x.com",      RoleName = "Product Manager" },
            new() { Email = "pm2@x.com",     RoleName = "Junior PM" },
            new() { Email = "dev@x.com",     RoleName = "Backend Developer" },
            new() { Email = "design@x.com",  RoleName = "UX Designer" },
        };

        var result = FilterPMMembers(members, sendInvitationToPMOnly: true);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, m => m.Email == "pm@x.com");
        Assert.Contains(result, m => m.Email == "pm2@x.com");
        Assert.DoesNotContain(result, m => m.Email == "dev@x.com");
        Assert.DoesNotContain(result, m => m.Email == "design@x.com");
    }

    // ── T7: PM filter when SendInvitationToPMOnly=false ───────────────────

    [Fact]
    public void T07_PMFilter_WhenFlagFalse_AllMembersPass()
    {
        var members = new List<TrelloTeamMember>
        {
            new() { Email = "pm@x.com",  RoleName = "Product Manager" },
            new() { Email = "dev@x.com", RoleName = "Backend Developer" },
        };

        var result = FilterPMMembers(members, sendInvitationToPMOnly: false);

        Assert.Equal(2, result.Count);
    }

    // ── T8: List stripping when CreateUserStoryBoard=true ─────────────────

    [Fact]
    public void T08_WhenFlagTrue_UserStoryListAndCardsStrippedFromRequest()
    {
        var request = MakeRequest();
        StripUserStoryFromRequest(request, createUserStoryBoard: true);

        Assert.DoesNotContain(request.SprintPlan.Lists,
            l => string.Equals(l.Name, "User Stories", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(request.SprintPlan.Cards,
            c => string.Equals(c.ListName, "User Stories", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(request.SprintPlan.Lists, l => l.Name == "Sprint 1");
        Assert.Contains(request.SprintPlan.Lists, l => l.Name == "Sprint 2");
    }

    // ── T9: List preserved when CreateUserStoryBoard=false (legacy) ────────

    [Fact]
    public void T09_WhenFlagFalse_UserStoryListPreservedOnMainBoard()
    {
        var request = MakeRequest();
        StripUserStoryFromRequest(request, createUserStoryBoard: false);

        Assert.Contains(request.SprintPlan.Lists,
            l => string.Equals(l.Name, "User Stories", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(request.SprintPlan.Cards,
            c => string.Equals(c.ListName, "User Stories", StringComparison.OrdinalIgnoreCase));
    }

    // ── T10: Response DTO fields are null when not created ────────────────

    [Fact]
    public void T10_ResponseDto_UserStoryBoardFieldsAreNullByDefault()
    {
        var response = new TrelloProjectCreationResponse();
        Assert.Null(response.UserStoryBoardId);
        Assert.Null(response.UserStoryBoardUrl);
    }

    // ── T11: Zero modules → zero cards to build ────────────────────────────

    [Fact]
    public void T11_ZeroModules_ProducesZeroCards()
    {
        var modules = new List<ProjectModuleInfo>();
        var cards = modules.Select(BuildUserStoryCard).ToList();
        Assert.Empty(cards);
    }

    // ── T12: ModuleType=3 modules excluded from query ─────────────────────

    [Fact]
    public void T12_ModuleType3_ExcludedByFilter()
    {
        // Replicates the WHERE clause in BoardsController before calling CreateProjectWithSprintsAsync:
        // .Where(pm => pm.ProjectId == id && pm.ModuleType != 3)
        var rawModules = new[]
        {
            new { Id = 1, Title = "Auth",    ModuleType = 1 },
            new { Id = 2, Title = "Billing", ModuleType = 2 },
            new { Id = 3, Title = "Hidden",  ModuleType = 3 },
            new { Id = 4, Title = "Reports", ModuleType = 1 },
        };

        var filtered = rawModules.Where(m => m.ModuleType != 3).ToList();

        Assert.Equal(3, filtered.Count);
        Assert.DoesNotContain(filtered, m => m.ModuleType == 3);
        Assert.DoesNotContain(filtered, m => m.Title == "Hidden");
    }

    // ── T19: CreateUserStoryBoard defaults to false ────────────────────────

    [Fact]
    public void T19_TrelloConfig_CreateUserStoryBoardDefaultsFalse()
    {
        var config = new TrelloConfig();
        Assert.False(config.CreateUserStoryBoard);
    }

    // ── T20: Null modules resolved to empty list gracefully ────────────────

    [Fact]
    public void T20_NullModules_ResolvedToEmptyListGracefully()
    {
        // Replicates the `modules ?? new()` guard in CreateProjectWithSprintsAsync
        List<ProjectModuleInfo>? modules = null;
        var effective = modules ?? new List<ProjectModuleInfo>();
        Assert.NotNull(effective);
        Assert.Empty(effective);

        // Card generation on the effective list produces no cards and no exceptions
        var cards = effective.Select(BuildUserStoryCard).ToList();
        Assert.Empty(cards);
    }
}
