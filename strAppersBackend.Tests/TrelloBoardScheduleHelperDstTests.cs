using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Covers TrelloBoardScheduleHelper.ConvertUtcToLocal / GetLocalTimeZone — the DST-aware
/// replacement for the old fixed "GMT+2" offset used to display meeting/kickoff times in emails.
/// Root cause this fixes: Israel is UTC+2 (IST) in winter but UTC+3 (IDT) in summer; a fixed
/// offset was correct only half the year.
/// </summary>
public class TrelloBoardScheduleHelperDstTests
{
    [Fact]
    public void ConvertUtcToLocal_WinterDate_UsesIsraelStandardTimeUtcPlus2()
    {
        // Israel is on standard time (UTC+2) in mid-January.
        var winterUtc = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var local = TrelloBoardScheduleHelper.ConvertUtcToLocal(winterUtc, "Israel Standard Time");
        Assert.Equal(new DateTime(2026, 1, 15, 14, 0, 0), local);
    }

    [Fact]
    public void ConvertUtcToLocal_SummerDate_UsesIsraelDaylightTimeUtcPlus3()
    {
        // Israel is on daylight time (UTC+3) in mid-July.
        var summerUtc = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        var local = TrelloBoardScheduleHelper.ConvertUtcToLocal(summerUtc, "Israel Standard Time");
        Assert.Equal(new DateTime(2026, 7, 15, 15, 0, 0), local);
    }

    [Fact]
    public void ConvertUtcToLocal_NullTimeZoneId_DefaultsToIsraelStandardTime()
    {
        var summerUtc = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        var local = TrelloBoardScheduleHelper.ConvertUtcToLocal(summerUtc, null);
        Assert.Equal(new DateTime(2026, 7, 15, 15, 0, 0), local);
    }

    [Fact]
    public void ConvertUtcToLocal_UnresolvableTimeZoneId_FallsBackToFixedUtcPlus2WithoutThrowing()
    {
        var summerUtc = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        var local = TrelloBoardScheduleHelper.ConvertUtcToLocal(summerUtc, "Not A Real Timezone");
        Assert.Equal(new DateTime(2026, 7, 15, 14, 0, 0), local);
    }

    [Fact]
    public void ConvertUtcToLocal_UnspecifiedKind_TreatedAsUtc()
    {
        var winterUnspecified = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Unspecified);
        var local = TrelloBoardScheduleHelper.ConvertUtcToLocal(winterUnspecified, "Israel Standard Time");
        Assert.Equal(new DateTime(2026, 1, 15, 14, 0, 0), local);
    }
}
