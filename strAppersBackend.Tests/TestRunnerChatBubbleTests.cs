using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests ShouldInjectTestResultBubble — the gate that prevents students from seeing
/// a duplicate chat bubble every time they push without changing the test outcome.
/// Rules:
///   - Always inject on first run (no previous record, previousStatus == null).
///   - Always inject when status changes (FAIL→PASS, PASS→FAIL, etc.).
///   - Skip when status is unchanged (FAIL→FAIL, PASS→PASS, NO_TESTS→NO_TESTS).
/// </summary>
public class TestRunnerChatBubbleTests
{
    [Theory]
    [InlineData(null, "PASS")]
    [InlineData(null, "FAIL")]
    [InlineData(null, "NO_TESTS")]
    public void FirstRun_AlwaysInjects(string? previousStatus, string newStatus)
    {
        Assert.True(MentorController.ShouldInjectTestResultBubble(previousStatus, newStatus));
    }

    [Theory]
    [InlineData("FAIL",     "PASS")]
    [InlineData("PASS",     "FAIL")]
    [InlineData("FAIL",     "NO_TESTS")]
    [InlineData("NO_TESTS", "PASS")]
    [InlineData("NO_TESTS", "FAIL")]
    [InlineData("PASS",     "NO_TESTS")]
    public void StatusChanged_Injects(string previousStatus, string newStatus)
    {
        Assert.True(MentorController.ShouldInjectTestResultBubble(previousStatus, newStatus));
    }

    [Theory]
    [InlineData("PASS",     "PASS")]
    [InlineData("FAIL",     "FAIL")]
    [InlineData("NO_TESTS", "NO_TESTS")]
    public void StatusUnchanged_Skips(string previousStatus, string newStatus)
    {
        Assert.False(MentorController.ShouldInjectTestResultBubble(previousStatus, newStatus));
    }

    [Theory]
    [InlineData("PASS", "pass")]
    [InlineData("FAIL", "fail")]
    [InlineData("pass", "PASS")]
    public void CaseInsensitive_UnchangedIsSkipped(string previousStatus, string newStatus)
    {
        Assert.False(MentorController.ShouldInjectTestResultBubble(previousStatus, newStatus));
    }
}
