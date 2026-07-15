using System.Text.Json;
using strAppersBackend.Models;

namespace strAppersBackend.Utilities;

/// <summary>
/// Resolves sprint calendar windows from <see cref="ProjectBoard.SprintPlan"/> JSON (Trello template lists with optional dates)
/// or from board start + plan totals.
/// </summary>
public static class SprintPlanDateResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Inclusive UTC range from <see cref="ProjectBoardSprintMerge"/> — same rules as
    /// <c>GET /api/Boards/use/sprint-schedule</c> (DueDate, MergedAt for sprint 1, and <paramref name="sprintLengthInDays"/>).
    /// Prefer this over <see cref="TryGetSprintInclusiveUtcRange"/> when merge rows exist so CRM and the board UI agree.
    /// Callers supply DAYS (resolve per board via <see cref="SprintLengthResolver"/>; legacy = configWeeks × 7).
    /// </summary>
    public static bool TryGetInclusiveUtcRangeFromSprintMerge(
        ProjectBoardSprintMerge? merge,
        int sprintNumber,
        int sprintLengthInDays,
        out DateTime startUtc,
        out DateTime endInclusiveUtc)
    {
        startUtc = default;
        endInclusiveUtc = default;
        if (merge == null || sprintNumber <= 0 || merge.DueDate == null)
            return false;

        var sprintDays = Math.Max(1, sprintLengthInDays);
        var endRaw = ToUtc(merge.DueDate.Value);
        endInclusiveUtc = endRaw.TimeOfDay == TimeSpan.Zero
            ? endRaw.Date.AddDays(1).AddTicks(-1)
            : endRaw;

        if (sprintNumber == 1)
        {
            if (merge.MergedAt == null)
                return false;
            startUtc = ToUtc(merge.MergedAt.Value);
        }
        else
        {
            // Contiguous windows: start = one tick after the previous sprint's inclusive end
            // (derived from the NORMALIZED end so end-of-day dues yield start-of-day starts).
            // The old "due − (days − 1)" preserved the due's time-of-day, leaving the first
            // day's daytime in no window when dues are 23:59:59-style.
            startUtc = endInclusiveUtc.AddDays(-sprintDays).AddTicks(1);
        }

        if (endInclusiveUtc < startUtc)
            (startUtc, endInclusiveUtc) = (endInclusiveUtc, startUtc);
        return true;
    }

    /// <summary>
    /// Inclusive UTC range for stakeholders / time-bounded queries: <c>CreatedAt &gt;= start</c> and <c>CreatedAt &lt;= endInclusive</c>.
    /// Uses <see cref="ProjectBoard.SprintPlan"/> list Start/End dates, or board <see cref="ProjectBoard.StartDate"/> + plan totals as fallback.
    /// </summary>
    public static bool TryGetSprintInclusiveUtcRange(
        string? sprintPlanJson,
        DateTime? boardStartDateUtc,
        int sprintNumber,
        out DateTime startUtc,
        out DateTime endInclusiveUtc,
        int? sprintLengthDaysOverride = null)
    {
        startUtc = default;
        endInclusiveUtc = default;
        if (sprintNumber <= 0 || string.IsNullOrWhiteSpace(sprintPlanJson))
            return false;

        try
        {
            var plan = JsonSerializer.Deserialize<TrelloSprintPlan>(sprintPlanJson, JsonOptions);
            if (plan?.Lists == null || plan.Lists.Count == 0)
                return TryFallbackFromBoardStart(plan, boardStartDateUtc, sprintNumber, sprintLengthDaysOverride, out startUtc, out endInclusiveUtc);

            TrelloList? match = null;
            foreach (var list in plan.Lists)
            {
                if (string.IsNullOrWhiteSpace(list.Name)) continue;
                if (ListNameMatchesSprint(list.Name, sprintNumber))
                {
                    match = list;
                    break;
                }
            }

            if (match == null || match.StartDate == null || match.EndDate == null)
                return TryFallbackFromBoardStart(plan, boardStartDateUtc, sprintNumber, sprintLengthDaysOverride, out startUtc, out endInclusiveUtc);

            startUtc = ToUtc(match.StartDate.Value);
            var endRaw = ToUtc(match.EndDate.Value);
            endInclusiveUtc = endRaw.TimeOfDay == TimeSpan.Zero
                ? endRaw.Date.AddDays(1).AddTicks(-1)
                : endRaw;
            if (endInclusiveUtc < startUtc)
                (startUtc, endInclusiveUtc) = (endInclusiveUtc, startUtc);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ListNameMatchesSprint(string name, int sprintNumber)
    {
        var n = name.Trim();
        if (string.Equals(n, $"Sprint {sprintNumber}", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(n, $"Sprint{sprintNumber}", StringComparison.OrdinalIgnoreCase))
            return true;
        return n.Contains($"Sprint {sprintNumber}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryFallbackFromBoardStart(
        TrelloSprintPlan? plan,
        DateTime? boardStartDateUtc,
        int sprintNumber,
        int? sprintLengthDaysOverride,
        out DateTime startUtc,
        out DateTime endInclusiveUtc)
    {
        startUtc = default;
        endInclusiveUtc = default;
        if (boardStartDateUtc == null)
            return false;

        var originRaw = boardStartDateUtc.Value;
        var origin = originRaw.Kind switch
        {
            DateTimeKind.Utc => originRaw,
            DateTimeKind.Local => originRaw.ToUniversalTime(),
            _ => DateTime.SpecifyKind(originRaw, DateTimeKind.Utc),
        };

        double daysPerSprint;
        if (sprintLengthDaysOverride is int overrideDays)
        {
            // Day-based course: the caller resolved the cadence from the template; the
            // EstimatedWeeks heuristic below assumes weekly sprints and must not be used.
            daysPerSprint = Math.Max(1, overrideDays);
        }
        else
        {
            var totalSprints = plan?.TotalSprints ?? 0;
            if (totalSprints <= 0 && plan?.Lists != null)
            {
                totalSprints = plan.Lists.Count(l => ListNameMatchesAnySprint(l.Name));
            }

            if (totalSprints <= 0)
                totalSprints = 1;

            var estimatedWeeks = plan?.EstimatedWeeks ?? 0;
            if (estimatedWeeks <= 0)
                estimatedWeeks = totalSprints;

            daysPerSprint = Math.Max(1.0, 7.0 * estimatedWeeks / totalSprints);
        }

        startUtc = origin.AddDays(daysPerSprint * (sprintNumber - 1));
        var endExclusive = origin.AddDays(daysPerSprint * sprintNumber);
        endInclusiveUtc = endExclusive.AddTicks(-1);
        return endInclusiveUtc >= startUtc;
    }

    private static bool ListNameMatchesAnySprint(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return name.Contains("Sprint", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime ToUtc(DateTime dt)
    {
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
        };
    }
}
