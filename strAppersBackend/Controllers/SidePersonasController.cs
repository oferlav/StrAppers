using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

/// <summary>
/// Side personas of a board's institute project (InstituteProjectPersonas), for example an "IT Manager"
/// next to the main "Consumer". Mirrors the customer chat (CustomerController) with its own history table
/// (PersonaChatHistory), so the main-persona chat and every metric that reads CustomerChatHistory stay untouched.
/// Side personas are supporting roles and are not assessed.
/// </summary>
[ApiController]
[Route("api/Personas/use/side")]
public class SidePersonasController : ControllerBase
{
    private const string ProjectDescriptionPlaceholder = "[INSERT PROJECT DESCRIPTION HERE]";

    private readonly ApplicationDbContext _context;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly PromptConfig _promptConfig;
    private readonly TestingConfig _testingConfig;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SidePersonasController> _logger;

    public SidePersonasController(
        ApplicationDbContext context,
        IChatCompletionService chatCompletionService,
        IOptions<PromptConfig> promptConfig,
        IOptions<TestingConfig> testingConfig,
        IConfiguration configuration,
        ILogger<SidePersonasController> logger)
    {
        _context = context;
        _chatCompletionService = chatCompletionService;
        _promptConfig = promptConfig.Value;
        _testingConfig = testingConfig.Value;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/Personas/use/side/by-board/{boardId}: the side personas to show as extra chat tabs, in order.
    /// Empty for boards without an institute project or without side personas, so the sidebar renders as before.
    /// </summary>
    [HttpGet("by-board/{boardId}")]
    public async Task<ActionResult<object>> GetByBoard(string boardId)
    {
        try
        {
            var instituteProjectId = await _context.ProjectBoards.AsNoTracking()
                .Where(b => b.Id == boardId)
                .Select(b => b.InstituteProjectId)
                .FirstOrDefaultAsync();
            if (instituteProjectId == null)
                return Ok(new { Success = true, Personas = Array.Empty<object>() });

            var personas = await _context.InstituteProjectPersonas.AsNoTracking()
                .Where(p => p.InstituteProjectId == instituteProjectId.Value)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                .Select(p => new { p.PersonaId, p.Persona.Name, p.SortOrder })
                .ToListAsync();
            return Ok(new { Success = true, Personas = personas });
        }
        catch (Exception ex)
        {
            // Never break the board room over optional tabs.
            _logger.LogError(ex, "Side personas lookup failed for board {BoardId}", boardId);
            return Ok(new { Success = true, Personas = Array.Empty<object>() });
        }
    }

    /// <summary>GET /api/Personas/use/side/chat-history: last ChatHistoryLength pairs with one side persona, per sprint.</summary>
    [HttpGet("chat-history")]
    public async Task<ActionResult<object>> GetChatHistory([FromQuery] int studentId, [FromQuery] int sprintNumber, [FromQuery] int personaId)
    {
        try
        {
            var limit = _promptConfig.Customer.ChatHistoryLength * 2;
            var messages = await _context.PersonaChatHistory.AsNoTracking()
                .Where(h => h.StudentId == studentId && h.SprintId == sprintNumber && h.PersonaId == personaId)
                .OrderByDescending(h => h.CreatedAt)
                .Take(limit)
                .OrderBy(h => h.CreatedAt)
                .Select(h => new { h.Role, h.Message, h.CreatedAt, h.AIModelName })
                .ToListAsync();
            return Ok(new { Success = true, Messages = messages });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Side persona chat history error: StudentId={StudentId}, Sprint={Sprint}, PersonaId={PersonaId}", studentId, sprintNumber, personaId);
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    /// <summary>POST /api/Personas/use/side/respond/{aiModelName}: one chat turn with a side persona of the board's project.</summary>
    [HttpPost("respond/{aiModelName}")]
    public async Task<ActionResult<object>> Respond(string aiModelName, [FromBody] SidePersonaRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.BoardId))
                return BadRequest(new { Success = false, Message = "BoardId is required." });
            if (request.StudentId <= 0)
                return BadRequest(new { Success = false, Message = "StudentId is required and must be greater than 0." });

            var board = await _context.ProjectBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BoardId);
            if (board == null)
                return NotFound(new { Success = false, Message = $"Board '{request.BoardId}' not found." });
            var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StudentId);
            if (student == null)
                return NotFound(new { Success = false, Message = $"Student {request.StudentId} not found." });
            if (student.ProjectId != board.ProjectId)
                return BadRequest(new { Success = false, Message = $"Student {request.StudentId} is not assigned to the board's project." });

            var link = board.InstituteProjectId == null ? null : await _context.InstituteProjectPersonas.AsNoTracking()
                .Include(p => p.Persona)
                .FirstOrDefaultAsync(p => p.InstituteProjectId == board.InstituteProjectId && p.PersonaId == request.PersonaId);
            if (link == null)
                return NotFound(new { Success = false, Message = $"Persona {request.PersonaId} is not part of this board's project." });

