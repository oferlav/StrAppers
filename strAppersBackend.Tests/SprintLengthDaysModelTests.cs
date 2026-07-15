using System.Text.Json;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// SprintLengthDays travels inside the template JSON (TrelloProjectCreationRequest) — no DB column.
/// These tests lock the JSON round-trip and the legacy-null fallback semantics.
/// </summary>
public class SprintLengthDaysModelTests
{
    [Fact]
    public void SprintLengthDays_RoundTripsThroughJson()
    {
        var request = new TrelloProjectCreationRequest
        {
            ProjectId = 42,
            SprintLengthWeeks = 1,
            SprintLengthDays = 3,
        };

        var json = JsonSerializer.Serialize(request);
        var back = JsonSerializer.Deserialize<TrelloProjectCreationRequest>(json);

        Assert.NotNull(back);
        Assert.Equal(3, back!.SprintLengthDays);
        Assert.Equal(1, back.SprintLengthWeeks);
    }

    [Fact]
    public void LegacyJson_WithoutProperty_DeserializesToNull()
    {
        const string legacyJson = """{"ProjectId":7,"SprintLengthWeeks":1,"ProjectLengthWeeks":10}""";

        var back = JsonSerializer.Deserialize<TrelloProjectCreationRequest>(legacyJson);

        Assert.NotNull(back);
        Assert.Null(back!.SprintLengthDays);
    }

    [Fact]
    public void BuiltTemplateJson_CarriesSprintLengthDays_ReadableByResolver()
    {
        // Shape mirrors CourseBoardBuilderService board assembly: SprintLengthDays = config.SprintLengthInDays,
        // serialized with System.Text.Json defaults (same as InstituteTemplate.TrelloBoardJson storage).
        var board = new TrelloProjectCreationRequest
        {
            ProjectId = 1,
            ProjectTitle = "Course",
            ProjectLengthWeeks = 10, // misnomer: course builder stores sprint COUNT here
            SprintLengthWeeks = 1,
            SprintLengthDays = 3,
            SprintPlan = new TrelloSprintPlan { TotalSprints = 10, EstimatedWeeks = 10 },
        };
        var templateJson = JsonSerializer.Serialize(board);

        var days = strAppersBackend.Utilities.SprintLengthResolver.ResolveFromJson(templateJson, configSprintLengthWeeks: 1);

        Assert.Equal(3, days);
    }
}

/// <summary>Pure unit tests for the day-based kickoff and due-date schedule helpers.</summary>
public class TrelloBoardScheduleHelperDayTests
{
    private static readonly TimeSpan Gmt2 = TimeSpan.FromHours(2);

    [Fact]
    public void Kickoff_MorningCreation_IsNextDay10Local()
    {
        // 2026-07-06 06:00 UTC = 08:00 local (GMT+2) → kickoff 2026-07-07 10:00 local = 08:00 UTC
        var utcNow = new DateTime(2026, 7, 6, 6, 0, 0, DateTimeKind.Utc);

        var kickoff = strAppersBackend.Services.TrelloBoardScheduleHelper.GetDayBasedKickoffUtc(utcNow, Gmt2);

        Assert.Equal(new DateTime(2026, 7, 7, 8, 0, 0), kickoff);
    }

    [Fact]
    public void Kickoff_LateNightCreation_RollsToCorrectLocalDay()
    {
        // 2026-07-06 23:30 UTC = 2026-07-07 01:30 local → kickoff 2026-07-08 10:00 local = 08:00 UTC
        var utcNow = new DateTime(2026, 7, 6, 23, 30, 0, DateTimeKind.Utc);

        var kickoff = strAppersBackend.Services.TrelloBoardScheduleHelper.GetDayBasedKickoffUtc(utcNow, Gmt2);

        Assert.Equal(new DateTime(2026, 7, 8, 8, 0, 0), kickoff);
    }

