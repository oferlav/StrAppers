using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]/use")]
public class CRMController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CRMController> _logger;

    public CRMController(ApplicationDbContext context, ILogger<CRMController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Add a new stakeholder.</summary>
    [HttpPost("add-stakeholder")]
    public async Task<ActionResult<object>> AddStakeholder([FromBody] AddStakeholderRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { Success = false, Message = "Name is required." });
        if (request.CategoryId <= 0)
            return BadRequest(new { Success = false, Message = "CategoryId is required and must be greater than 0." });
        if (request.StatusId <= 0)
            return BadRequest(new { Success = false, Message = "StatusId is required and must be greater than 0." });

        var categoryExists = await _context.StakeholderCategories.AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists)
            return BadRequest(new { Success = false, Message = $"CategoryId {request.CategoryId} not found." });
        var statusExists = await _context.StakeholderStatuses.AnyAsync(s => s.Id == request.StatusId);
        if (!statusExists)
            return BadRequest(new { Success = false, Message = $"StatusId {request.StatusId} not found." });

        if (!string.IsNullOrWhiteSpace(request.BoardId))
        {
            var boardExists = await _context.ProjectBoards.AnyAsync(b => b.Id == request.BoardId);
            if (!boardExists)
                return BadRequest(new { Success = false, Message = $"BoardId '{request.BoardId}' not found." });
        }

        var stakeholder = new Stakeholder
        {
            Name = request.Name.Trim(),
            CategoryId = request.CategoryId,
            StatusId = request.StatusId,
            V1AlignmentScore = request.V1AlignmentScore,
            Delta = request.Delta?.Trim(),
            BoardId = string.IsNullOrWhiteSpace(request.BoardId) ? null : request.BoardId.Trim()
        };
        _context.Stakeholders.Add(stakeholder);
        await _context.SaveChangesAsync();
        _logger.LogInformation("CRM: added stakeholder Id={Id}, Name={Name}", stakeholder.Id, stakeholder.Name);
        return Ok(new { Success = true, Message = "Stakeholder added.", Id = stakeholder.Id, Stakeholder = stakeholder });
    }

    /// <summary>Update an existing stakeholder.</summary>
    [HttpPost("update-stakeholder")]
    public async Task<ActionResult<object>> UpdateStakeholder([FromBody] UpdateStakeholderRequest request)
    {
        if (request == null || request.Id <= 0)
            return BadRequest(new { Success = false, Message = "Id is required and must be greater than 0." });

        var stakeholder = await _context.Stakeholders.FirstOrDefaultAsync(s => s.Id == request.Id);
        if (stakeholder == null)
            return NotFound(new { Success = false, Message = $"Stakeholder {request.Id} not found." });

        if (request.Name != null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { Success = false, Message = "Name cannot be empty." });
            stakeholder.Name = request.Name.Trim();
        }
        if (request.CategoryId.HasValue)
        {
            if (request.CategoryId.Value <= 0)
                return BadRequest(new { Success = false, Message = "CategoryId must be greater than 0." });
            var categoryExists = await _context.StakeholderCategories.AnyAsync(c => c.Id == request.CategoryId.Value);
            if (!categoryExists)
                return BadRequest(new { Success = false, Message = $"CategoryId {request.CategoryId} not found." });
            stakeholder.CategoryId = request.CategoryId.Value;
        }
        if (request.StatusId.HasValue)
        {
            if (request.StatusId.Value <= 0)
                return BadRequest(new { Success = false, Message = "StatusId must be greater than 0." });
            var statusExists = await _context.StakeholderStatuses.AnyAsync(s => s.Id == request.StatusId.Value);
            if (!statusExists)
                return BadRequest(new { Success = false, Message = $"StatusId {request.StatusId} not found." });
            stakeholder.StatusId = request.StatusId.Value;
        }
        if (request.V1AlignmentScore.HasValue)
            stakeholder.V1AlignmentScore = request.V1AlignmentScore.Value;
        if (request.Delta != null)
            stakeholder.Delta = request.Delta.Trim();
        if (request.BoardId != null)
        {
            if (string.IsNullOrWhiteSpace(request.BoardId))
                stakeholder.BoardId = null;
            else
            {
                var boardExists = await _context.ProjectBoards.AnyAsync(b => b.Id == request.BoardId);
                if (!boardExists)
                    return BadRequest(new { Success = false, Message = $"BoardId '{request.BoardId}' not found." });
                stakeholder.BoardId = request.BoardId.Trim();
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("CRM: updated stakeholder Id={Id}", stakeholder.Id);
        return Ok(new { Success = true, Message = "Stakeholder updated.", Stakeholder = stakeholder });
    }

    /// <summary>Delete a stakeholder by Id.</summary>
    [HttpPost("delete-stakeholder")]
    public async Task<ActionResult<object>> DeleteStakeholder([FromBody] DeleteStakeholderRequest request)
    {
        if (request == null || request.Id <= 0)
            return BadRequest(new { Success = false, Message = "Id is required and must be greater than 0." });

        var stakeholder = await _context.Stakeholders.FirstOrDefaultAsync(s => s.Id == request.Id);
        if (stakeholder == null)
            return NotFound(new { Success = false, Message = $"Stakeholder {request.Id} not found." });

        _context.Stakeholders.Remove(stakeholder);
        await _context.SaveChangesAsync();
        _logger.LogInformation("CRM: deleted stakeholder Id={Id}", request.Id);
        return Ok(new { Success = true, Message = "Stakeholder deleted.", Id = request.Id });
    }

    /// <summary>Get stakeholders, optionally filtered by CategoryId, StatusId, BoardId.</summary>
    [HttpGet("stakeholder")]
    public async Task<ActionResult<object>> GetStakeholders(
        [FromQuery] int? categoryId,
        [FromQuery] int? statusId,
        [FromQuery] string? boardId)
    {
        var query = _context.Stakeholders
            .AsNoTracking()
            .Include(s => s.Category)
            .Include(s => s.Status)
            .AsQueryable();

        if (categoryId.HasValue && categoryId.Value > 0)
            query = query.Where(s => s.CategoryId == categoryId.Value);
        if (statusId.HasValue && statusId.Value > 0)
            query = query.Where(s => s.StatusId == statusId.Value);
        if (!string.IsNullOrWhiteSpace(boardId))
            query = query.Where(s => s.BoardId != null && s.BoardId == boardId.Trim());

        var list = await query
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.CategoryId,
                CategoryName = s.Category != null ? s.Category.Name : null,
                s.StatusId,
                StatusName = s.Status != null ? s.Status.Name : null,
                s.V1AlignmentScore,
                s.Delta,
                s.BoardId
            })
            .ToListAsync();

        return Ok(new { Success = true, Count = list.Count, Stakeholders = list });
    }

    /// <summary>Get all stakeholder statuses.</summary>
    [HttpGet("statuses")]
    public async Task<ActionResult<object>> GetStatuses()
    {
        var list = await _context.StakeholderStatuses
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync();
        return Ok(new { Success = true, Count = list.Count, Statuses = list });
    }

    /// <summary>Get all stakeholder categories.</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<object>> GetCategories()
    {
        var list = await _context.StakeholderCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();
        return Ok(new { Success = true, Count = list.Count, Categories = list });
    }
}

public class AddStakeholderRequest
{
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int StatusId { get; set; }
    public int V1AlignmentScore { get; set; }
    public string? Delta { get; set; }
    public string? BoardId { get; set; }
}

public class UpdateStakeholderRequest
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? CategoryId { get; set; }
    public int? StatusId { get; set; }
    public int? V1AlignmentScore { get; set; }
    public string? Delta { get; set; }
    public string? BoardId { get; set; }
}

public class DeleteStakeholderRequest
{
    public int Id { get; set; }
}
