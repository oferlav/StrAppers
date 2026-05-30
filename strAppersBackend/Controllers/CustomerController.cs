using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using strAppersBackend.Utilities;
using Microsoft.Extensions.Configuration;

namespace strAppersBackend.Controllers
{
    /// <summary>
    /// Customer chatbot: context from ProjectModules (ProjectId, Sequence = SprintNumber-1), chat history from CustomerChatHistory, system prompt from config.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IChatCompletionService _chatCompletionService;
        private readonly PromptConfig _promptConfig;
        private readonly TestingConfig _testingConfig;
        private readonly ILogger<CustomerController> _logger;
        private readonly IConfiguration _configuration;
        private readonly ISmtpEmailService _smtpEmailService;

        private bool DebugAiContext => _configuration.GetValue<bool>("Debug:AiContext", false);

        public CustomerController(
            ApplicationDbContext context,
            IChatCompletionService chatCompletionService,
            IOptions<PromptConfig> promptConfig,
            IOptions<TestingConfig> testingConfig,
            ILogger<CustomerController> logger,
            IConfiguration configuration,
            ISmtpEmailService smtpEmailService)
        {
            _context = context;
            _chatCompletionService = chatCompletionService;
            _promptConfig = promptConfig.Value;
            _testingConfig = testingConfig.Value;
            _logger = logger;
            _configuration = configuration;
            _smtpEmailService = smtpEmailService;
        }

        /// <summary>
        /// Get the last X chat history messages for a student/sprint (X = ChatHistoryLength from appSettings). For frontend refresh of chat.
        /// Filters by StudentId and SprintNumber (CustomerChatHistory.StudentId stores the actual student Id).
        /// </summary>
        [HttpGet("use/chat-history")]
        public async Task<ActionResult<object>> GetCustomerChatHistory(
            [FromQuery] int studentId,
            [FromQuery] int sprintNumber)
        {
            try
            {
                var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
                if (student == null)
                    return NotFound(new { Success = false, Message = $"Student {studentId} not found." });
                var limit = _promptConfig.Customer.ChatHistoryLength * 2; // last N pairs (user + assistant)
                var messages = await _context.CustomerChatHistory
                    .Where(h => h.StudentId == studentId && h.SprintId == sprintNumber)
                    .OrderByDescending(h => h.CreatedAt)
                    .Take(limit)
                    .OrderBy(h => h.CreatedAt)
                    .Select(h => new { h.Role, h.Message, h.CreatedAt, h.AIModelName })
                    .ToListAsync();
                return Ok(new { Success = true, Messages = messages });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer chat history for StudentId: {StudentId}, SprintNumber: {SprintNumber}", studentId, sprintNumber);
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        /// <summary>
        /// Get customer response using a specific AI model. Context comes from ProjectModules (ProjectId from BoardId, Sequence = SprintNumber-1); chat history from CustomerChatHistory limited by ChatHistoryLength.
        /// </summary>
        [HttpPost("use/respond/{aiModelName}")]
        public async Task<ActionResult<object>> Respond(
            string aiModelName,
            [FromBody] CustomerRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.BoardId))
                    return BadRequest(new { Success = false, Message = "BoardId is required." });
                if (request.StudentId <= 0)
                    return BadRequest(new { Success = false, Message = "StudentId is required and must be greater than 0." });
                var projectBoard = await _context.ProjectBoards.AsNoTracking().FirstOrDefaultAsync(pb => pb.Id == request.BoardId);
                if (projectBoard == null)
                    return NotFound(new { Success = false, Message = $"Board '{request.BoardId}' not found." });
                var projectId = projectBoard.ProjectId;
                var boardInstituteProjectId = projectBoard.InstituteProjectId;
                var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StudentId);
                if (student == null)
                    return NotFound(new { Success = false, Message = $"Student {request.StudentId} not found." });
                if (student.ProjectId != projectId)
                    return BadRequest(new { Success = false, Message = $"Student {request.StudentId} is not assigned to the board's project." });