    [Fact]
    public void Kickoff_NeverSameDay_EvenAtMidnightLocal()
    {
        // 2026-07-05 22:00 UTC = 2026-07-06 00:00 local exactly → kickoff 2026-07-07 10:00 local
        var utcNow = new DateTime(2026, 7, 5, 22, 0, 0, DateTimeKind.Utc);

        var kickoff = strAppersBackend.Services.TrelloBoardScheduleHelper.GetDayBasedKickoffUtc(utcNow, Gmt2);

        Assert.Equal(new DateTime(2026, 7, 7, 8, 0, 0), kickoff);
    }

    [Fact]
    public void DueDate_Sprint1_ThreeDays_EndsTwoDaysAfterKickoffDate()
    {
        // Kickoff local Tue 2026-07-07 10:00 (08:00 UTC). 3-day sprint 1 → due Thu 2026-07-09 end-of-day local.
        var kickoffUtc = new DateTime(2026, 7, 7, 8, 0, 0, DateTimeKind.Utc);

        var due = strAppersBackend.Services.TrelloBoardScheduleHelper.GetSprintDueDateUtcForDays(kickoffUtc, 1, 3, Gmt2);

        var expectedLocalEnd = new DateTime(2026, 7, 9).AddDays(1).AddTicks(-1); // 2026-07-09 23:59:59.9999999 local
        Assert.Equal(expectedLocalEnd.Subtract(Gmt2), due);
    }

    [Fact]
    public void DueDate_SprintN_ChainsWithoutOverlapOrGap()
    {
        var kickoffUtc = new DateTime(2026, 7, 7, 8, 0, 0, DateTimeKind.Utc);

        var due1 = strAppersBackend.Services.TrelloBoardScheduleHelper.GetSprintDueDateUtcForDays(kickoffUtc, 1, 3, Gmt2);
        var due2 = strAppersBackend.Services.TrelloBoardScheduleHelper.GetSprintDueDateUtcForDays(kickoffUtc, 2, 3, Gmt2);

        // Consecutive 3-day sprints: due2 is exactly 3 days after due1.
        Assert.Equal(due1.AddDays(3), due2);
    }

    [Fact]
    public void DueDate_TimezoneConversion_ProducesUtc()
    {
        // GMT+2: due UTC = local end-of-day − 2h.
        var kickoffUtc = new DateTime(2026, 7, 7, 8, 0, 0, DateTimeKind.Utc);

        var due = strAppersBackend.Services.TrelloBoardScheduleHelper.GetSprintDueDateUtcForDays(kickoffUtc, 1, 3, Gmt2);

        Assert.Equal(21, due.Hour); // 23:59 local − 2h = 21:59 UTC
        Assert.Equal(59, due.Minute);
    }

    [Fact]
    public void DueDate_ZeroOrNegativeDays_ClampedToOne()
    {
        var kickoffUtc = new DateTime(2026, 7, 7, 8, 0, 0, DateTimeKind.Utc);

        var dueZero = strAppersBackend.Services.TrelloBoardScheduleHelper.GetSprintDueDateUtcForDays(kickoffUtc, 1, 0, Gmt2);
        var dueOne = strAppersBackend.Services.TrelloBoardScheduleHelper.GetSprintDueDateUtcForDays(kickoffUtc, 1, 1, Gmt2);

        Assert.Equal(dueOne, dueZero);
    }

    [Fact]
    public void Weekly_Helpers_Unchanged_ForFixedDate()
    {
        // Regression: weekly sprint start/due math unaffected by the new day-based helpers.
        var projectStartUtc = new DateTime(2026, 7, 5, 8, 0, 0, DateTimeKind.Utc); // Sunday
        var (start, due) = strAppersBackend.Services.TrelloBoardScheduleHelper.GetSprintStartAndDueUtc(
            1, projectStartUtc, DayOfWeek.Sunday, Gmt2);

        // Sunday 2026-07-05 local 00:00 → start; Saturday 2026-07-11 end-of-day local → due.
        Assert.Equal(new DateTime(2026, 7, 5).Subtract(Gmt2), start);
        Assert.Equal(new DateTime(2026, 7, 11).AddDays(1).AddTicks(-1).Subtract(Gmt2), due);
    }
}
