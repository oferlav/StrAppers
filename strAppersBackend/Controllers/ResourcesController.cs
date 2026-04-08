using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]/use")]
public class ResourcesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ResourcesController> _logger;

    public ResourcesController(ApplicationDbContext context, ILogger<ResourcesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Add a resource to a board. Requires BoardId, StudentId, Name, Url; isFigma defaults to false. Optional sprintNumber.</summary>
    [HttpPost("add")]
    public async Task<ActionResult<ResourceResponse>> Add([FromBody] AddResourceRequest request)
    {
        if (request == null)
            return BadRequest(new { Message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(request.BoardId))
            return BadRequest(new { Message = "BoardId is required." });
        if (request.StudentId <= 0)
            return BadRequest(new { Message = "StudentId is required and must be positive." });
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { Message = "Name is required." });
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { Message = "Url is required." });

        var name = request.Name.Trim().Length > 100 ? request.Name.Trim().Substring(0, 100) : request.Name.Trim();
        var url = request.Url.Trim().Length > 1000 ? request.Url.Trim().Substring(0, 1000) : request.Url.Trim();

        var boardExists = await _context.ProjectBoards.AnyAsync(pb => pb.Id == request.BoardId.Trim(), HttpContext.RequestAborted);
        if (!boardExists)
            return NotFound(new { Message = "Board not found." });
        var studentExists = await _context.Students.AnyAsync(s => s.Id == request.StudentId, HttpContext.RequestAborted);
        if (!studentExists)
            return NotFound(new { Message = "Student not found." });

        var resource = new Resource
        {
            BoardId = request.BoardId.Trim(),
            StudentId = request.StudentId,
            Name = name,
            Url = url,
            IsFigma = request.IsFigma,
            SprintNumber = request.SprintNumber
        };
        _context.Resources.Add(resource);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);
        _logger.LogInformation("Resource added: Id={Id}, BoardId={BoardId}, StudentId={StudentId}, IsFigma={IsFigma}", resource.Id, resource.BoardId, resource.StudentId, resource.IsFigma);
        return Ok(ToResponse(resource));
    }

    /// <summary>Modify an existing resource. Optional name, url, sprintNumber; set clearSprintNumber to drop sprintNumber.</summary>
    [HttpPost("modify")]
    public async Task<ActionResult<ResourceResponse>> Modify([FromBody] ModifyResourceRequest request)
    {
        if (request == null)
            return BadRequest(new { Message = "Request body is required." });
        if (request.Id <= 0)
            return BadRequest(new { Message = "Id is required and must be positive." });

        var resource = await _context.Resources.FindAsync(new object[] { request.Id }, HttpContext.RequestAborted);
        if (resource == null)
            return NotFound(new { Message = "Resource not found." });

        if (!string.IsNullOrWhiteSpace(request.Name))
            resource.Name = request.Name.Trim().Length > 100 ? request.Name.Trim().Substring(0, 100) : request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.Url))
            resource.Url = request.Url.Trim().Length > 1000 ? request.Url.Trim().Substring(0, 1000) : request.Url.Trim();
        if (request.ClearSprintNumber)
            resource.SprintNumber = null;
        else if (request.SprintNumber.HasValue)
            resource.SprintNumber = request.SprintNumber;

        await _context.SaveChangesAsync(HttpContext.RequestAborted);
        _logger.LogInformation("Resource modified: Id={Id}", resource.Id);
        return Ok(ToResponse(resource));
    }

    /// <summary>Delete a resource by Id.</summary>
    [HttpPost("delete")]
    public async Task<ActionResult> Delete([FromBody] DeleteResourceRequest request)
    {
        if (request == null)
            return BadRequest(new { Message = "Request body is required." });
        if (request.Id <= 0)
            return BadRequest(new { Message = "Id is required and must be positive." });

        var resource = await _context.Resources.FindAsync(new object[] { request.Id }, HttpContext.RequestAborted);
        if (resource == null)
            return NotFound(new { Message = "Resource not found." });

        _context.Resources.Remove(resource);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);
        _logger.LogInformation("Resource deleted: Id={Id}", request.Id);
        return Ok(new { Success = true, Message = "Resource deleted successfully." });
    }

    /// <summary>Get Figma resources for a board (isFigma = true). Optional sprintNumber filters to that sprint.</summary>
    [HttpGet("figma")]
    public async Task<ActionResult<List<ResourceResponse>>> GetFigma([FromQuery] string boardId, [FromQuery] int? sprintNumber)
    {
        if (string.IsNullOrWhiteSpace(boardId))
            return BadRequest(new { Message = "boardId is required." });
        var q = _context.Resources
            .AsNoTracking()
            .Where(r => r.BoardId == boardId.Trim() && r.IsFigma);
        if (sprintNumber.HasValue)
            q = q.Where(r => r.SprintNumber == sprintNumber.Value);
        var list = await q
            .OrderBy(r => r.Id)
            .Select(r => new ResourceResponse { Id = r.Id, BoardId = r.BoardId, StudentId = r.StudentId, Name = r.Name, Url = r.Url, IsFigma = r.IsFigma, SprintNumber = r.SprintNumber })
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(list);
    }

    /// <summary>Get all resources for a board where isFigma = false. Optional sprintNumber filters to that sprint.</summary>
    [HttpGet("resources-all")]
    public async Task<ActionResult<List<ResourceResponse>>> GetResourcesAll([FromQuery] string boardId, [FromQuery] int? sprintNumber)
    {
        if (string.IsNullOrWhiteSpace(boardId))
            return BadRequest(new { Message = "boardId is required." });
        var q = _context.Resources
            .AsNoTracking()
            .Where(r => r.BoardId == boardId.Trim() && !r.IsFigma);
        if (sprintNumber.HasValue)
            q = q.Where(r => r.SprintNumber == sprintNumber.Value);
        var list = await q
            .OrderBy(r => r.Id)
            .Select(r => new ResourceResponse { Id = r.Id, BoardId = r.BoardId, StudentId = r.StudentId, Name = r.Name, Url = r.Url, IsFigma = r.IsFigma, SprintNumber = r.SprintNumber })
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(list);
    }

    /// <summary>Get resources for a board where isFigma = false and StudentId = studentId. Optional sprintNumber filters to that sprint.</summary>
    [HttpGet("resources-by-student")]
    public async Task<ActionResult<List<ResourceResponse>>> GetResourcesByStudent([FromQuery] string boardId, [FromQuery] int studentId, [FromQuery] int? sprintNumber)
    {
        if (string.IsNullOrWhiteSpace(boardId))
            return BadRequest(new { Message = "boardId is required." });
        if (studentId <= 0)
            return BadRequest(new { Message = "studentId is required and must be positive." });
        var q = _context.Resources
            .AsNoTracking()
            .Where(r => r.BoardId == boardId.Trim() && !r.IsFigma && r.StudentId == studentId);
        if (sprintNumber.HasValue)
            q = q.Where(r => r.SprintNumber == sprintNumber.Value);
        var list = await q
            .OrderBy(r => r.Id)
            .Select(r => new ResourceResponse { Id = r.Id, BoardId = r.BoardId, StudentId = r.StudentId, Name = r.Name, Url = r.Url, IsFigma = r.IsFigma, SprintNumber = r.SprintNumber })
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(list);
    }

    private static ResourceResponse ToResponse(Resource r)
    {
        return new ResourceResponse { Id = r.Id, BoardId = r.BoardId, StudentId = r.StudentId, Name = r.Name, Url = r.Url, IsFigma = r.IsFigma, SprintNumber = r.SprintNumber };
    }
}

public class AddResourceRequest
{
    public string BoardId { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsFigma { get; set; } = false;
    /// <summary>Optional sprint number (e.g. 1 for Sprint 1).</summary>
    [JsonPropertyName("sprintNumber")]
    public int? SprintNumber { get; set; }
}

public class ModifyResourceRequest
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Url { get; set; }
    [JsonPropertyName("sprintNumber")]
    public int? SprintNumber { get; set; }
    /// <summary>When true, clears <see cref="Resource.SprintNumber"/> (takes precedence over <see cref="SprintNumber"/>).</summary>
    [JsonPropertyName("clearSprintNumber")]
    public bool ClearSprintNumber { get; set; }
}

public class DeleteResourceRequest
{
    public int Id { get; set; }
}

public class ResourceResponse
{
    public int Id { get; set; }
    public string BoardId { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsFigma { get; set; }
    [JsonPropertyName("sprintNumber")]
    public int? SprintNumber { get; set; }
}
