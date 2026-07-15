using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// Regression tests for the missing-deliverable detection bug: the Backend Developer sprint 1
/// card requested "Save your schema diagram in the Meeting Room" (a resource-type deliverable),
/// the PM had correctly checked "Required Resource Data" on the card, but neither Adherence nor
/// Gap Analysis flagged the missing diagram because <see cref="MetricsController.ResourceTaskKeywords"/>
/// didn't match any word in that checklist text. Text below is copied verbatim from the live
/// Trello card (board 6a4f5e0885b6a58f59f776dd) so the test reproduces the exact failure.
/// </summary>
public class MissingDeliverableDetectionTests
{
    private const string RealChecklistsText = """
        ### Checklist
        - [complete] Team Kickoff: Participate in the "Project Kickoff" to meet your teammates and understand the project’s high-level mission.
        - [complete] Local Repository Setup: Clone the provided GitHub repository to your local developer environment to begin your technical exploration.
        - [complete] Sprint Branch Initialization (Mentor Panel): Use the mentor buttons panel to create your official branch for the current sprint.
        - [complete] Local Sync Verification (Local Environment): From your local environment, push a "Health Check" commit to your branch to confirm your local IDE is perfectly synced with the repository.
        - [complete] GitHub Cycle Practice (Mentor Panel): Return to the mentor buttons panel to create a Pull Request (PR) and merge your branch into main. Use this to gain a hands-on understanding of the project's GitHub cycle.
        - [complete] Infrastructure Audit: Connect to the Postgres DB and perform a "Stress Test" (e.g., creating and deleting a dummy table) to ensure the environment is stable for future data.
        - [complete] Collaborative Journey & Persona Review: Sit down with the PM to review their Global Journey Diagram and review the UI/UX Designer's new User Personas. Use this session to brainstorm the core entities and relationships (e.g., Donors, Coordinators, Drivers) needed to support the entire system.
        - [complete] High-Level Schema Diagram: Based on your review with the PM, create a visual ERD (Entity Relationship Diagram) showing the high-level data architecture.
        - [complete] Blueprint Centralization: Save your schema diagram in the Meeting Room so the team can reference our "Data Source of Truth." (Your Mentor can guide you on the process).
        """;

    // ── ResourceTaskKeywords: the original list missed the real deliverable ──

    private static readonly string[] OriginalKeywords =
        { "resource", "upload", "share", "link", "artifact", "submit", "attach" };

    [Fact]
    public void OriginalKeywords_DoNotMatchTheRealDiagramChecklistItems()
    {
        // Documents the bug: this is why Adherence's effectiveRequiredResource stayed false
        // and Gap Analysis had no artifact channel to flag the missing ERD/diagram against.
        var matches = MetricsController.ExtractMatchingChecklistLines(RealChecklistsText, OriginalKeywords);
        Assert.Empty(matches);
    }

    [Fact]
    public void CurrentKeywords_MatchBothDiagramDeliverableLines()
    {
        var matches = MetricsController.ExtractMatchingChecklistLines(RealChecklistsText, MetricsController.ResourceTaskKeywords);

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, l => l.Contains("High-Level Schema Diagram", StringComparison.Ordinal));
        Assert.Contains(matches, l => l.Contains("Blueprint Centralization", StringComparison.Ordinal));
    }

    [Fact]
    public void CurrentKeywords_DoNotFalsePositiveOnPureCodeChecklistItems()
    {
        const string codeOnlyChecklist = """
            ### Checklist
            - [complete] Local Repository Setup: Clone the provided GitHub repository to your local developer environment.
            - [complete] Infrastructure Audit: Connect to the Postgres DB and perform a "Stress Test".
            """;

        Assert.Empty(MetricsController.ExtractMatchingChecklistLines(codeOnlyChecklist, MetricsController.ResourceTaskKeywords));
    }

    // ── ExtractMatchingChecklistLines: edge cases ──

    [Fact]
    public void ExtractMatchingChecklistLines_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(MetricsController.ExtractMatchingChecklistLines(null, MetricsController.ResourceTaskKeywords));
        Assert.Empty(MetricsController.ExtractMatchingChecklistLines("", MetricsController.ResourceTaskKeywords));
    }

    [Fact]
    public void ExtractMatchingChecklistLines_IgnoresNonChecklistLines()
    {
        const string text = "### Checklist\nSave your diagram somewhere.\n- [complete] Save your schema diagram in the Meeting Room.";
        var matches = MetricsController.ExtractMatchingChecklistLines(text, MetricsController.ResourceTaskKeywords);

        Assert.Single(matches);
        Assert.StartsWith("- [complete]", matches[0]);
    }
}
