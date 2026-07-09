using System.Text.Json;
using strAppersBackend.Controllers;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Board-creation schedule decisions for day-based courses (plan items 5 + 10):
/// kickoff choice, template value survival through the load-time overrides,
/// merge-row cadence, and ProjectBoard.DueDate derivation.
/// </summary>
public class BoardCreationSprintDaysTests
{
    private static readonly TimeSpan Gmt2 = TimeSpan.FromHours(2);

    private static TrelloProjectCreationRequest DayTemplate(int days, int sprintCount) => new()
    {
        ProjectId = 1,
        SprintLengthWeeks = 1,
        SprintLengthDays = days,
        ProjectLengthWeeks = sprintCount, // course-builder misnomer: sprint count
        SprintPlan = new TrelloSprintPlan
        {
            TotalSprints = sprintCount,
            EstimatedWeeks = sprintCount,
            Lists = Enumerable.Range(1, sprintCount)
                .Select(n => new TrelloList { Name = $"Sprint{n}" })
                .Concat(new[] { new TrelloList { Name = "Bugs" }, new TrelloList { Name = "User Stories" } })
                .ToList(),
        },
    };

    [Fact]
    public void SavedTemplate_SprintLengthDays_NotOverwrittenOnLoad()
    {
        // Mirrors the CreateBoard load sequence: only ProjectLengthWeeks/SprintLengthWeeks
        // are clobbered with global config; SprintLengthDays must survive.
        var saved = JsonSerializer.Deserialize<TrelloProjectCreationRequest>(
            JsonSerializer.Serialize(DayTemplate(days: 3, sprintCount: 10)))!;

        saved.ProjectLengthWeeks = 12; // global config override (as in CreateBoard)
        saved.SprintLengthWeeks = 1;   // global config override (as in CreateBoard)

        Assert.Equal(3, saved.SprintLengthDays);
    }

    [Fact]
    public void Schedule_WithDays3_UsesDayBasedKickoffAndDueDates()
    {
        var utcNow = new DateTime(2026, 7, 6, 6, 0, 0, DateTimeKind.Utc);
        var kickoff = TrelloBoardScheduleHelper.GetDayBasedKickoffUtc(utcNow, Gmt2);
        var due1 = TrelloBoardScheduleHelper.GetSprintDueDateUtcForDays(kickoff, 1, 3, Gmt2);
        var due2 = TrelloBoardScheduleHelper.GetSprintDueDateUtcForDays(kickoff, 2, 3, Gmt2);

        // Kickoff next day 10:00 local; due dates chain +3 days per sprint.
        Assert.Equal(new DateTime(2026, 7, 7, 8, 0, 0), kickoff);
        Assert.Equal(due1.AddDays(3), due2);
        Assert.True(due1 > kickoff);
    }

    [Fact]
    public void MergeRowSeed_NextInvisibleSprint_DueMatchesDayCadence()
    {
        // MergeType=Add seeds a row for sprint VisibleSprints+1 (e.g. 3) with no list yet.
        var kickoff = new DateTime(2026, 7, 7, 8, 0, 0, DateTimeKind.Utc);
        const int visibleSprints = 2;

        var due = TrelloBoardScheduleHelper.GetSprintDueDateUtcForDays(kickoff, visibleSprints + 1, 3, Gmt2);

        // Sprint 3 of a 3-day cadence: kickoff date + (3×3 − 1) = +8 days, end of day local.
        var expected = new DateTime(2026, 7, 7).AddDays(8).AddDays(1).AddTicks(-1).Subtract(Gmt2);
        Assert.Equal(expected, due);
    }

    [Fact]
    public void NextMeetingTime_EqualsDayBasedKickoff_AndInsideSprint1Window()
    {
        // NextMeetingTime = kickoffUtc at creation; must land inside sprint 1's window
        // so the attendance metric counts the kickoff meeting.
        var utcNow = new DateTime(2026, 7, 6, 6, 0, 0, DateTimeKind.Utc);
        var kickoff = TrelloBoardScheduleHelper.GetDayBasedKickoffUtc(utcNow, Gmt2);
        var due1 = TrelloBoardScheduleHelper.GetSprintDueDateUtcForDays(kickoff, 1, 3, Gmt2);
        var window1Start = due1.AddDays(-3).AddTicks(1); // inclusive window start for a 3-day sprint

        Assert.InRange(kickoff, window1Start, due1);
    }

    // ── ProjectBoard.DueDate (item 10) ────────────────────────────────────────

    [Fact]
    public void BoardDueDate_Days3Sprints10_IsKickoffPlus30Days()
    {
        var kickoff = new DateTime(2026, 7, 7, 8, 0, 0, DateTimeKind.Utc);
        var template = DayTemplate(days: 3, sprintCount: 10);

        var due = BoardsController.ComputeBoardDueDateUtc(3, kickoff, template, projectLengthWeeks: 12);

        // 10 Sprint lists × 3 days — Bugs and User Stories lists NOT counted.
        Assert.Equal(kickoff.AddDays(30), due);
    }

    [Fact]
    public void BoardDueDate_SprintListCount_IgnoresBugsAndUserStories()
    {
        var kickoff = new DateTime(2026, 7, 7, 8, 0, 0, DateTimeKind.Utc);
        var template = DayTemplate(days: 2, sprintCount: 4); // 4 sprint lists + Bugs + User Stories

        var due = BoardsController.ComputeBoardDueDateUtc(2, kickoff, template, projectLengthWeeks: 12);

        Assert.Equal(kickoff.AddDays(8), due); // 4 × 2, not 6 × 2
    }

    [Fact]
    public void BoardDueDate_LegacyTemplate_IsConfigWeeksFromNow()
    {
        var kickoff = new DateTime(2026, 7, 7, 8, 0, 0, DateTimeKind.Utc);
        var before = DateTime.UtcNow.AddDays(12 * 7).AddMinutes(-1);

        var due = BoardsController.ComputeBoardDueDateUtc(null, kickoff, DayTemplate(3, 10), projectLengthWeeks: 12);

        var after = DateTime.UtcNow.AddDays(12 * 7).AddMinutes(1);
        Assert.InRange(due, before, after);
    }

    [Fact]
    public void BoardDueDate_NoSprintLists_FallsBackToTotalSprints()
    {
        var kickoff = new DateTime(2026, 7, 7, 8, 0, 0, DateTimeKind.Utc);
        var template = new TrelloProjectCreationRequest
        {
            SprintLengthDays = 3,
            SprintPlan = new TrelloSprintPlan { TotalSprints = 5, Lists = new List<TrelloList>() },
        };

        var due = BoardsController.ComputeBoardDueDateUtc(3, kickoff, template, projectLengthWeeks: 12);

        Assert.Equal(kickoff.AddDays(15), due);
    }
}
