using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuestBoardsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<QuestBoardsController> _logger;

    public QuestBoardsController(ApplicationDbContext context, ILogger<QuestBoardsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Returns the QuestBoard record for the given student on the given board.
    /// The boardroom uses this to get per-student GitHub/Railway/Pages URLs.
    /// </summary>
    [HttpGet("{boardId}/my")]
    public async Task<ActionResult<object>> GetMyQuestBoard(string boardId, [FromQuery] int studentId)
    {
        if (string.IsNullOrWhiteSpace(boardId))
            return BadRequest(new { success = false, message = "boardId is required." });
        if (studentId <= 0)
            return BadRequest(new { success = false, message = "studentId is required." });

        var qb = await _context.QuestBoards
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.BoardId == boardId && q.StudentId == studentId);

        if (qb == null)
            return NotFound(new { success = false, message = $"No QuestBoard found for student {studentId} on board {boardId}." });

        return Ok(new
        {
            success = true,
            boardId = qb.BoardId,
            studentId = qb.StudentId,
            githubFrontendUrl = qb.GithubFrontendUrl,
            githubBackendUrl = qb.GithubBackendUrl,
            webApiUrl = qb.WebApiUrl,
            publishUrl = qb.PublishUrl,
            neonProjectId = qb.NeonProjectId,
            neonBranchId = qb.NeonBranchId,
            createdAt = qb.CreatedAt,
            updatedAt = qb.UpdatedAt
        });
    }

    /// <summary>
    /// Returns all QuestBoards for a given board (admin/teacher view).
    /// </summary>
    [HttpGet("{boardId}/all")]
    public async Task<ActionResult<object>> GetAllQuestBoards(string boardId)
    {
        if (string.IsNullOrWhiteSpace(boardId))
            return BadRequest(new { success = false, message = "boardId is required." });

        var questBoards = await _context.QuestBoards
            .AsNoTracking()
            .Where(q => q.BoardId == boardId)
            .Include(q => q.Student)
            .OrderBy(q => q.StudentId)
            .ToListAsync();

        return Ok(new
        {
            success = true,
            boardId,
            count = questBoards.Count,
            questBoards = questBoards.Select(qb => new
            {
                studentId = qb.StudentId,
                studentName = $"{qb.Student?.FirstName} {qb.Student?.LastName}",
                studentEmail = qb.Student?.Email,
                githubFrontendUrl = qb.GithubFrontendUrl,
                githubBackendUrl = qb.GithubBackendUrl,
                webApiUrl = qb.WebApiUrl,
                publishUrl = qb.PublishUrl,
                neonProjectId = qb.NeonProjectId,
                createdAt = qb.CreatedAt
            })
        });
    }
}
