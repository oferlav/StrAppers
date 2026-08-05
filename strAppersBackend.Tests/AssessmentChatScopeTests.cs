using System.Text;
using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// Group and private chats are not sprint-scoped storage, so the sensor no longer filters them to the
/// sprint window.
///
/// Filtering discarded the entire conversation whenever the window could not be resolved, and cut
/// context that legitimately spans sprints. The lines are now passed through whole, each carrying its
/// own timestamp, with the sprint's date range stated so the model can attribute activity itself.
/// </summary>
public class AssessmentChatScopeTests
{
    private const string Blob =
        "[2026-04-11 07:00:00] dev@x.com: Found a problem with the report numbers.\n" +
        "[2026-04-11 07:02:00] pm@x.com: Go ahead.\n" +
        "second line of the same message\n" +
        "\n" +
        "[2026-06-01 09:00:00] dev@x.com: Much later, different sprint.\n";

    [Fact]
    public void EveryNonEmptyLineIsKept_IncludingOutsideAnySprintWindow()
    {
        var lines = MetricsController.AllChatBlobLines(Blob);

        Assert.Equal(4, lines.Count);
        Assert.Contains(lines, l => l.Contains("Much later", StringComparison.Ordinal));
    }

    /// <summary>
    /// The window filter dropped continuation lines because they carry no timestamp, silently
    /// truncating multi-line messages mid-sentence.
    /// </summary>
    [Fact]
    public void ContinuationLinesSurvive_UnlikeTheWindowFilter()
    {
        var kept = MetricsController.AllChatBlobLines(Blob);
        var filtered = MetricsController.FilterChatBlobByWindow(
            Blob, new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc));

        Assert.Contains(kept, l => l == "second line of the same message");
        Assert.DoesNotContain(filtered, l => l == "second line of the same message");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyBlobYieldsNoLines(string? blob)
    {
        Assert.Empty(MetricsController.AllChatBlobLines(blob));
    }

    // ------------------------------------------------------------------ scope note

    /// <summary>
    /// Relevance is judged by content, not by date: a thread routinely spans sprints, so the note
    /// must not push the model back towards filtering by when something was said.
    /// </summary>
    [Fact]
    public void TheNoteDirectsJudgementByContentRatherThanDate()
    {
        var note = MetricsController.ChatScopeNote;

        Assert.Contains("judge relevance by what is being discussed, not by when it was said", note, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("regardless of its date", note, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not dismiss a message as irrelevant merely because it falls outside this sprint", note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheNoteCarriesNoSprintDateRange()
    {
        // A date range here would reintroduce exactly the filtering this replaced.
        Assert.DoesNotMatch(@"\d{4}-\d{2}-\d{2}", MetricsController.ChatScopeNote);
    }

    [Fact]
    public void ChatSectionRendersEveryLine()
    {
        var sb = new StringBuilder();
        MetricsController.AppendChatBlobSection(sb, "### Group chat", MetricsController.AllChatBlobLines(Blob), haveWindow: true);
        var text = sb.ToString();

        Assert.Contains("Found a problem", text, StringComparison.Ordinal);
        Assert.Contains("Much later", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(none for this sprint)", text, StringComparison.Ordinal);
    }
}
