using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

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
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(
            ApplicationDbContext context,
            IChatCompletionService chatCompletionService,
            IOptions<PromptConfig> promptConfig,
            ILogger<CustomerController> logger)
        {
            _context = context;
            _chatCompletionService = chatCompletionService;
            _promptConfig = promptConfig.Value;
            _logger = logger;
        }

        /// <summary>
        /// Get the last X chat history messages for a student/sprint (X = ChatHistoryLength from appSettings). For frontend refresh of chat.
        /// Filters by StudentId and SprintNumber (CustomerChatHistory.StudentId stores the actual student Id).
        /// </summary>
        [HttpGet("use/chat-history")]
        public async Task<ActionResult<object>> GetCustomerChatHistory([FromQuery] int studentId, [FromQuery] int sprintNumber)
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
                var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StudentId);
                if (student == null)
                    return NotFound(new { Success = false, Message = $"Student {request.StudentId} not found." });
                if (student.ProjectId != projectId)
                    return BadRequest(new { Success = false, Message = $"Student {request.StudentId} is not assigned to the board's project." });

                _logger.LogInformation("Customer respond: BoardId={BoardId}, ProjectId={ProjectId}, StudentId={StudentId}, SprintNumber={SprintNumber}, Model={Model}", request.BoardId, projectId, request.StudentId, request.SprintNumber, aiModelName);

                var aiModel = await _context.AIModels
                    .FirstOrDefaultAsync(m => m.Name == aiModelName && m.IsActive);
                if (aiModel == null)
                {
                    _logger.LogWarning("AI model '{ModelName}' not found or not active", aiModelName);
                    return NotFound(new { Success = false, Message = $"AI model '{aiModelName}' not found or not active" });
                }

                var userQuestion = request.UserQuestion ?? "";
                var sprintNumber = request.SprintNumber;

                // General statement from Projects.Description
                var project = await _context.Projects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == projectId);
                var projectDescription = project?.Description ?? "(No project description.)";

                // Context from ProjectModules: ProjectId = @ProjectId and Sequence = (SprintNumber - 1)
                var sequenceForSprint = sprintNumber - 1;
                var modules = await _context.ProjectModules
                    .Where(m => m.ProjectId == projectId && m.Sequence == sequenceForSprint)
                    .OrderBy(m => m.Id)
                    .ToListAsync();

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

                var responseWithTokens = $"{aiResponse}\n\n---\n[Token Usage: Input={inputTokens}, Output={outputTokens}, Total={inputTokens + outputTokens}]";

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
                    Response = responseWithTokens
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
