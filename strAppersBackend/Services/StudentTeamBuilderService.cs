using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;

namespace strAppersBackend.Services
{
    /// <summary>
    /// Runs due sprint merges for boards with students in status=3: for each unique BoardId, merges sprints where DueDate has passed and MergedAt is null.
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

                // Sprints for this board where MergedAt is null and DueDate has passed (current date >= DueDate)
                var nowUtc = DateTime.UtcNow;
                var dueSprints = await _context.ProjectBoardSprintMerges
                    .Where(m => m.ProjectBoardId == boardId && m.MergedAt == null && m.DueDate != null && m.DueDate.Value <= nowUtc)
                    .OrderBy(m => m.SprintNumber)
                    .ToListAsync();

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
