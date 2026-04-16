using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

public partial class MentorController
{
    private const string CacheReviewRoleFullStack = "Full Stack Developer";
    private const string CacheReviewRoleBackend = "Backend Developer";
    private const string CacheReviewRoleFrontend = "Frontend Developer";

    /// <summary>Parses sprint number from task branch names like "2-F", "Bugs-B".</summary>
    private static bool TryGetSprintNumberFromGithubTaskBranch(string githubBranch, out int sprintNumber)
    {
        sprintNumber = 0;
        var branchParts = githubBranch.Split('-');
        if (branchParts.Length == 2 && branchParts[0].Equals("Bugs", StringComparison.OrdinalIgnoreCase) &&
            (branchParts[1].Equals("B", StringComparison.OrdinalIgnoreCase) ||
             branchParts[1].Equals("F", StringComparison.OrdinalIgnoreCase)))
        {
            sprintNumber = 0;
            return true;
        }

        if (branchParts.Length != 2 || !int.TryParse(branchParts[0], out sprintNumber))
            return false;
        return true;
    }

    /// <summary>
    /// From branch name: <c>*-B</c> / <c>Bugs-B</c> = backend track; <c>*-F</c> / <c>Bugs-F</c> = frontend track.
    /// </summary>
    private static bool TryGetBackendFrontendFromGithubTaskBranch(string githubBranch, out bool isBackend, out bool isFrontend)
    {
        isBackend = false;
        isFrontend = false;
        var branchParts = githubBranch.Split('-');
        if (branchParts.Length != 2)
            return false;

        var track = branchParts[1].Trim();
        if (branchParts[0].Equals("Bugs", StringComparison.OrdinalIgnoreCase))
        {
            if (track.Equals("B", StringComparison.OrdinalIgnoreCase))
            {
                isBackend = true;
                return true;
            }

            if (track.Equals("F", StringComparison.OrdinalIgnoreCase))
            {
                isFrontend = true;
                return true;
            }

            return false;
        }

        if (track.Equals("B", StringComparison.OrdinalIgnoreCase))
        {
            isBackend = true;
            return true;
        }

        if (track.Equals("F", StringComparison.OrdinalIgnoreCase))
        {
            isFrontend = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Chooses <see cref="Student.Id"/> for PR/code-review cache when the request has no StudentId: branch suffix
    /// determines backend vs frontend; if anyone on the board has <see cref="CacheReviewRoleFullStack"/>, that student
    /// is used for both tracks; otherwise the active <see cref="CacheReviewRoleBackend"/> or
    /// <see cref="CacheReviewRoleFrontend"/> assignment on this board.
    /// </summary>
    private async Task<int?> ResolveCacheReviewStudentIdForBranchAsync(
        string boardId,
        string githubBranch,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetBackendFrontendFromGithubTaskBranch(githubBranch, out var isBackend, out var isFrontend))
        {
            _logger.LogWarning("CacheReview: could not parse backend/frontend from branch {Branch}", githubBranch);
            return null;
        }

        var rows = await (
            from sr in _context.StudentRoles.AsNoTracking()
            join s in _context.Students.AsNoTracking() on sr.StudentId equals s.Id
            join r in _context.Roles.AsNoTracking() on sr.RoleId equals r.Id
            where sr.IsActive && s.BoardId == boardId
            select new { sr.StudentId, RoleName = r.Name }
        ).ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return null;

        int? PickByRole(string roleName) =>
            rows
                .Where(r => string.Equals(r.RoleName, roleName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.StudentId)
                .Select(r => (int?)r.StudentId)
                .FirstOrDefault();

        var fullStackId = PickByRole(CacheReviewRoleFullStack);
        if (fullStackId.HasValue)
            return fullStackId.Value;

        if (isBackend)
            return PickByRole(CacheReviewRoleBackend);

        if (isFrontend)
            return PickByRole(CacheReviewRoleFrontend);

        return null;
    }

    /// <summary>Persists mentor review output; failures are logged and do not affect the HTTP response.</summary>
    private async Task TryPersistCacheReviewAsync(
        string boardId,
        int studentId,
        int sprintNumber,
        CacheReviewType type,
        string reviewContent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reviewContent))
            return;

        var trimmed = reviewContent.Trim();
        if (trimmed.Length == 0)
            return;

        try
        {
            var maxSeq = await _context.CacheReviews
                .Where(c => c.BoardId == boardId && c.StudentId == studentId && c.SprintNumber == sprintNumber)
                .Select(c => (int?)c.SequenceNumber)
                .MaxAsync(cancellationToken);

            var next = (maxSeq ?? 0) + 1;

            _context.CacheReviews.Add(new CacheReview
            {
                BoardId = boardId,
                StudentId = studentId,
                SprintNumber = sprintNumber,
                SequenceNumber = next,
                Type = type,
                ReviewContent = trimmed
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CacheReview insert failed for board {BoardId}, student {StudentId}, sprint {SprintNumber}, type {Type}",
                boardId, studentId, sprintNumber, type);
        }
    }
}