                _logger.LogInformation("Customer respond: BoardId={BoardId}, ProjectId={ProjectId}, InstituteProjectId={IpId}, StudentId={StudentId}, SprintNumber={SprintNumber}, Model={Model}", request.BoardId, projectId, boardInstituteProjectId?.ToString() ?? "null", request.StudentId, request.SprintNumber, aiModelName);

                // Resolve model name: "default" (or empty) → Customer:AiModel config → first active DB model
                var resolvedModelName = aiModelName;
                if (string.IsNullOrWhiteSpace(resolvedModelName) || resolvedModelName.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedModelName = _configuration["Customer:AiModel"] ?? string.Empty;
                    _logger.LogInformation("Customer model resolved from config: {ModelName}", resolvedModelName);
                }

                var aiModel = string.IsNullOrWhiteSpace(resolvedModelName)
                    ? await _context.AIModels.FirstOrDefaultAsync(m => m.IsActive)
                    : await _context.AIModels.FirstOrDefaultAsync(m => m.Name == resolvedModelName && m.IsActive);

                if (aiModel == null)
                {
                    _logger.LogWarning("AI model '{ModelName}' not found or not active", resolvedModelName);
                    return NotFound(new { Success = false, Message = $"AI model '{resolvedModelName}' not found or not active" });
                }

                var userQuestion = request.UserQuestion ?? "";
                var sprintNumber = request.SprintNumber;

                // Pattern A: Description + CustomerPastStory — from InstituteProjects when boardInstituteProjectId is set, else Projects
                var (effectiveDescription, effectiveCustomerPastStory, projectSource) =
                    await ProjectContextHelper.GetEffectiveProjectDataAsync(_context, projectId, boardInstituteProjectId);
                var projectDescription = effectiveDescription ?? "(No project description.)";

                // Pattern B: module rows by Sequence — from InstituteProjectModules when boardInstituteProjectId is set, else ProjectModules
                var sequenceForSprint = sprintNumber - 1;
                var modules = await ProjectModuleLookup.FindManyBySequenceAsync(
                    _context, sequenceForSprint, projectId, boardInstituteProjectId);

                var contextText = modules.Count == 0
                    ? "(No module context for this project/sprint.)"
                    : string.Join("\n\n", modules.Select(m => $"Module: {m.Title ?? "Untitled"}\nDescription: {m.Description ?? ""}"));

                // PromptType: Customer — Keep (config; single block with placeholders [INSERT PROJECT DESCRIPTION HERE], etc.)
                var systemPrompt = string.IsNullOrWhiteSpace(_promptConfig.Customer.SystemPrompt)
                    ? "You are a helpful assistant. Use the context provided to answer questions. Be concise and professional."
                    : _promptConfig.Customer.SystemPrompt;
                // Inject Projects.Description into placeholder [INSERT PROJECT DESCRIPTION HERE]
                const string projectDescriptionPlaceholder = "[INSERT PROJECT DESCRIPTION HERE]";
                var promptWithDescription = systemPrompt.Contains(projectDescriptionPlaceholder, StringComparison.OrdinalIgnoreCase)
                    ? systemPrompt.Replace(projectDescriptionPlaceholder, projectDescription, StringComparison.OrdinalIgnoreCase)
                    : systemPrompt;
                // Inject ProjectModules content into placeholder [INSERT PROJECT-SPECIFIC DESIGN/LOGIC DATA HERE]
                const string designContextPlaceholder = "[INSERT PROJECT-SPECIFIC DESIGN/LOGIC DATA HERE]";
                var enhancedSystemPrompt = promptWithDescription.Contains(designContextPlaceholder, StringComparison.OrdinalIgnoreCase)
                    ? promptWithDescription.Replace(designContextPlaceholder, contextText, StringComparison.OrdinalIgnoreCase)
                    : $"{promptWithDescription}\n\n=== PROJECT OVERVIEW ===\n{projectDescription}\n\n=== PROJECT MODULE CONTEXT (Sprint {sprintNumber}, Sequence {sequenceForSprint}) ===\n{contextText}\n=== END CONTEXT ===";

