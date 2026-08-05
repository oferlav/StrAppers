using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using strAppersBackend.Controllers;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// How the Trello task sections must be framed so their content is not over-read.
///
/// A live PM assessment concluded "the sprint checklist is entirely incomplete, and no Trello tasks
/// were assigned or tracked". Both statements traced to the prompt: nine checklist items rendered as
/// [incomplete], and a bare "(none)" under "Trello tasks (assigned to student)". Neither supports the
/// conclusion drawn — an unticked box is not proof the work was skipped, and the member-card lookup
/// being empty is the normal state on a board where work is allocated by role card.
/// </summary>
public class AssessmentTrelloTaskFramingTests
{
    private static MetricsController BuildController(ITrelloService trello) =>
        new(new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options),
            trello, Mock.Of<IGitHubService>(), new ConfigurationBuilder().Build(),
            NullLogger<MetricsController>.Instance, Mock.Of<IChatCompletionService>(),
            Mock.Of<IHttpClientFactory>(), Options.Create(new PromptConfig()),
            Mock.Of<IMicrosoftGraphService>(), Mock.Of<ISmtpEmailService>(),
            Mock.Of<IAzureBlobStorageService>());

    private static Mock<ITrelloService> TrelloWith(bool roleCardExists)
    {
        var trello = new Mock<ITrelloService>();
        trello.Setup(t => t.ResolveSprintLabelsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new[] { "Product Manager" });
        trello.Setup(t => t.GetSprintRoleCardSnapshotAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(roleCardExists
                ? new SprintRoleCardSnapshot
                {
                    TrelloCardId = "5-PM",
                    CardName = "Tactical Blueprint and User Story",
                    Description = "Translate the roadmap into a User Story.",
                    ChecklistsText = "### Checklist\n- [incomplete] Meeting Orchestration\n- [incomplete] Design Alignment",
                }
                : null);
        trello.Setup(t => t.GetMemberBoardCardsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<SprintRoleCardSnapshot>());
        return trello;
    }

    private static async Task<string> RenderAsync(bool roleCardExists)
    {
        var sb = new StringBuilder();
        await BuildController(TrelloWith(roleCardExists).Object).AppendAssessmentTrelloTasksAsync(
            sb, "b1", "pm@example.com", "Product Manager", 5, CancellationToken.None);
        return sb.ToString();
    }

    [Fact]
    public async Task UntickedChecklistItems_AreLabelledAsTrackingNotDelivery()
    {
        var text = System.Text.RegularExpressions.Regex.Replace(await RenderAsync(roleCardExists: true), @"\s+", " ");

        Assert.Contains("only whether the box was ticked", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT evidence the underlying work was not done", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not as proof of missing delivery", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmptyMemberCardLookup_IsExplainedNotLeftAsBareNone()
    {
        var text = System.Text.RegularExpressions.Regex.Replace(await RenderAsync(roleCardExists: true), @"\s+", " ");

        Assert.DoesNotContain("_(none)_", text, StringComparison.Ordinal);
        Assert.Contains("allocated through the sprint role card", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do NOT conclude that no tasks were assigned or tracked", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WithNoRoleCard_TheWeakerWordingIsUsed()
    {
        var text = System.Text.RegularExpressions.Regex.Replace(await RenderAsync(roleCardExists: false), @"\s+", " ");

        // Nothing to point at, so it must not claim a role card allocates the work.
        Assert.DoesNotContain("allocated through the sprint role card", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not by itself mean no work was assigned", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChecklistCaveatIsOmitted_WhenThereIsNoRoleCard()
    {
        var text = await RenderAsync(roleCardExists: false);

        Assert.DoesNotContain("only whether the box was ticked", text, StringComparison.OrdinalIgnoreCase);
    }
}
