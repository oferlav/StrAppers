using Microsoft.AspNetCore.Mvc;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

/// <summary>
/// Support request logging. POST to create a support entry with default priority 3.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SupportController : ControllerBase
{
    private const int DefaultPriority = 3;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SupportController> _logger;

    public SupportController(ApplicationDbContext context, ILogger<SupportController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Log a support request. Name, Email, and Description are required. Priority is set to 3 by default.
    /// POST /api/Support
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SupportResponse>> CreateSupportRequest([FromBody] CreateSupportRequest request)
    {
        if (request == null)
            return BadRequest(new { Message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { Message = "Name is required." });
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { Message = "Email is required." });

        var desc = string.IsNullOrWhiteSpace(request.Description) ? null : (request.Description.Length > 500 ? request.Description.Trim().Substring(0, 500) : request.Description.Trim());
        var support = new Support
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Description = desc,
            Priority = DefaultPriority
        };

        _context.Supports.Add(support);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Support request logged: Id={Id}, Email={Email}, Priority={Priority}", support.Id, support.Email, support.Priority);

        return Ok(new SupportResponse
        {
            Success = true,
            Id = support.Id,
            Name = support.Name,
            Email = support.Email,
            Description = support.Description,
            Priority = support.Priority,
            Message = "Support request logged successfully."
        });
    }
}

/// <summary>Request body for POST /api/Support.</summary>
public class CreateSupportRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>Response for POST /api/Support.</summary>
public class SupportResponse
{
    public bool Success { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Priority { get; set; }
    public string Message { get; set; } = string.Empty;
}