            // Same model resolution as the customer chat: "default" -> Customer:AiModel -> first active model.
            var resolvedModelName = aiModelName;
            if (string.IsNullOrWhiteSpace(resolvedModelName) || resolvedModelName.Equals("default", StringComparison.OrdinalIgnoreCase))
                resolvedModelName = _configuration["Customer:AiModel"] ?? string.Empty;
            var aiModel = string.IsNullOrWhiteSpace(resolvedModelName)
                ? await _context.AIModels.FirstOrDefaultAsync(m => m.IsActive)
                : await _context.AIModels.FirstOrDefaultAsync(m => m.Name == resolvedModelName && m.IsActive);
            if (aiModel == null)
                return NotFound(new { Success = false, Message = $"AI model '{resolvedModelName}' not found or not active" });

            var (description, _, _) = await ProjectContextHelper.GetEffectiveProjectDataAsync(_context, board.ProjectId, board.InstituteProjectId);
            var systemPrompt = BuildSystemPrompt(link.Persona.Name, link.Persona.Prompt, description, link.ContextText);

            var sprintNumber = request.SprintNumber;
            var history = await _context.PersonaChatHistory
                .Where(h => h.StudentId == request.StudentId && h.SprintId == sprintNumber && h.PersonaId == request.PersonaId)
                .OrderByDescending(h => h.CreatedAt)
                .Take(_promptConfig.Customer.ChatHistoryLength * 2)
                .OrderBy(h => h.CreatedAt)
                .Select(h => new ChatMessageEntry { Role = h.Role, Message = h.Message })
                .ToListAsync();

            var userQuestion = request.UserQuestion ?? "";
            _context.PersonaChatHistory.Add(new PersonaChatHistory
            {
                StudentId = request.StudentId, SprintId = sprintNumber, PersonaId = request.PersonaId,
                Role = "user", Message = userQuestion, CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            string aiResponse;
            int inputTokens, outputTokens;
            try
            {
                var result = await _chatCompletionService.GetChatCompletionAsync(aiModel, systemPrompt, userQuestion, history);
                aiResponse = result.Response;
                inputTokens = result.InputTokens;
                outputTokens = result.OutputTokens;
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("billing") || ex.Message.Contains("credit"))
            {
                _logger.LogWarning("AI API billing issue: {Error}", ex.Message);
                return StatusCode(402, new { Success = false, Message = "AI API credits insufficient. Please check your API account billing." });
            }

            _context.PersonaChatHistory.Add(new PersonaChatHistory
            {
                StudentId = request.StudentId, SprintId = sprintNumber, PersonaId = request.PersonaId,
                Role = "assistant", Message = aiResponse, AIModelName = aiModel.Name, CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Model = new { aiModel.Id, aiModel.Name, aiModel.Provider },
                Response = aiResponse,
                TokenUsage = _testingConfig.ShowTokenUsage
                    ? new { Input = inputTokens, Output = outputTokens, Total = inputTokens + outputTokens }
                    : null,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Side persona respond error: BoardId={BoardId}, PersonaId={PersonaId}, Model={Model}", request?.BoardId, request?.PersonaId, aiModelName);
            return StatusCode(500, new { Success = false, Message = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Persona prompt (generic behavior) + project description + this project's context for the persona.
    /// The description goes into [INSERT PROJECT DESCRIPTION HERE] when the prompt has it, as in the customer chat.
    /// </summary>
    internal static string BuildSystemPrompt(string personaName, string? personaPrompt, string? projectDescription, string? contextText)
    {
        var prompt = string.IsNullOrWhiteSpace(personaPrompt)
            ? $"You are the {personaName} on this project. Answer the team's questions concisely, in character."
            : personaPrompt.Trim();
        var description = string.IsNullOrWhiteSpace(projectDescription) ? "(No project description.)" : projectDescription.Trim();

        prompt = prompt.Contains(ProjectDescriptionPlaceholder, StringComparison.OrdinalIgnoreCase)
            ? prompt.Replace(ProjectDescriptionPlaceholder, description, StringComparison.OrdinalIgnoreCase)
            : $"{prompt}\n\n=== PROJECT OVERVIEW ===\n{description}\n=== END PROJECT OVERVIEW ===";

        var context = string.IsNullOrWhiteSpace(contextText) ? "(None.)" : contextText.Trim();
        var header = personaName.Trim().ToUpperInvariant();
        return $"{prompt}\n\n=== {header} CONTEXT ===\n{context}\n=== END {header} CONTEXT ===";
    }
}

/// <summary>Request for a side persona chat turn. PersonaId must be linked to the board's institute project.</summary>
public class SidePersonaRequest
{
    public string BoardId { get; set; } = string.Empty;
    public int SprintNumber { get; set; }
    public int StudentId { get; set; }
    public int PersonaId { get; set; }
    public string UserQuestion { get; set; } = string.Empty;
}
