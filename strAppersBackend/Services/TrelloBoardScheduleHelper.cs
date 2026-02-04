namespace strAppersBackend.Services;

/// <summary>
/// Helper for Trello board schedule: first day of week, local timezone, kickoff at 10:00 local, sprint start/due.
/// </summary>
public static class TrelloBoardScheduleHelper
{
    /// <summary>
    /// Parse FirstDayOfWeek string to DayOfWeek. Defaults to Sunday if invalid.
    /// </summary>
    public static DayOfWeek ParseFirstDayOfWeek(string? firstDayOfWeek)
    {
        if (string.IsNullOrWhiteSpace(firstDayOfWeek))
            return DayOfWeek.Sunday;
        return firstDayOfWeek.Trim().ToLowerInvariant() switch
        {
            "sunday" or "sun" => DayOfWeek.Sunday,
            "monday" or "mon" => DayOfWeek.Monday,
            "tuesday" or "tue" => DayOfWeek.Tuesday,
            "wednesday" or "wed" => DayOfWeek.Wednesday,
            "thursday" or "thu" => DayOfWeek.Thursday,
            "friday" or "fri" => DayOfWeek.Friday,
            "saturday" or "sat" => DayOfWeek.Saturday,
            _ => DayOfWeek.Sunday
        };
    }

    /// <summary>
    /// Parse LocalTime string (e.g. "GMT+2", "UTC", "GMT-5") to UTC offset. Returns TimeSpan.
    /// </summary>
    public static TimeSpan ParseLocalTimeOffset(string? localTime)
    {
        if (string.IsNullOrWhiteSpace(localTime))
            return TimeSpan.FromHours(2); // default GMT+2
        var s = localTime.Trim().ToUpperInvariant();
        if (s == "UTC" || s == "GMT" || s == "GMT+0" || s == "GMT-0")
            return TimeSpan.Zero;
        // GMT+2, GMT-5, UTC+3, etc.
        if (s.StartsWith("GMT+", StringComparison.Ordinal) || s.StartsWith("UTC+", StringComparison.Ordinal))
        {
            if (int.TryParse(s.AsSpan(4), out int h))
                return TimeSpan.FromHours(h);
        }
        if (s.StartsWith("GMT-", StringComparison.Ordinal) || s.StartsWith("UTC-", StringComparison.Ordinal))
        {
            if (int.TryParse(s.AsSpan(4), out int h))
                return TimeSpan.FromHours(-h);
        }
        return TimeSpan.FromHours(2);
    }

    /// <summary>
    /// Get the next occurrence of firstDayOfWeek at 10:00 in local time, returned as UTC.
    /// </summary>
    public static DateTime GetNextKickoffUtc(DayOfWeek firstDayOfWeek, TimeSpan localOffset)
    {
        var utcNow = DateTime.UtcNow;
        var localNow = utcNow.Add(localOffset);
        var localDate = localNow.Date;
        var targetLocal = localDate;
        var daysToAdd = ((int)firstDayOfWeek - (int)localDate.DayOfWeek + 7) % 7;
        if (daysToAdd == 0 && localNow.TimeOfDay >= TimeSpan.FromHours(10))
            daysToAdd = 7;
        targetLocal = localDate.AddDays(daysToAdd).AddHours(10);
        return targetLocal.Subtract(localOffset);
    }

    /// <summary>
    /// Last day of the week when first day is given (e.g. Sunday -> Saturday).
    /// </summary>
    public static DayOfWeek GetLastDayOfWeekend(DayOfWeek firstDayOfWeek)
    {
        var last = (int)firstDayOfWeek + 6;
        if (last > 6) last -= 7;
        return (DayOfWeek)last;
    }

    /// <summary>
    /// Get sprint start (first day of week at 00:00 local) and due (last day of weekend at end-of-day local) for 1-based sprint number.
    /// projectStartUtc is the reference (e.g. board creation or kickoff). Returns (startUtc, dueUtc).
    /// </summary>
    public static (DateTime StartUtc, DateTime DueUtc) GetSprintStartAndDueUtc(
        int sprintNumber,
        DateTime projectStartUtc,
        DayOfWeek firstDayOfWeek,
        TimeSpan localOffset)
    {
        var localStart = projectStartUtc.Add(localOffset).Date;
        var firstDay = (int)firstDayOfWeek;
        var currentDay = (int)localStart.DayOfWeek;
        var daysToFirst = (firstDay - currentDay + 7) % 7;
        var weekStart = localStart.AddDays(daysToFirst);
        var sprintWeekStart = weekStart.AddDays((sprintNumber - 1) * 7);
        var lastDay = GetLastDayOfWeekend(firstDayOfWeek);
        var sprintWeekEnd = sprintWeekStart.AddDays((int)lastDay - (int)firstDayOfWeek);
        if ((int)lastDay < (int)firstDayOfWeek)
            sprintWeekEnd = sprintWeekEnd.AddDays(7);
        var startLocal = sprintWeekStart; // 00:00
        var dueLocal = sprintWeekEnd.AddDays(1).AddTicks(-1); // 23:59:59.9999999
        var startUtc = startLocal.Subtract(localOffset);
        var dueUtc = dueLocal.Subtract(localOffset);
        return (startUtc, dueUtc);
    }

    /// <summary>
    /// Convert UTC time to local for display using the given offset. Returns DateTime in "local" (Unspecified) for formatting.
    /// </summary>
    public static DateTime UtcToLocalForDisplay(DateTime utc, TimeSpan localOffset)
    {
        return DateTime.SpecifyKind(utc.Add(localOffset), DateTimeKind.Unspecified);
    }
}
