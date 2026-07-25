using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;

namespace strAppersBackend.Services;

public record KickoffResetCheckResult
{
    public string BoardId { get; init; } = string.Empty;
    public bool BoardFound { get; init; }
    public int? KickoffState { get; init; }
    public bool IsStale { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public int ConfiguredTimeoutMinutes { get; init; }
    public DateTime? DeadlineUtc { get; init; }
    public DateTime NowUtc { get; init; }
    public double? MinutesUntilDeadline { get; init; }
    public bool WouldReset { get; init; }
    public bool DidReset { get; init; }
    public int StudentsOnBoard { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public interface IKickoffResetService
{
    /// <summary>
    /// Checks a board's kickoff deadline and, if it's passed and the squad still hasn't
    /// unanimously agreed, resets it (IsStale=true; every student's Status/ProjectId/BoardId/
    /// InstitutePriority1-4/ApprovedKickoff cleared) and emails the reset notice. Cheap no-op for
    /// the common case (board not yet due) — safe to call on every page load, not just login.
    /// </summary>
    Task<KickoffResetCheckResult> CheckAndResetAsync(string boardId);
}

public class KickoffResetService : IKickoffResetService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ISmtpEmailService _smtpEmailService;
    private readonly ILogger<KickoffResetService> _logger;

    public KickoffResetService(ApplicationDbContext context, IConfiguration configuration, ISmtpEmailService smtpEmailService, ILogger<KickoffResetService> logger)
    {
        _context = context;
        _configuration = configuration;
        _smtpEmailService = smtpEmailService;
        _logger = logger;
    }

    public async Task<KickoffResetCheckResult> CheckAndResetAsync(string boardId)
    {
        var nowUtc = DateTime.UtcNow;
        var timeoutMinutes = _configuration.GetValue<int>("KickoffConfig2:BoardTimeout", 4320);

        if (timeoutMinutes <= 0)
        {
            _logger.LogInformation("[KICKOFF-RESET] BoardId={BoardId}: BoardTimeout is {Minutes}; disabled, skipping.", boardId, timeoutMinutes);
            return new KickoffResetCheckResult { BoardId = boardId, ConfiguredTimeoutMinutes = timeoutMinutes, NowUtc = nowUtc, Reason = "BoardTimeout <= 0 (disabled)" };
        }

        // Cheap, unlocked pre-check first — this now runs on every kickoff-relevant page load /
        // stats poll (not just login), so avoid opening a transaction for the common "not due yet" case.
        var quick = await _context.ProjectBoards.AsNoTracking()
            .Where(b => b.Id == boardId)
            .Select(b => new { b.KickoffState, b.IsStale, b.CreatedAt })
            .FirstOrDefaultAsync();

        if (quick == null)
        {
            _logger.LogInformation("[KICKOFF-RESET] BoardId={BoardId}: not found.", boardId);
            return new KickoffResetCheckResult { BoardId = boardId, BoardFound = false, ConfiguredTimeoutMinutes = timeoutMinutes, NowUtc = nowUtc, Reason = "Board not found" };
        }

        var deadlineUtc = quick.CreatedAt.AddMinutes(timeoutMinutes);
        var minutesUntilDeadline = (deadlineUtc - nowUtc).TotalMinutes;

        _logger.LogInformation(
            "[KICKOFF-RESET] BoardId={BoardId}: KickoffState={KickoffState}, IsStale={IsStale}, CreatedAtUtc={CreatedAtUtc:O}, NowUtc={NowUtc:O}, TimeoutMinutes={TimeoutMinutes}, DeadlineUtc={DeadlineUtc:O}, MinutesUntilDeadline={MinutesUntilDeadline}",
            boardId, quick.KickoffState?.ToString() ?? "null", quick.IsStale, quick.CreatedAt, nowUtc, timeoutMinutes, deadlineUtc, minutesUntilDeadline);

        var baseResult = new KickoffResetCheckResult
        {
            BoardId = boardId,
            BoardFound = true,
            KickoffState = quick.KickoffState,
            IsStale = quick.IsStale,
            CreatedAtUtc = quick.CreatedAt,
            ConfiguredTimeoutMinutes = timeoutMinutes,
            DeadlineUtc = deadlineUtc,
            NowUtc = nowUtc,
            MinutesUntilDeadline = minutesUntilDeadline,
        };

        if (quick.KickoffState == null)
            return baseResult with { WouldReset = false, Reason = "KickoffState is null (legacy board, not part of this flow)" };
        if (quick.KickoffState >= 2)
            return baseResult with { WouldReset = false, Reason = "KickoffState >= 2 (already agreed)" };
        if (quick.IsStale)
            return baseResult with { WouldReset = false, Reason = "Already IsStale (already reset)" };
        if (nowUtc < deadlineUtc)
            return baseResult with { WouldReset = false, Reason = $"Deadline not reached yet ({minutesUntilDeadline:0.0} minutes remaining)" };

        // Eligible — acquire the row lock and re-check inside it (another request, e.g. a
        // concurrent login or another tab's poll, may have just reset it).
        using var transaction = await _context.Database.BeginTransactionAsync();
        var lockedBoard = await _context.ProjectBoards
            .FromSqlInterpolated($"SELECT * FROM \"ProjectBoards\" WHERE \"BoardId\" = {boardId} FOR UPDATE")
            .FirstOrDefaultAsync();

        if (lockedBoard == null || lockedBoard.KickoffState == null || lockedBoard.KickoffState >= 2 || lockedBoard.IsStale
            || DateTime.UtcNow < lockedBoard.CreatedAt.AddMinutes(timeoutMinutes))
        {
            await transaction.CommitAsync();
            _logger.LogInformation("[KICKOFF-RESET] BoardId={BoardId}: no longer eligible after acquiring lock (raced with another trigger).", boardId);
            return baseResult with { WouldReset = true, DidReset = false, Reason = "Raced with another trigger — already handled" };
        }

        lockedBoard.IsStale = true;
        lockedBoard.UpdatedAt = DateTime.UtcNow;

        var squad = await _context.Students.Where(s => s.BoardId == boardId).ToListAsync();
        var resetRecipients = new List<(string Email, string FirstName)>();
        foreach (var s in squad)
        {
            s.Status = 0;
            s.ProjectId = null;
            s.BoardId = null;
            s.InstitutePriority1 = null;
            s.InstitutePriority2 = null;
            s.InstitutePriority3 = null;
            s.InstitutePriority4 = null;
            s.ApprovedKickoff = false;
            s.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(s.Email)) resetRecipients.Add((s.Email, s.FirstName));
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        _logger.LogInformation("[KICKOFF-RESET] BoardId={BoardId}: RESET — IsStale=true, {Count} student(s) reset.", boardId, squad.Count);

        foreach (var (email, firstName) in resetRecipients)
        {
            try
            {
                await _smtpEmailService.SendPlainEmailAsync(
                    email,
                    "Your squad needs to restart project selection",
                    KickoffEmailTemplates.BuildResetNoticeEmailHtml(firstName));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[KICKOFF-RESET] Failed to send reset-notice email to {Email}", email);
            }
        }

        return baseResult with { WouldReset = true, DidReset = true, IsStale = true, StudentsOnBoard = squad.Count, Reason = "Reset performed" };
    }
}
