using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmployersController> _logger;
        private readonly IPasswordHasherService _passwordHasher;

        public EmployersController(ApplicationDbContext context, ILogger<EmployersController> logger, IPasswordHasherService passwordHasher)
        {
            _context = context;
            _logger = logger;
            _passwordHasher = passwordHasher;
        }

        /// <summary>
        /// Create a new employer (for frontend use)
        /// </summary>
        [HttpPost("use/create")]
        public async Task<ActionResult<Employer>> CreateEmployer([FromBody] CreateEmployerRequest request)
        {
            try
            {
                _logger.LogInformation("Starting CreateEmployer method with request: {Request}",
                    System.Text.Json.JsonSerializer.Serialize(request));

                // Validate the request
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState is invalid. Validation errors:");
                    foreach (var error in ModelState)
                    {
                        _logger.LogWarning("Field: {Field}, Errors: {Errors}",
                            error.Key,
                            string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
                    }
                    return BadRequest(ModelState);
                }

                // Check if employer with same contact email already exists
                var existingEmployer = await _context.Employers
                    .FirstOrDefaultAsync(e => e.ContactEmail.ToLower() == request.ContactEmail.ToLower());

                if (existingEmployer != null)
                {
                    _logger.LogWarning("Employer with contact email {ContactEmail} already exists", request.ContactEmail);
                    return Conflict($"An employer with contact email '{request.ContactEmail}' already exists");
                }

                // Check if email exists in Students table
                var existingStudent = await _context.Students
                    .FirstOrDefaultAsync(s => s.Email.ToLower() == request.ContactEmail.ToLower());

                if (existingStudent != null)
                {
                    _logger.LogWarning("Contact email {ContactEmail} is already used by a student (StudentId: {StudentId})", 
                        request.ContactEmail, existingStudent.Id);
                    return Conflict($"The contact email '{request.ContactEmail}' is already used by a student");
                }

                // Check if email exists in Organizations table
                var existingOrganization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.ContactEmail != null && o.ContactEmail.ToLower() == request.ContactEmail.ToLower());

                if (existingOrganization != null)
                {
                    _logger.LogWarning("Contact email {ContactEmail} is already used by an organization (OrganizationId: {OrganizationId})", 
                        request.ContactEmail, existingOrganization.Id);
                    return Conflict($"The contact email '{request.ContactEmail}' is already used by an organization");
                }

                // Validate that SubscriptionTypeId exists
                var subscriptionExists = await _context.Subscriptions
                    .AnyAsync(s => s.Id == request.SubscriptionTypeId);

                if (!subscriptionExists)
                {
                    _logger.LogWarning("SubscriptionTypeId {SubscriptionTypeId} does not exist", request.SubscriptionTypeId);
                    return BadRequest($"Subscription type with ID {request.SubscriptionTypeId} does not exist");
                }

                // Normalize website URL - add http:// if missing
                string? normalizedWebsite = null;
                if (!string.IsNullOrWhiteSpace(request.Website))
                {
                    var website = request.Website.Trim();
                    if (!website.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                        !website.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        normalizedWebsite = "http://" + website;
                        _logger.LogInformation("Normalized website URL: {Original} -> {Normalized}", request.Website, normalizedWebsite);
                    }
                    else
                    {
                        normalizedWebsite = website;
                    }
                }

                // Hash password if provided
                string? passwordHash = null;
                if (!string.IsNullOrWhiteSpace(request.Password))
                {
                    _logger.LogInformation("Hashing password for employer with email {Email}", request.ContactEmail);
                    passwordHash = _passwordHasher.HashPassword(request.Password);
                }

                // Create new employer
                var employer = new Employer
                {
                    Name = request.Name,
                    Logo = request.Logo, // Base64 string or URL
                    Website = normalizedWebsite,
                    ContactEmail = request.ContactEmail,
                    Phone = request.Phone,
                    Address = request.Address,
                    Description = request.Description,
                    SubscriptionTypeId = request.SubscriptionTypeId,
                    PasswordHash = passwordHash,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Employers.Add(employer);
                await _context.SaveChangesAsync();

                // Load the employer with related data for response
                var createdEmployer = await _context.Employers
                    .Include(e => e.SubscriptionType)
                    .FirstOrDefaultAsync(e => e.Id == employer.Id);

                _logger.LogInformation("Employer created successfully with ID {EmployerId} and name {Name}",
                    employer.Id, employer.Name);

                return Ok(createdEmployer);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while creating employer: {Message}", ex.Message);
                return StatusCode(500, $"An error occurred while saving the employer to the database: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating employer: {Message}", ex.Message);
                return StatusCode(500, $"An unexpected error occurred while creating the employer: {ex.Message}");
            }
        }

        /// <summary>
        /// Edit/Update an existing employer (for frontend use)
        /// </summary>
        [HttpPost("use/edit")]
        public async Task<ActionResult<Employer>> EditEmployer([FromBody] EditEmployerRequest request)
        {
            try
            {
                _logger.LogInformation("Starting EditEmployer method for employer ID {EmployerId} with request: {Request}",
                    request.Id, System.Text.Json.JsonSerializer.Serialize(request));

                // Validate the request
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState is invalid. Validation errors:");
                    foreach (var error in ModelState)
                    {
                        _logger.LogWarning("Field: {Field}, Errors: {Errors}",
                            error.Key,
                            string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
                    }
                    return BadRequest(ModelState);
                }

                // Find the employer
                var employer = await _context.Employers.FindAsync(request.Id);
                if (employer == null)
                {
                    _logger.LogWarning("Employer with ID {EmployerId} not found", request.Id);
                    return NotFound($"Employer with ID {request.Id} not found");
                }

                // Check if contact email is being changed and if it conflicts with existing
                if (employer.ContactEmail.ToLower() != request.ContactEmail.ToLower())
                {
                    var existingEmployer = await _context.Employers
                        .FirstOrDefaultAsync(e => e.ContactEmail.ToLower() == request.ContactEmail.ToLower() && e.Id != request.Id);

                    if (existingEmployer != null)
                    {
                        _logger.LogWarning("Employer with contact email {ContactEmail} already exists", request.ContactEmail);
                        return Conflict($"An employer with contact email '{request.ContactEmail}' already exists");
                    }

                    // Check if email exists in Students table
                    var existingStudent = await _context.Students
                        .FirstOrDefaultAsync(s => s.Email.ToLower() == request.ContactEmail.ToLower());

                    if (existingStudent != null)
                    {
                        _logger.LogWarning("Contact email {ContactEmail} is already used by a student (StudentId: {StudentId})", 
                            request.ContactEmail, existingStudent.Id);
                        return Conflict($"The contact email '{request.ContactEmail}' is already used by a student");
                    }

                    // Check if email exists in Organizations table
                    var existingOrganization = await _context.Organizations
                        .FirstOrDefaultAsync(o => o.ContactEmail != null && o.ContactEmail.ToLower() == request.ContactEmail.ToLower());

                    if (existingOrganization != null)
                    {
                        _logger.LogWarning("Contact email {ContactEmail} is already used by an organization (OrganizationId: {OrganizationId})", 
                            request.ContactEmail, existingOrganization.Id);
                        return Conflict($"The contact email '{request.ContactEmail}' is already used by an organization");
                    }
                }

                // Validate that SubscriptionTypeId exists
                var subscriptionExists = await _context.Subscriptions
                    .AnyAsync(s => s.Id == request.SubscriptionTypeId);

                if (!subscriptionExists)
                {
                    _logger.LogWarning("SubscriptionTypeId {SubscriptionTypeId} does not exist", request.SubscriptionTypeId);
                    return BadRequest($"Subscription type with ID {request.SubscriptionTypeId} does not exist");
                }

                // Normalize website URL - add http:// if missing
                string? normalizedWebsite = null;
                if (!string.IsNullOrWhiteSpace(request.Website))
                {
                    var website = request.Website.Trim();
                    if (!website.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                        !website.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        normalizedWebsite = "http://" + website;
                        _logger.LogInformation("Normalized website URL: {Original} -> {Normalized}", request.Website, normalizedWebsite);
                    }
                    else
                    {
                        normalizedWebsite = website;
                    }
                }

                // Update employer properties
                employer.Name = request.Name;
                employer.Logo = request.Logo; // Base64 string or URL
                employer.Website = normalizedWebsite;
                employer.ContactEmail = request.ContactEmail;
                employer.Phone = request.Phone;
                employer.Address = request.Address;
                employer.Description = request.Description;
                employer.SubscriptionTypeId = request.SubscriptionTypeId;
                employer.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Load the employer with related data for response
                var updatedEmployer = await _context.Employers
                    .Include(e => e.SubscriptionType)
                    .FirstOrDefaultAsync(e => e.Id == employer.Id);

                _logger.LogInformation("Employer with ID {EmployerId} updated successfully", request.Id);

                return Ok(updatedEmployer);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while updating employer with ID {EmployerId}: {Message}", request.Id, ex.Message);
                return StatusCode(500, $"An error occurred while updating the employer in the database: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating employer with ID {EmployerId}: {Message}", request.Id, ex.Message);
                return StatusCode(500, $"An unexpected error occurred while updating the employer: {ex.Message}");
            }
        }

        /// <summary>
        /// Get an employer by contact email
        /// </summary>
        [HttpGet("use/{email}")]
        public async Task<ActionResult<Employer>> GetEmployerByEmail(string email)
        {
            try
            {
                _logger.LogInformation("Getting employer by email: {Email}", email);

                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogWarning("Email parameter is empty or null");
                    return BadRequest(new { Success = false, Message = "Email parameter is required" });
                }

                // Find employer by contact email (case-insensitive)
                var employer = await _context.Employers
                    .Include(e => e.SubscriptionType)
                    .FirstOrDefaultAsync(e => e.ContactEmail.ToLower() == email.ToLower());

                if (employer == null)
                {
                    _logger.LogWarning("Employer with contact email {Email} not found", email);
                    return NotFound(new { Success = false, Message = $"Employer with contact email '{email}' not found" });
                }

                _logger.LogInformation("Employer found: ID={EmployerId}, Name={Name}, Email={Email}", 
                    employer.Id, employer.Name, employer.ContactEmail);

                return Ok(employer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employer by email {Email}: {Message}", email, ex.Message);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"An error occurred while retrieving the employer: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Login endpoint for employers - verifies contact email and password
        /// </summary>
        [HttpPost("use/login")]
        public async Task<ActionResult<object>> LoginEmployer(EmployerLoginRequest request)
        {
            try
            {
                _logger.LogInformation("Login attempt for employer with email {Email}", request.Email);

                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    _logger.LogWarning("Login attempt with missing email or password");
                    return BadRequest(new { Success = false, Message = "Email and password are required" });
                }

                var employer = await _context.Employers
                    .Include(e => e.SubscriptionType)
                    .FirstOrDefaultAsync(e => e.ContactEmail.ToLower() == request.Email.ToLower());

                if (employer == null)
                {
                    _logger.LogWarning("Login attempt failed: Employer with email {Email} not found", request.Email);
                    return Unauthorized(new { Success = false, Message = "Invalid email or password" });
                }

                if (string.IsNullOrWhiteSpace(employer.PasswordHash))
                {
                    _logger.LogWarning("Login attempt failed: Employer with email {Email} has no password set", request.Email);
                    return Unauthorized(new { Success = false, Message = "Password not set for this account" });
                }

                bool isValidPassword = _passwordHasher.VerifyPassword(employer.PasswordHash, request.Password);

                if (!isValidPassword)
                {
                    _logger.LogWarning("Login attempt failed: Invalid password for employer with email {Email}", request.Email);
                    return Unauthorized(new { Success = false, Message = "Invalid email or password" });
                }

                _logger.LogInformation("Login successful for employer with email {Email}", request.Email);

                return Ok(new
                {
                    Success = true,
                    Message = "Login successful",
                    Employer = new
                    {
                        Id = employer.Id,
                        Name = employer.Name,
                        ContactEmail = employer.ContactEmail,
                        Website = employer.Website,
                        SubscriptionType = employer.SubscriptionType != null ? new
                        {
                            Id = employer.SubscriptionType.Id,
                            Description = employer.SubscriptionType.Description,
                            Price = employer.SubscriptionType.Price
                        } : null
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during employer login for email {Email}", request.Email);
                return StatusCode(500, new { Success = false, Message = "An error occurred during login" });
            }
        }

        /// <summary>
        /// Change password endpoint for employers
        /// </summary>
        [HttpPost("use/change-password")]
        public async Task<ActionResult<object>> ChangeEmployerPassword(EmployerChangePasswordRequest request)
        {
            try
            {
                _logger.LogInformation("Password change request for employer with email {Email}", request.Email);

                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    _logger.LogWarning("Password change attempt with missing email or new password");
                    return BadRequest(new { Success = false, Message = "Email and new password are required" });
                }

                var employer = await _context.Employers
                    .FirstOrDefaultAsync(e => e.ContactEmail.ToLower() == request.Email.ToLower());

                if (employer == null)
                {
                    _logger.LogWarning("Password change failed: Employer with email {Email} not found", request.Email);
                    return NotFound(new { Success = false, Message = "Employer not found" });
                }

                // Hash the new password
                string passwordHash = _passwordHasher.HashPassword(request.NewPassword);
                employer.PasswordHash = passwordHash;
                employer.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Password changed successfully for employer with email {Email}", request.Email);

                return Ok(new
                {
                    Success = true,
                    Message = "Password changed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for employer with email {Email}", request.Email);
                return StatusCode(500, new { Success = false, Message = "An error occurred while changing password" });
            }
        }

        /// <summary>
        /// Observe a board - increments ProjectBoards.Observed count and sets EmployerBoards.Observed to true
        /// Only increments ProjectBoards.Observed if EmployerBoards.Observed is not already true
        /// </summary>
        [HttpPost("use/observe")]
        public async Task<ActionResult<object>> ObserveBoard([FromBody] ObserveBoardRequest request)
        {
            try
            {
                _logger.LogInformation("Observe board request received: Email={Email}, BoardId={BoardId}", 
                    request.Email, request.BoardId);

                // Validate request
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    _logger.LogWarning("Email is required");
                    return BadRequest(new { Success = false, Message = "Email is required" });
                }

                // Find employer by email
                var employer = await _context.Employers
                    .FirstOrDefaultAsync(e => e.ContactEmail.ToLower() == request.Email.ToLower());

                if (employer == null)
                {
                    _logger.LogWarning("Employer with email {Email} not found", request.Email);
                    return NotFound(new { Success = false, Message = $"Employer with email '{request.Email}' not found" });
                }

                // Find the ProjectBoard
                var projectBoard = await _context.ProjectBoards.FindAsync(request.BoardId);
                if (projectBoard == null)
                {
                    _logger.LogWarning("ProjectBoard not found for BoardId={BoardId}", request.BoardId);
                    return NotFound(new { Success = false, Message = "ProjectBoard not found" });
                }

                // Find or create the EmployerBoard record
                var employerBoard = await _context.EmployerBoards
                    .FirstOrDefaultAsync(eb => 
                        eb.EmployerId == employer.Id && 
                        eb.BoardId == request.BoardId);

                bool isNewRecord = false;
                bool shouldIncrementProjectBoard = false;
                
                if (employerBoard == null)
                {
                    // Create new EmployerBoard record
                    _logger.LogInformation("Creating new EmployerBoard record for EmployerId={EmployerId} (email: {Email}), BoardId={BoardId}", 
                        employer.Id, request.Email, request.BoardId);
                    
                    employerBoard = new EmployerBoard
                    {
                        EmployerId = employer.Id,
                        BoardId = request.BoardId,
                        Observed = true,
                        Approved = false,
                        MeetRequest = null, // Leave MeetRequest empty
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.EmployerBoards.Add(employerBoard);
                    isNewRecord = true;
                    // For new records, always increment ProjectBoards.Observed
                    shouldIncrementProjectBoard = true;
                }
                else
                {
                    // Check if EmployerBoards.Observed is already true (only for existing records)
                    shouldIncrementProjectBoard = !employerBoard.Observed;
                }

                if (shouldIncrementProjectBoard)
                {
                    // Increment ProjectBoards.Observed
                    projectBoard.Observed += 1;
                    _logger.LogInformation("Incrementing ProjectBoards.Observed for BoardId={BoardId} from {OldValue} to {NewValue}", 
                        request.BoardId, projectBoard.Observed - 1, projectBoard.Observed);
                }
                else
                {
                    _logger.LogInformation("EmployerBoards.Observed is already true for EmployerId={EmployerId} (email: {Email}), BoardId={BoardId} - skipping ProjectBoards.Observed increment", 
                        employer.Id, request.Email, request.BoardId);
                }

                // Set EmployerBoards.Observed to true
                employerBoard.Observed = true;
                employerBoard.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Observe board completed: EmployerId={EmployerId} (email: {Email}), BoardId={BoardId}, ProjectBoards.Observed={ObservedCount}, EmployerBoards.Observed={EmployerObserved}, Incremented={Incremented}, IsNewRecord={IsNewRecord}", 
                    employer.Id, request.Email, request.BoardId, projectBoard.Observed, employerBoard.Observed, shouldIncrementProjectBoard, isNewRecord);

                return Ok(new
                {
                    Success = true,
                    Message = isNewRecord
                        ? "EmployerBoard record created and board observed successfully - ProjectBoards.Observed incremented"
                        : (shouldIncrementProjectBoard 
                            ? "Board observed successfully - ProjectBoards.Observed incremented" 
                            : "Board observed successfully - ProjectBoards.Observed not incremented (already observed by this employer)"),
                    IsNewRecord = isNewRecord,
                    EmployerId = employer.Id,
                    Email = request.Email,
                    BoardId = request.BoardId,
                    ProjectBoardsObserved = projectBoard.Observed,
                    EmployerBoardsObserved = employerBoard.Observed,
                    Incremented = shouldIncrementProjectBoard,
                    MeetRequest = employerBoard.MeetRequest
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error observing board: Email={Email}, BoardId={BoardId}, Message={Message}", 
                    request.Email, request.BoardId, ex.Message);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"An error occurred while observing the board: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Invite an employer to a board - creates an EmployerBoard record
        /// </summary>
        [HttpPost("use/invite")]
        public async Task<ActionResult<object>> InviteEmployerToBoard([FromBody] InviteEmployerRequest request)
        {
            try
            {
                _logger.LogInformation("Inviting employer with email {Email} to board {BoardId} with meeting time {MeetingTime}", 
                    request.Email, request.BoardId, request.MeetingTime);

                // Validate the request
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState is invalid for invite request");
                    return BadRequest(ModelState);
                }

                // Validate that employer exists by email
                var employer = await _context.Employers
                    .FirstOrDefaultAsync(e => e.ContactEmail.ToLower() == request.Email.ToLower());
                if (employer == null)
                {
                    _logger.LogWarning("Employer with email {Email} not found", request.Email);
                    return NotFound(new { Success = false, Message = $"Employer with email '{request.Email}' not found" });
                }

                // Validate that board exists
                var board = await _context.ProjectBoards.FindAsync(request.BoardId);
                if (board == null)
                {
                    _logger.LogWarning("Board with ID {BoardId} not found", request.BoardId);
                    return NotFound(new { Success = false, Message = $"Board with ID {request.BoardId} not found" });
                }

                // Check if EmployerBoard record already exists
                var existingInvite = await _context.EmployerBoards
                    .FirstOrDefaultAsync(eb => 
                        eb.EmployerId == employer.Id && 
                        eb.BoardId == request.BoardId);

                EmployerBoard employerBoard;
                bool isUpdate = false;

                if (existingInvite != null)
                {
                    // Update existing record
                    _logger.LogInformation("EmployerBoard record already exists for EmployerId={EmployerId}, BoardId={BoardId} - updating with new meeting time", 
                        employer.Id, request.BoardId);
                    
                    existingInvite.MeetRequest = request.MeetingTime;
                    existingInvite.UpdatedAt = DateTime.UtcNow;
                    employerBoard = existingInvite;
                    isUpdate = true;
                }
                else
                {
                    // Create new EmployerBoard record
                    employerBoard = new EmployerBoard
                    {
                        EmployerId = employer.Id,
                        BoardId = request.BoardId,
                        MeetRequest = request.MeetingTime,
                        Observed = false,
                        Approved = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.EmployerBoards.Add(employerBoard);
                }

                await _context.SaveChangesAsync();

                if (isUpdate)
                {
                    _logger.LogInformation("Employer {EmployerId} (email: {Email}) invite updated for board {BoardId} with new meeting time {MeetingTime}", 
                        employer.Id, request.Email, request.BoardId, request.MeetingTime);
                }
                else
                {
                    _logger.LogInformation("Employer {EmployerId} (email: {Email}) successfully invited to board {BoardId} with meeting time {MeetingTime}", 
                        employer.Id, request.Email, request.BoardId, request.MeetingTime);
                }

                return Ok(new
                {
                    Success = true,
                    Message = isUpdate ? "Employer invite updated successfully" : "Employer invited successfully",
                    IsUpdate = isUpdate,
                    EmployerBoardId = employerBoard.Id,
                    EmployerId = employerBoard.EmployerId,
                    BoardId = employerBoard.BoardId,
                    MeetRequest = employerBoard.MeetRequest,
                    Observed = employerBoard.Observed,
                    Approved = employerBoard.Approved,
                    CreatedAt = employerBoard.CreatedAt,
                    UpdatedAt = employerBoard.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inviting employer with email {Email} to board {BoardId}: {Message}", 
                    request.Email, request.BoardId, ex.Message);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"An error occurred while inviting the employer: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Check if an employer is invited to a board and the meeting request is still valid (Today < MeetRequest)
        /// </summary>
        [HttpGet("use/is-invite")]
        public async Task<ActionResult<object>> IsInvite([FromQuery] string email, [FromQuery] string boardId)
        {
            try
            {
                _logger.LogInformation("Checking if employer with email {Email} is invited to board {BoardId}", email, boardId);

                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogWarning("Email is required");
                    return BadRequest(new { Success = false, Message = "Email is required" });
                }

                if (string.IsNullOrWhiteSpace(boardId))
                {
                    _logger.LogWarning("BoardId is required");
                    return BadRequest(new { Success = false, Message = "BoardId is required" });
                }

                // Find employer by email
                var employer = await _context.Employers
                    .FirstOrDefaultAsync(e => e.ContactEmail.ToLower() == email.ToLower());

                if (employer == null)
                {
                    _logger.LogInformation("Employer with email {Email} not found", email);
                    return Ok(new
                    {
                        Success = true,
                        IsInvite = false,
                        Message = "Employer not found"
                    });
                }

                // Find EmployerBoard record
                var employerBoard = await _context.EmployerBoards
                    .FirstOrDefaultAsync(eb => 
                        eb.EmployerId == employer.Id && 
                        eb.BoardId == boardId);

                if (employerBoard == null)
                {
                    _logger.LogInformation("No EmployerBoard record found for EmployerId={EmployerId} (email: {Email}), BoardId={BoardId}", 
                        employer.Id, email, boardId);
                    return Ok(new
                    {
                        Success = true,
                        IsInvite = false,
                        Message = "No invite found"
                    });
                }

                // Check if MeetRequest exists and if Today < MeetRequest
                var today = DateTime.UtcNow.Date;
                var isInviteValid = employerBoard.MeetRequest.HasValue && 
                                    today < employerBoard.MeetRequest.Value.Date;

                _logger.LogInformation("Invite check result for EmployerId={EmployerId} (email: {Email}), BoardId={BoardId}: IsInvite={IsInvite}, MeetRequest={MeetRequest}, Today={Today}", 
                    employer.Id, email, boardId, isInviteValid, employerBoard.MeetRequest, today);

                return Ok(new
                {
                    Success = true,
                    IsInvite = isInviteValid,
                    MeetRequest = employerBoard.MeetRequest,
                    Today = today,
                    Message = isInviteValid ? "Valid invite found" : "Invite not valid or expired"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking invite for employer with email {Email} and board {BoardId}: {Message}", 
                    email, boardId, ex.Message);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"An error occurred while checking the invite: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get all candidates for a specific employer by email
        /// </summary>
        [HttpGet("use/candidates/{employerEmail}")]
        public async Task<ActionResult<object>> GetEmployerCandidates(string employerEmail)
        {
            try
            {
                _logger.LogInformation("Getting candidates for employer with email {Email}", employerEmail);

                if (string.IsNullOrWhiteSpace(employerEmail))
                {
                    _logger.LogWarning("Employer email is required");
                    return BadRequest(new { Success = false, Message = "Employer email is required" });
                }

                // Find employer by email
                var employer = await _context.Employers
                    .FirstOrDefaultAsync(e => e.ContactEmail.ToLower() == employerEmail.ToLower());

                if (employer == null)
                {
                    _logger.LogWarning("Employer with email {Email} not found", employerEmail);
                    return NotFound(new { Success = false, Message = $"Employer with email '{employerEmail}' not found" });
                }

                // Get all candidates for this employer
                var candidates = await _context.EmployerCandidates
                    .Include(ec => ec.Student)
                        .ThenInclude(s => s.Major)
                    .Include(ec => ec.Student)
                        .ThenInclude(s => s.Year)
                    .Where(ec => ec.EmployerId == employer.Id)
                    .OrderByDescending(ec => ec.CreatedAt)
                    .Select(ec => new
                    {
                        Id = ec.Id,
                        StudentId = ec.StudentId,
                        StudentEmail = ec.Student.Email,
                        StudentFirstName = ec.Student.FirstName,
                        StudentLastName = ec.Student.LastName,
                        StudentMajor = ec.Student.Major != null ? ec.Student.Major.Name : null,
                        StudentYear = ec.Student.Year != null ? ec.Student.Year.Name : null,
                        CreatedAt = ec.CreatedAt
                    })
                    .ToListAsync();

                _logger.LogInformation("Found {Count} candidates for employer {EmployerId} (email: {Email})", 
                    candidates.Count, employer.Id, employerEmail);

                return Ok(new
                {
                    Success = true,
                    EmployerId = employer.Id,
                    EmployerEmail = employerEmail,
                    CandidatesCount = candidates.Count,
                    Candidates = candidates
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting candidates for employer with email {Email}: {Message}", 
                    employerEmail, ex.Message);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"An error occurred while retrieving candidates: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Check if a student is a candidate for a specific employer
        /// </summary>
        [HttpGet("use/is-a-candidate/{employerEmail}/{studentEmail}")]
        public async Task<ActionResult<object>> IsEmployerCandidate(string employerEmail, string studentEmail)
        {
            try
            {
                _logger.LogInformation("Checking if student {StudentEmail} is a candidate for employer {EmployerEmail}", 
                    studentEmail, employerEmail);

                if (string.IsNullOrWhiteSpace(employerEmail))
                {
                    _logger.LogWarning("Employer email is required");
                    return BadRequest(new { Success = false, Message = "Employer email is required" });
                }

                if (string.IsNullOrWhiteSpace(studentEmail))
                {
                    _logger.LogWarning("Student email is required");
                    return BadRequest(new { Success = false, Message = "Student email is required" });
                }

                // Find employer by email
                var employer = await _context.Employers
                    .FirstOrDefaultAsync(e => e.ContactEmail.ToLower() == employerEmail.ToLower());

                if (employer == null)
                {
                    _logger.LogWarning("Employer with email {Email} not found", employerEmail);
                    return NotFound(new { Success = false, Message = $"Employer with email '{employerEmail}' not found" });
                }

                // Find student by email
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Email.ToLower() == studentEmail.ToLower());

                if (student == null)
                {
                    _logger.LogWarning("Student with email {Email} not found", studentEmail);
                    return NotFound(new { Success = false, Message = $"Student with email '{studentEmail}' not found" });
                }

                // Check if candidate record exists
                var employerCandidate = await _context.EmployerCandidates
                    .FirstOrDefaultAsync(ec => 
                        ec.EmployerId == employer.Id && 
                        ec.StudentId == student.Id);

                bool isCandidate = employerCandidate != null;

                _logger.LogInformation("Candidate check result: EmployerId={EmployerId}, StudentId={StudentId}, IsCandidate={IsCandidate}", 
                    employer.Id, student.Id, isCandidate);

                return Ok(new
                {
                    Success = true,
                    IsCandidate = isCandidate,
                    EmployerId = employer.Id,
                    EmployerEmail = employerEmail,
                    StudentId = student.Id,
                    StudentEmail = studentEmail,
                    EmployerCandidateId = employerCandidate?.Id,
                    CreatedAt = employerCandidate?.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking candidate for employer {EmployerEmail} and student {StudentEmail}: {Message}", 
                    employerEmail, studentEmail, ex.Message);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"An error occurred while checking the candidate: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Add a student candidate to an employer's candidate list
        /// </summary>
        [HttpPost("use/set-candidate/{employerEmail}/{studentEmail}")]
        public async Task<ActionResult<object>> SetEmployerCandidate(string employerEmail, string studentEmail)
        {
            try
            {
                _logger.LogInformation("Setting candidate: EmployerEmail={EmployerEmail}, StudentEmail={StudentEmail}", 
                    employerEmail, studentEmail);

                if (string.IsNullOrWhiteSpace(employerEmail))
                {
                    _logger.LogWarning("Employer email is required");
                    return BadRequest(new { Success = false, Message = "Employer email is required" });
                }

                if (string.IsNullOrWhiteSpace(studentEmail))
                {
                    _logger.LogWarning("Student email is required");
                    return BadRequest(new { Success = false, Message = "Student email is required" });
                }

                // Find employer by email
                var employer = await _context.Employers
                    .FirstOrDefaultAsync(e => e.ContactEmail.ToLower() == employerEmail.ToLower());

                if (employer == null)
                {
                    _logger.LogWarning("Employer with email {Email} not found", employerEmail);
                    return NotFound(new { Success = false, Message = $"Employer with email '{employerEmail}' not found" });
                }

                // Find student by email
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Email.ToLower() == studentEmail.ToLower());

                if (student == null)
                {
                    _logger.LogWarning("Student with email {Email} not found", studentEmail);
                    return NotFound(new { Success = false, Message = $"Student with email '{studentEmail}' not found" });
                }

                // Check if candidate record already exists
                var existingCandidate = await _context.EmployerCandidates
                    .FirstOrDefaultAsync(ec => 
                        ec.EmployerId == employer.Id && 
                        ec.StudentId == student.Id);

                if (existingCandidate != null)
                {
                    _logger.LogInformation("Candidate already exists for EmployerId={EmployerId}, StudentId={StudentId}", 
                        employer.Id, student.Id);
                    return Ok(new
                    {
                        Success = true,
                        Message = "Candidate already exists",
                        IsNew = false,
                        EmployerCandidateId = existingCandidate.Id,
                        EmployerId = employer.Id,
                        EmployerEmail = employerEmail,
                        StudentId = student.Id,
                        StudentEmail = studentEmail,
                        CreatedAt = existingCandidate.CreatedAt
                    });
                }

                // Create new candidate record
                var employerCandidate = new EmployerCandidate
                {
                    EmployerId = employer.Id,
                    StudentId = student.Id,
                    CreatedAt = DateTime.UtcNow
                };

                _context.EmployerCandidates.Add(employerCandidate);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Candidate added successfully: EmployerId={EmployerId}, StudentId={StudentId}, EmployerCandidateId={Id}", 
                    employer.Id, student.Id, employerCandidate.Id);

                return Ok(new
                {
                    Success = true,
                    Message = "Candidate added successfully",
                    IsNew = true,
                    EmployerCandidateId = employerCandidate.Id,
                    EmployerId = employer.Id,
                    EmployerEmail = employerEmail,
                    StudentId = student.Id,
                    StudentEmail = studentEmail,
                    CreatedAt = employerCandidate.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting candidate for employer {EmployerEmail} and student {StudentEmail}: {Message}", 
                    employerEmail, studentEmail, ex.Message);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"An error occurred while setting the candidate: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Remove a student candidate from an employer's candidate list
        /// </summary>
        [HttpPost("use/remove-candidate/{employerEmail}/{studentEmail}")]
        public async Task<ActionResult<object>> RemoveEmployerCandidate(string employerEmail, string studentEmail)
        {
            try
            {
                _logger.LogInformation("Removing candidate: EmployerEmail={EmployerEmail}, StudentEmail={StudentEmail}", 
                    employerEmail, studentEmail);

                if (string.IsNullOrWhiteSpace(employerEmail))
                {
                    _logger.LogWarning("Employer email is required");
                    return BadRequest(new { Success = false, Message = "Employer email is required" });
                }

                if (string.IsNullOrWhiteSpace(studentEmail))
                {
                    _logger.LogWarning("Student email is required");
                    return BadRequest(new { Success = false, Message = "Student email is required" });
                }

                // Find employer by email
                var employer = await _context.Employers
                    .FirstOrDefaultAsync(e => e.ContactEmail.ToLower() == employerEmail.ToLower());

                if (employer == null)
                {
                    _logger.LogWarning("Employer with email {Email} not found", employerEmail);
                    return NotFound(new { Success = false, Message = $"Employer with email '{employerEmail}' not found" });
                }

                // Find student by email
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Email.ToLower() == studentEmail.ToLower());

                if (student == null)
                {
                    _logger.LogWarning("Student with email {Email} not found", studentEmail);
                    return NotFound(new { Success = false, Message = $"Student with email '{studentEmail}' not found" });
                }

                // Find candidate record
                var employerCandidate = await _context.EmployerCandidates
                    .FirstOrDefaultAsync(ec => 
                        ec.EmployerId == employer.Id && 
                        ec.StudentId == student.Id);

                if (employerCandidate == null)
                {
                    _logger.LogInformation("Candidate not found for EmployerId={EmployerId}, StudentId={StudentId}", 
                        employer.Id, student.Id);
                    return NotFound(new { Success = false, Message = "Candidate not found" });
                }

                // Remove candidate record
                _context.EmployerCandidates.Remove(employerCandidate);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Candidate removed successfully: EmployerId={EmployerId}, StudentId={StudentId}, EmployerCandidateId={Id}", 
                    employer.Id, student.Id, employerCandidate.Id);

                return Ok(new
                {
                    Success = true,
                    Message = "Candidate removed successfully",
                    EmployerCandidateId = employerCandidate.Id,
                    EmployerId = employer.Id,
                    EmployerEmail = employerEmail,
                    StudentId = student.Id,
                    StudentEmail = studentEmail
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing candidate for employer {EmployerEmail} and student {StudentEmail}: {Message}", 
                    employerEmail, studentEmail, ex.Message);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"An error occurred while removing the candidate: {ex.Message}"
                });
            }
        }
    }
}

// Request DTO for EmployersController
public class CreateEmployerRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Logo { get; set; } // Base64 string or URL (TEXT type, no length limit)

    [MaxLength(500)]
    public string? Website { get; set; } // Can be provided with or without http:// or https:// prefix

    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string ContactEmail { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public int SubscriptionTypeId { get; set; }

    [MaxLength(100)]
    public string? Password { get; set; }  // Plain password (will be hashed before storing)
}

// Request DTO for observing a board
public class ObserveBoardRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string BoardId { get; set; } = string.Empty;
}

// Request DTO for editing employer
public class EditEmployerRequest
{
    [Required]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Logo { get; set; } // Base64 string or URL (TEXT type, no length limit)

    [MaxLength(500)]
    public string? Website { get; set; } // Can be provided with or without http:// or https:// prefix

    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string ContactEmail { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public int SubscriptionTypeId { get; set; }
}

// Request DTO for employer login
public class EmployerLoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}

// Request DTO for changing employer password
public class EmployerChangePasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string NewPassword { get; set; } = string.Empty;
}

// Request DTO for inviting employer to board
public class InviteEmployerRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string BoardId { get; set; } = string.Empty;

    [Required]
    public DateTime MeetingTime { get; set; }
}