                // Append Customer Past Story — from InstituteProjects when boardInstituteProjectId is set, else Projects
                var customerPastStory = string.IsNullOrWhiteSpace(effectiveCustomerPastStory)
                    ? "(None.)"
                    : effectiveCustomerPastStory.Trim();
                enhancedSystemPrompt += $"\n\n=== CUSTOMER PAST STORY ===\n{customerPastStory}\n=== END CUSTOMER PAST STORY ===";

                if (DebugAiContext)
                {
                    var firstModule = modules.Count > 0 ? modules[0] : null;
                    var moduleSource = firstModule == null ? null
                        : boardInstituteProjectId.HasValue
                            ? $"InstituteProjectModules (InstituteProjectId={boardInstituteProjectId.Value})"
                            : $"ProjectModules (ProjectId={projectId})";
                    await AiContextDebugLogger.LogAndEmailAsync(
                        _smtpEmailService,
                        $"POST /api/Customer/use/respond/{aiModelName}",
                        request.BoardId, request.StudentId, sprintNumber,
                        boardInstituteProjectId, projectSource,
                        effectiveDescription, effectiveCustomerPastStory,
                        moduleSource, firstModule?.Id, firstModule?.Title);
                }

                // Chat history from CustomerChatHistory (StudentId = student Id, SprintId = SprintNumber), limited by ChatHistoryLength
                var chatHistoryLength = _promptConfig.Customer.ChatHistoryLength;
                var rawChatHistory = await _context.CustomerChatHistory
                    .Where(h => h.StudentId == request.StudentId && h.SprintId == sprintNumber)
                    .OrderByDescending(h => h.CreatedAt)
                    .Take(chatHistoryLength * 2)
                    .OrderBy(h => h.CreatedAt)
                    .Select(h => new ChatMessageEntry { Role = h.Role, Message = h.Message })
                    .ToListAsync();

                // Save user message (StudentId = actual student Id)
                var userMessage = new CustomerChatHistory
                {
                    StudentId = request.StudentId,
                    SprintId = sprintNumber,
                    Role = "user",
                    Message = userQuestion,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CustomerChatHistory.Add(userMessage);
                await _context.SaveChangesAsync();

                string aiResponse;
                int inputTokens = 0;
                int outputTokens = 0;
                try
                {
                    var result = await _chatCompletionService.GetChatCompletionAsync(aiModel, enhancedSystemPrompt, userQuestion, rawChatHistory);
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

                var assistantMessage = new CustomerChatHistory
                {
                    StudentId = request.StudentId,
                    SprintId = sprintNumber,
                    Role = "assistant",
                    Message = aiResponse,
                    AIModelName = aiModel.Name,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CustomerChatHistory.Add(assistantMessage);
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
                _logger.LogError(ex, "Customer respond error: BoardId={BoardId}, SprintNumber={SprintNumber}, Model={Model}", request?.BoardId, request?.SprintNumber, aiModelName);
                return StatusCode(500, new { Success = false, Message = "An error occurred while processing your request." });
            }
        }
    }

    /// <summary>
    /// Request for customer respond endpoint. BoardId is the Trello board (ProjectBoards.Id); ProjectId is resolved from it. StudentId is the student chatting with the AI Customer.
    /// </summary>
    public class CustomerRequest
    {
        public string BoardId { get; set; } = string.Empty;
        public int SprintNumber { get; set; }
        /// <summary>Student Id (who is chatting with the AI Customer). Stored in CustomerChatHistory.StudentId.</summary>
        public int StudentId { get; set; }
        public string UserQuestion { get; set; } = string.Empty;
    }
}
