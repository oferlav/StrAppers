using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;

namespace strAppersBackend.Controllers;

/// <summary>
/// Catalog of student-facing AI chat personas, and the label lookups the UI uses to name that chat.
///
/// The persona is chosen per institute (Institutes.MainAIPersonaId): its Prompt drives
/// <see cref="CustomerController"/>'s system prompt, and its Name replaces every "Customer" label in
/// the board room and the staff Project Designs screen.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PersonasController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PersonasController> _logger;

    /// <summary>
    /// Label shown when no persona applies — an institute that selected none, a B2C student with no
    /// institute, or a lookup that failed. Matches the wording the UI hard-coded before personas
    /// existed, so those users see no change.
    /// </summary>
    public const string DefaultPersonaName = "Customer";

    public PersonasController(ApplicationDbContext context, ILogger<PersonasController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// All personas, for the institute settings combo.
    /// GET /api/Personas/use/list
    /// </summary>
    [HttpGet("use/list")]
    public async Task<ActionResult<object>> GetPersonas()
    {
        try
        {
            var personas = await _context.Personas.AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            return Ok(new { Success = true, Personas = personas, Count = personas.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing personas");
            return StatusCode(500, new { Success = false, Message = "An error occurred while listing personas" });
        }
    }

    /// <summary>
    /// The persona name an institute's screens should label the AI chat with.
    /// Always 200 with a usable name — a missing institute or unset persona yields
    /// <see cref="DefaultPersonaName"/>, because a label lookup must never break the page that
    /// renders around it.
    /// GET /api/Personas/use/label/by-institute/{instituteId}
    /// </summary>
    [HttpGet("use/label/by-institute/{instituteId:int}")]
    public async Task<ActionResult<object>> GetPersonaLabelByInstitute(int instituteId)
    {
        try
        {
            var name = instituteId > 0
                ? await _context.Institutes.AsNoTracking()
                    .Where(i => i.Id == instituteId && i.MainAIPersonaId != null)
                    .Select(i => i.MainAIPersona!.Name)
                    .FirstOrDefaultAsync()
                : null;

            return Ok(new { Success = true, Name = ResolvePersonaLabel(name) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving persona label for institute {InstituteId}", instituteId);
            return Ok(new { Success = true, Name = DefaultPersonaName });
        }
    }

    /// <summary>Blank or missing persona names fall back to <see cref="DefaultPersonaName"/>.</summary>
    internal static string ResolvePersonaLabel(string? personaName) =>
        string.IsNullOrWhiteSpace(personaName) ? DefaultPersonaName : personaName.Trim();
}
