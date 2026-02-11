using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;

namespace strAppersBackend.Services
{
    /// <summary>
    /// Runs due sprint merges for boards with students in status=3: for each unique BoardId, merges sprint N when the previous sprint (N-1) DueDate has passed and MergedAt is null (e.g. Sprint 2 DueDate Feb 22 → merge Sprint 3 on Feb 23+).
    /// </summary>
    public class StudentTeamBuilderService : IStudentTeamBuilderService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITrelloSprintMergeService _sprintMergeService;
        private readonly ILogger<StudentTeamBuilderService> _logger;

        public StudentTeamBuilderService(
            ApplicationDbContext context,
            ITrelloSprintMergeService sprintMergeService,
            ILogger<StudentTeamBuilderService> logger)
        {
            _context = context;
            _sprintMergeService = sprintMergeService;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<(int MergedCount, int ErrorCount, IReadOnlyList<string> Errors)> RunDueSprintMergesAsync()
        {
            var errors = new List<string>();
            var mergedCount = 0;
            var errorCount = 0;

            // Unique BoardIds from students with Status=3 and BoardId NOT NULL
            var uniqueBoardIds = await _context.Students
                .Where(s => s.Status == 3 && s.BoardId != null && s.BoardId != "")
                .Select(s => s.BoardId!)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation("[STUDENT-TEAM-BUILDER] Found {Count} unique board(s) for students with status=3 and BoardId not null.", uniqueBoardIds.Count);

            foreach (var boardId in uniqueBoardIds)
            {
                var projectBoard = await _context.ProjectBoards
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pb => pb.Id == boardId);
                if (projectBoard == null)
                {
                    _logger.LogWarning("[STUDENT-TEAM-BUILDER] BoardId={BoardId} has no ProjectBoard; skipping.", boardId);
                    continue;
                }
                var projectId = projectBoard.ProjectId;

                // Merge sprint N when the *previous* sprint (N-1) DueDate has passed (e.g. Sprint 2 DueDate Feb 22 → merge Sprint 3 on Feb 23+)
                var nowUtc = DateTime.UtcNow;
                var allMerges = await _context.ProjectBoardSprintMerges
                    .Where(m => m.ProjectBoardId == boardId)
                    .OrderBy(m => m.SprintNumber)
                    .ToListAsync();
                var mergeBySprint = allMerges.ToDictionary(m => m.SprintNumber);
                var dueSprints = new List<strAppersBackend.Models.ProjectBoardSprintMerge>();
                foreach (var m in allMerges)
                {
                    if (m.MergedAt != null)
                        continue;
                    if (m.SprintNumber <= 1)
                        continue; // Sprint 1 is never due by this rule (and has MergedAt set for VisibleSprints)
                    if (!mergeBySprint.TryGetValue(m.SprintNumber - 1, out var prev) || prev.DueDate == null || prev.DueDate.Value > nowUtc)
                        continue;
                    dueSprints.Add(m);
                }

                if (dueSprints.Count == 0)
                    continue;

                _logger.LogInformation("[STUDENT-TEAM-BUILDER] BoardId={BoardId} ProjectId={ProjectId}: {Count} overdue sprint(s) to merge.", boardId, projectId, dueSprints.Count);

                foreach (var mergeRow in dueSprints)
                {
                    var (success, error, _) = await _sprintMergeService.ExecuteMergeSprintAsync(projectId, boardId, mergeRow.SprintNumber, merge: true);
                    if (success)
                    {
                        mergedCount++;
                        _logger.LogInformation("[STUDENT-TEAM-BUILDER] Merged BoardId={BoardId}, SprintNumber={SprintNumber}. MergedAt updated by service.", boardId, mergeRow.SprintNumber);
                    }
                    else
                    {
                        errorCount++;
                        var msg = $"BoardId={boardId}, SprintNumber={mergeRow.SprintNumber}: {error}";
                        errors.Add(msg);
                        _logger.LogWarning("[STUDENT-TEAM-BUILDER] {Message}", msg);
                    }
                }
            }

            _logger.LogInformation("[STUDENT-TEAM-BUILDER] RunDueSprintMerges completed. Merged: {Merged}, Errors: {Errors}.", mergedCount, errorCount);
            return (mergedCount, errorCount, errors);
        }
    }
}
