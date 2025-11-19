using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers
{
// DISABLED - Functionality moved to BoardsController
[ApiController]
[Route("api/[controller]")]
public class TrelloController : ControllerBase
    {
        private readonly ITrelloService _trelloService;
        private readonly ApplicationDbContext _context;
        private readonly TrelloConfig _trelloConfig;
        private readonly BusinessLogicConfig _businessLogicConfig;
        private readonly ILogger<TrelloController> _logger;

        public TrelloController(ITrelloService trelloService, ApplicationDbContext context, IOptions<TrelloConfig> trelloConfig, IOptions<BusinessLogicConfig> businessLogicConfig, ILogger<TrelloController> logger)
        {
            _trelloService = trelloService;
            _context = context;
            _trelloConfig = trelloConfig.Value;
            _businessLogicConfig = businessLogicConfig.Value;
            _logger = logger;
        }

        /// <summary>
        /// Invite a user to Trello (obsolete - no longer needed with simplified workflow)
        /// </summary>
        /// <param name="request">User registration request</param>
        /// <returns>Registration response</returns>
        [HttpPost("invite-user")]
        [Obsolete("This method is obsolete. User invitations are now handled automatically during project creation.")]
        public async Task<ActionResult<TrelloUserRegistrationResponse>> InviteUser([FromBody] TrelloUserRegistrationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("Inviting user {Email} to Trello", request.Email);

                var result = await _trelloService.InviteUserToTrelloAsync(request.Email, request.FullName);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inviting user {Email} to Trello", request.Email);
                return StatusCode(500, new TrelloUserRegistrationResponse
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Check if a user is registered in Trello (obsolete - no longer needed with simplified workflow)
        /// </summary>
        /// <param name="request">User check request</param>
        /// <returns>User check response</returns>
        [HttpPost("check-user")]
        [Obsolete("This method is obsolete. User registration checks are no longer required.")]
        public async Task<ActionResult<TrelloUserCheckResponse>> CheckUser([FromBody] TrelloUserCheckRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("Checking if user {Email} is registered in Trello", request.Email);

                var result = await _trelloService.CheckUserRegistrationAsync(request.Email);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user {Email} in Trello", request.Email);
                return StatusCode(500, new TrelloUserCheckResponse
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}",
                    IsRegistered = false
                });
            }
        }

        /// <summary>
        /// Create a Trello project with sprints and assign tasks to team members
        /// </summary>
        /// <param name="request">Project creation request with boardId, team members and sprint plan</param>
        /// <returns>Project creation response with board URL and created cards</returns>
        [HttpPost("use/create-project")]
        [Obsolete("This method is disabled. Use BoardsController instead.")]
        public async Task<ActionResult<TrelloProjectCreationResponse>> CreateProject([FromBody] TrelloProjectCreationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("Creating Trello project for ProjectId {ProjectId}", request.ProjectId);

                // Get project from database to get project details
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

                if (project == null)
                {
                    return NotFound(new TrelloProjectCreationResponse
                    {
                        Success = false,
                        Message = $"Project with ID {request.ProjectId} not found"
                    });
                }

                var result = await _trelloService.CreateProjectWithSprintsAsync(request, project.Title);

                if (result.Success)
                {
                    // Insert ProjectBoard record into database
                    var currentTime = DateTime.UtcNow;
                    var projectBoard = new ProjectBoard
                    {
                        Id = result.BoardId!,  // Trello board ID
                        ProjectId = request.ProjectId,
                        StartDate = currentTime,  // Set to current time (like CreateDate)
                        EndDate = null,  // Stay blank (will be updated upon project termination)
                        DueDate = currentTime.AddDays(_businessLogicConfig.ProjectLengthInWeeks * 7),  // Use config for project length (convert weeks to days)
                        CreatedAt = currentTime,
                        UpdatedAt = currentTime,
                        StatusId = 1,  // Always set to 1 (New) for new ProjectBoards
                        AdminId = null,  // Will be set later when admin is assigned
                        BoardUrl = result.BoardUrl  // Store the Trello board URL
                    };

                    _context.ProjectBoards.Add(projectBoard);
                    await _context.SaveChangesAsync();

                    // Update students with the Trello BoardId
                    var studentEmails = request.TeamMembers.Select(m => m.Email).ToList();
                    var studentsToUpdate = await _context.Students
                        .Where(s => s.BoardId == null && 
                                   studentEmails.Contains(s.Email))
                        .ToListAsync();

                    foreach (var student in studentsToUpdate)
                    {
                        student.BoardId = result.BoardId;
                    }

                    if (studentsToUpdate.Any())
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Updated {StudentCount} students with Trello BoardId {TrelloBoardId}", 
                            studentsToUpdate.Count, result.BoardId);
                    }

                    _logger.LogInformation("Successfully created Trello project and saved ProjectBoard record for Project {ProjectId} with Trello BoardId {TrelloBoardId}", 
                        request.ProjectId, result.BoardId);

                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Trello project for ProjectId {ProjectId}", request.ProjectId);
                return StatusCode(500, new TrelloProjectCreationResponse
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get comprehensive Trello project statistics for dashboard (for frontend use)
        /// </summary>
        /// <param name="projectId">The project ID to get Trello stats for</param>
        /// <returns>Comprehensive Trello project statistics</returns>
        [HttpGet("use/project/{projectId}/stats")]
        [Obsolete("This method is disabled. Use GET /api/Boards/use/stats/{boardId} instead.")]
        public async Task<ActionResult<object>> GetTrelloProjectStats(int projectId)
        {
            try
            {
                _logger.LogInformation("Getting Trello stats for Project {ProjectId}", projectId);

                // Get ProjectBoard record from database
                var projectBoard = await _context.ProjectBoards
                    .Include(pb => pb.Project)
                    .FirstOrDefaultAsync(pb => pb.ProjectId == projectId);

                if (projectBoard == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = $"No Trello board found for Project {projectId}",
                        BoardFound = false
                    });
                }

                // Get Trello stats using the Trello board ID from database
                var stats = await _trelloService.GetProjectStatsAsync(projectBoard.Id);

                return Ok(new
                {
                    Success = true,
                    ProjectId = projectId,
                    TrelloBoardId = projectBoard.Id,
                    ProjectName = projectBoard.Project.Title,
                    Stats = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Trello stats for Project {ProjectId}", projectId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Error getting Trello stats: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// List all boards in the Trello workspace (helper method for debugging)
        /// </summary>
        /// <returns>List of all boards with their details</returns>
        [HttpGet("boards/list")]
        [Obsolete("This method is disabled. Use /use/ routing instead.")]
        public async Task<ActionResult<object>> ListAllBoards()
        {
            try
            {
                _logger.LogInformation("Listing all Trello boards");

                var result = await _trelloService.ListAllBoardsAsync();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing Trello boards");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Error listing boards: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get Trello configuration status
        /// </summary>
        /// <returns>Configuration status</returns>
        [HttpGet("config/status")]
        [Obsolete("This method is disabled. Use /use/ routing instead.")]
        public ActionResult<object> GetConfigStatus()
        {
            try
            {
                var hasApiKey = !string.IsNullOrEmpty(_trelloConfig.ApiKey);
                var hasApiSecret = !string.IsNullOrEmpty(_trelloConfig.ApiSecret);
                var hasApiToken = !string.IsNullOrEmpty(_trelloConfig.ApiToken);

                var tokenGenerationUrl = hasApiKey && hasApiSecret 
                    ? $"https://trello.com/1/authorize?expiration=never&scope=read,write,account&response_type=token&name=StrAppers&key={_trelloConfig.ApiKey}"
                    : null;

                return Ok(new
                {
                    Success = true,
                    Configuration = new
                    {
                        HasApiKey = hasApiKey,
                        HasApiSecret = hasApiSecret,
                        HasApiToken = hasApiToken,
                        IsFullyConfigured = hasApiKey && hasApiSecret && hasApiToken
                    },
                    TokenGenerationUrl = tokenGenerationUrl,
                    Message = hasApiToken 
                        ? "Trello is fully configured and ready to use"
                        : "Trello configuration incomplete. Generate API token to complete setup."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Trello configuration status");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Error getting configuration status: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get all cards and lists for a specific board filtered by label name
        /// </summary>
        /// <param name="boardId">Trello board ID</param>
        /// <param name="labelName">Label name to filter cards by</param>
        /// <returns>Lists and cards filtered by the specified label</returns>
        [HttpGet("use/board/{boardId}/label/{labelName}")]
        public async Task<ActionResult<object>> GetCardsAndListsByLabel(string boardId, string labelName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(boardId))
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Board ID is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(labelName))
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Label name is required"
                    });
                }

                // URL decode the label name in case it contains encoded characters like %2F for /
                labelName = System.Net.WebUtility.UrlDecode(labelName);
                _logger.LogInformation("Getting cards and lists for board {BoardId} filtered by label '{LabelName}' (decoded)", boardId, labelName);

                var result = await _trelloService.GetCardsAndListsByLabelAsync(boardId, labelName);

                // Check if the result indicates success
                var resultType = result.GetType();
                var successProperty = resultType.GetProperty("Success");
                if (successProperty != null)
                {
                    var success = (bool)successProperty.GetValue(result)!;
                    if (!success)
                    {
                        var messageProperty = resultType.GetProperty("Message");
                        var message = messageProperty?.GetValue(result)?.ToString() ?? "Unknown error";
                        
                        var labelFoundProperty = resultType.GetProperty("LabelFound");
                        var labelFound = labelFoundProperty != null && (bool)labelFoundProperty.GetValue(result)!;
                        
                        if (!labelFound)
                        {
                            return NotFound(result);
                        }
                        
                        return BadRequest(result);
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cards and lists by label '{LabelName}' for board {BoardId}", labelName, boardId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Error getting cards and lists by label: {ex.Message}"
                });
            }
        }
    }
}
