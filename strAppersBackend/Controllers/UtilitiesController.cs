using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text.Json;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UtilitiesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IConfiguration _configuration;

    public UtilitiesController(ApplicationDbContext context, ILogger<UtilitiesController> logger, IPasswordHasherService passwordHasher, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    /// <summary>
    /// Utility endpoint to set the same password hash for ALL Students and Organizations.
    /// This is a one-time utility for testing purposes.
    /// </summary>
    [HttpPost("set-password-for-all")]
    public async Task<ActionResult<object>> SetPasswordForAll(SetPasswordForAllRequest request)
    {
        try
        {
            _logger.LogInformation("Setting password for all Students and Organizations");

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("Password is required but was not provided");
                return BadRequest(new { Success = false, Message = "Password is required" });
            }

            // Hash the password once
            string passwordHash = _passwordHasher.HashPassword(request.Password);
            _logger.LogInformation("Password hashed successfully");

            // Update all Students
            var students = await _context.Students.ToListAsync();
            int studentsUpdated = 0;
            foreach (var student in students)
            {
                student.PasswordHash = passwordHash;
                student.UpdatedAt = DateTime.UtcNow;
                studentsUpdated++;
            }

            // Update all Organizations
            var organizations = await _context.Organizations.ToListAsync();
            int organizationsUpdated = 0;
            foreach (var organization in organizations)
            {
                organization.PasswordHash = passwordHash;
                organization.UpdatedAt = DateTime.UtcNow;
                organizationsUpdated++;
            }

            // Save all changes
            await _context.SaveChangesAsync();

            _logger.LogInformation("Password set successfully for {StudentsCount} students and {OrganizationsCount} organizations", 
                studentsUpdated, organizationsUpdated);

            return Ok(new
            {
                Success = true,
                Message = $"Password set successfully for all records",
                StudentsUpdated = studentsUpdated,
                OrganizationsUpdated = organizationsUpdated,
                TotalUpdated = studentsUpdated + organizationsUpdated
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting password for all Students and Organizations");
            return StatusCode(500, new 
            { 
                Success = false, 
                Message = $"An error occurred while setting passwords: {ex.Message}" 
            });
        }
    }

    /// <summary>
    /// Utility endpoint to fix orphaned projects and reassign students to valid projects.
    /// This is a destructive operation that will:
    /// - Change student ProjectId assignments
    /// - Update ProjectBoards associations
    /// - Delete orphaned projects and project modules
    /// 
    /// WARNING: This operation cannot be undone. Ensure you have a database backup before running.
    /// </summary>
    [HttpPost("change-projects")]
    public async Task<ActionResult<object>> ChangeProjects()
    {
        // Valid OrganizationId -> ProjectId mappings
        var validProjectMappings = new Dictionary<int, int>
        {
            { 12, 19 },
            { 3, 3 },
            { 14, 63 },
            { 23, 67 },
            { 25, 81 },
            { 29, 79 },
            { 31, 80 }
        };

        var validProjectIds = validProjectMappings.Values.ToHashSet();
        var validOrganizationIds = validProjectMappings.Keys.ToHashSet();

        IDbContextTransaction? transaction = null;
        try
        {
            _logger.LogWarning("Starting change-projects utility - DESTRUCTIVE OPERATION");
            
            // Start transaction
            transaction = await _context.Database.BeginTransactionAsync();
            _logger.LogInformation("Transaction started");

            // Get all valid organization IDs from database
            var existingOrgIds = await _context.Organizations
                .Where(o => validOrganizationIds.Contains(o.Id))
                .Select(o => o.Id)
                .ToListAsync();
            
            _logger.LogInformation("Found {Count} valid organizations in database", existingOrgIds.Count);

            // Get all valid project IDs from database
            var existingProjectIds = await _context.Projects
                .Where(p => validProjectIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();
            
            _logger.LogInformation("Found {Count} valid projects in database", existingProjectIds.Count);

            // Get available project IDs from the valid list (that exist in DB)
            var availableProjectIds = validProjectIds.Where(pid => existingProjectIds.Contains(pid)).ToList();
            
            if (availableProjectIds.Count == 0)
            {
                _logger.LogError("No valid projects found in database. Cannot proceed.");
                await transaction.RollbackAsync();
                return BadRequest(new 
                { 
                    Success = false, 
                    Message = "No valid projects found in database. Cannot proceed." 
                });
            }

            _logger.LogInformation("Available project IDs for assignment: {ProjectIds}", string.Join(", ", availableProjectIds));

            var random = new Random();
            var stats = new ChangeProjectsStats();

            // Step 1: Identify students with ProjectId pointing to projects with invalid OrganizationId
            var allStudents = await _context.Students
                .Include(s => s.Project)
                .ToListAsync();

            // Helper function to check if a project is invalid
            bool IsProjectInvalid(Project? project)
            {
                if (project == null) return true;
                if (!project.OrganizationId.HasValue) return true;
                return !validOrganizationIds.Contains(project.OrganizationId.Value);
            }

            var studentsWithInvalidProjects = allStudents
                .Where(s => s.ProjectId.HasValue && IsProjectInvalid(s.Project))
                .ToList();

            _logger.LogInformation("Found {Count} students with invalid project assignments", studentsWithInvalidProjects.Count);

            // Step 2: Identify boards that have at least one student with invalid ProjectId
            var boardsWithInvalidStudents = studentsWithInvalidProjects
                .Where(s => !string.IsNullOrWhiteSpace(s.BoardId))
                .Select(s => s.BoardId!)
                .Distinct()
                .ToList();

            _logger.LogInformation("Found {Count} unique boards with at least one invalid student", boardsWithInvalidStudents.Count);

            // Step 2a: Process students WITH BoardId - assign same ProjectId to ALL students in same board
            // This ensures data integrity: all students in same board have same ProjectId
            foreach (var boardId in boardsWithInvalidStudents)
            {
                // Get ALL students in this board (not just those with invalid projects)
                var allStudentsInBoard = allStudents
                    .Where(s => s.BoardId == boardId)
                    .ToList();
                
                // Randomly select a ProjectId for this board
                var selectedProjectId = availableProjectIds[random.Next(availableProjectIds.Count)];
                
                _logger.LogInformation("Assigning ProjectId {ProjectId} to board {BoardId} ({StudentCount} total students in board)", 
                    selectedProjectId, boardId, allStudentsInBoard.Count);

                // Update ALL students in this board to have the same ProjectId
                foreach (var student in allStudentsInBoard)
                {
                    var wasUpdated = student.ProjectId != selectedProjectId;
                    student.ProjectId = selectedProjectId;
                    student.UpdatedAt = DateTime.UtcNow;
                    
                    if (wasUpdated)
                    {
                        stats.StudentsUpdatedWithBoard++;
                    }
                }

                // Update ProjectBoards table to match
                var projectBoard = await _context.ProjectBoards.FindAsync(boardId);
                if (projectBoard != null)
                {
                    projectBoard.ProjectId = selectedProjectId;
                    projectBoard.UpdatedAt = DateTime.UtcNow;
                    stats.ProjectBoardsUpdated++;
                    _logger.LogInformation("Updated ProjectBoard {BoardId} to ProjectId {ProjectId}", boardId, selectedProjectId);
                }
                else
                {
                    _logger.LogWarning("ProjectBoard {BoardId} not found in database", boardId);
                }
            }

            // Step 2b: Process students WITHOUT BoardId but WITH invalid ProjectId
            // Exclude students that were already processed in Step 2a (those with BoardId)
            var studentsWithoutBoard = studentsWithInvalidProjects
                .Where(s => string.IsNullOrWhiteSpace(s.BoardId))
                .ToList();

            _logger.LogInformation("Found {Count} students without BoardId but with invalid ProjectId", studentsWithoutBoard.Count);

            foreach (var student in studentsWithoutBoard)
            {
                // Randomly select a ProjectId
                var selectedProjectId = availableProjectIds[random.Next(availableProjectIds.Count)];
                
                student.ProjectId = selectedProjectId;
                student.UpdatedAt = DateTime.UtcNow;
                stats.StudentsUpdatedWithoutBoard++;
                
                _logger.LogInformation("Assigned ProjectId {ProjectId} to student {StudentId} (no board)", 
                    selectedProjectId, student.Id);
            }

            // Step 3: Identify all orphaned projects (projects with OrganizationId not in valid list OR null)
            var orphanedProjects = await _context.Projects
                .Where(p => p.OrganizationId == null || !validOrganizationIds.Contains(p.OrganizationId.Value))
                .Select(p => p.Id)
                .ToListAsync();

            _logger.LogInformation("Found {Count} orphaned projects to delete", orphanedProjects.Count);
            stats.OrphanedProjectsFound = orphanedProjects.Count;

            // Step 4: Handle JoinRequests that reference orphaned projects
            // JoinRequests.ProjectId is NOT NULL, so we need to delete them before deleting projects
            var orphanedJoinRequests = await _context.JoinRequests
                .Where(jr => orphanedProjects.Contains(jr.ProjectId))
                .ToListAsync();

            if (orphanedJoinRequests.Any())
            {
                _context.JoinRequests.RemoveRange(orphanedJoinRequests);
                stats.JoinRequestsDeleted = orphanedJoinRequests.Count;
                _logger.LogInformation("Deleted {Count} join requests referencing orphaned projects", orphanedJoinRequests.Count);
            }

            // Step 5: Delete orphaned project modules first (due to foreign key constraints)
            var orphanedModules = await _context.ProjectModules
                .Where(pm => pm.ProjectId.HasValue && orphanedProjects.Contains(pm.ProjectId.Value))
                .ToListAsync();

            if (orphanedModules.Any())
            {
                _context.ProjectModules.RemoveRange(orphanedModules);
                stats.ProjectModulesDeleted = orphanedModules.Count;
                _logger.LogInformation("Deleted {Count} orphaned project modules", orphanedModules.Count);
            }

            // Step 6: Delete orphaned projects (cascade will handle other related data)
            var projectsToDelete = await _context.Projects
                .Where(p => orphanedProjects.Contains(p.Id))
                .ToListAsync();

            if (projectsToDelete.Any())
            {
                _context.Projects.RemoveRange(projectsToDelete);
                stats.ProjectsDeleted = projectsToDelete.Count;
                _logger.LogInformation("Deleted {Count} orphaned projects", projectsToDelete.Count);
            }

            // Step 7: Additional cleanup - Find any remaining orphaned ProjectIds in Students, ProjectBoards, ProjectModules
            // that don't exist in Projects table anymore
            var allExistingProjectIds = await _context.Projects.Select(p => p.Id).ToListAsync();
            
            // Fix students with non-existent ProjectIds
            var studentsWithNonExistentProjects = await _context.Students
                .Where(s => s.ProjectId.HasValue && !allExistingProjectIds.Contains(s.ProjectId.Value))
                .ToListAsync();

            foreach (var student in studentsWithNonExistentProjects)
            {
                var selectedProjectId = availableProjectIds[random.Next(availableProjectIds.Count)];
                student.ProjectId = selectedProjectId;
                student.UpdatedAt = DateTime.UtcNow;
                stats.StudentsFixedNonExistent++;
                _logger.LogInformation("Fixed student {StudentId} with non-existent ProjectId, assigned {ProjectId}", 
                    student.Id, selectedProjectId);
            }

            // Fix ProjectBoards with non-existent ProjectIds
            var boardsWithNonExistentProjects = await _context.ProjectBoards
                .Where(pb => !allExistingProjectIds.Contains(pb.ProjectId))
                .ToListAsync();

            foreach (var board in boardsWithNonExistentProjects)
            {
                var selectedProjectId = availableProjectIds[random.Next(availableProjectIds.Count)];
                board.ProjectId = selectedProjectId;
                board.UpdatedAt = DateTime.UtcNow;
                stats.ProjectBoardsFixedNonExistent++;
                _logger.LogInformation("Fixed ProjectBoard {BoardId} with non-existent ProjectId, assigned {ProjectId}", 
                    board.Id, selectedProjectId);
            }

            // Fix ProjectModules with non-existent ProjectIds
            var modulesWithNonExistentProjects = await _context.ProjectModules
                .Where(pm => pm.ProjectId.HasValue && !allExistingProjectIds.Contains(pm.ProjectId.Value))
                .ToListAsync();

            if (modulesWithNonExistentProjects.Any())
            {
                _context.ProjectModules.RemoveRange(modulesWithNonExistentProjects);
                stats.ProjectModulesDeletedNonExistent = modulesWithNonExistentProjects.Count;
                _logger.LogInformation("Deleted {Count} project modules with non-existent ProjectIds", 
                    modulesWithNonExistentProjects.Count);
            }

            // Save all changes
            await _context.SaveChangesAsync();
            _logger.LogInformation("All changes saved successfully");

            // Commit transaction
            await transaction.CommitAsync();
            _logger.LogWarning("Transaction committed - change-projects operation completed successfully");

            return Ok(new
            {
                Success = true,
                Message = "Projects changed successfully",
                Stats = stats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during change-projects operation");
            
            if (transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync();
                    _logger.LogInformation("Transaction rolled back due to error");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Error during transaction rollback");
                }
            }

            return StatusCode(500, new 
            { 
                Success = false, 
                Message = $"An error occurred while changing projects: {ex.Message}",
                Error = ex.ToString()
            });
        }
    }

    /// <summary>
    /// Get OpenAI account details (email, organization, etc.) using the configured API key
    /// </summary>
    [HttpGet("openai-account-details")]
    public async Task<ActionResult<object>> GetOpenAIAccountDetails()
    {
        try
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("OpenAI API key not configured");
                return BadRequest(new { Success = false, Message = "OpenAI API key not configured" });
            }

            var baseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1";
            _logger.LogInformation("Querying OpenAI account details from {BaseUrl}/me", baseUrl);

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var response = await httpClient.GetAsync($"{baseUrl}/me");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                
                return StatusCode((int)response.StatusCode, new
                {
                    Success = false,
                    Message = $"OpenAI API error: {response.StatusCode}",
                    Error = errorContent
                });
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var accountInfo = JsonSerializer.Deserialize<JsonElement>(responseContent);

            // Extract relevant fields
            var result = new Dictionary<string, object?>();
            if (accountInfo.TryGetProperty("id", out var idProp))
                result["id"] = idProp.GetString();
            if (accountInfo.TryGetProperty("email", out var emailProp))
                result["email"] = emailProp.GetString();
            if (accountInfo.TryGetProperty("name", out var nameProp))
                result["name"] = nameProp.GetString();
            if (accountInfo.TryGetProperty("object", out var objectProp))
                result["object"] = objectProp.GetString();
            if (accountInfo.TryGetProperty("picture", out var pictureProp))
                result["picture"] = pictureProp.GetString();
            if (accountInfo.TryGetProperty("created", out var createdProp))
                result["created"] = createdProp.GetInt64();
            if (accountInfo.TryGetProperty("organization_id", out var orgIdProp))
                result["organization_id"] = orgIdProp.GetString();
            if (accountInfo.TryGetProperty("organization", out var orgProp))
            {
                var orgDict = new Dictionary<string, object?>();
                if (orgProp.TryGetProperty("id", out var orgId))
                    orgDict["id"] = orgId.GetString();
                if (orgProp.TryGetProperty("name", out var orgName))
                    orgDict["name"] = orgName.GetString();
                if (orgProp.TryGetProperty("slug", out var orgSlug))
                    orgDict["slug"] = orgSlug.GetString();
                if (orgDict.Any())
                    result["organization"] = orgDict;
            }

            _logger.LogInformation("Successfully retrieved OpenAI account details for email: {Email}", result.ContainsKey("email") ? result["email"] : "unknown");

            return Ok(new
            {
                Success = true,
                Message = "OpenAI account details retrieved successfully",
                AccountDetails = result,
                RawResponse = responseContent // Include raw response for debugging
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving OpenAI account details");
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred while retrieving OpenAI account details: {ex.Message}",
                Error = ex.ToString()
            });
        }
    }
}

public class SetPasswordForAllRequest
{
    [Required]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}

public class ChangeProjectsStats
{
    public int StudentsUpdatedWithBoard { get; set; }
    public int StudentsUpdatedWithoutBoard { get; set; }
    public int StudentsFixedNonExistent { get; set; }
    public int ProjectBoardsUpdated { get; set; }
    public int ProjectBoardsFixedNonExistent { get; set; }
    public int OrphanedProjectsFound { get; set; }
    public int ProjectsDeleted { get; set; }
    public int ProjectModulesDeleted { get; set; }
    public int ProjectModulesDeletedNonExistent { get; set; }
    public int JoinRequestsDeleted { get; set; }
    
    public int TotalStudentsUpdated => StudentsUpdatedWithBoard + StudentsUpdatedWithoutBoard + StudentsFixedNonExistent;
    public int TotalProjectBoardsUpdated => ProjectBoardsUpdated + ProjectBoardsFixedNonExistent;
    public int TotalProjectsDeleted => ProjectsDeleted;
    public int TotalProjectModulesDeleted => ProjectModulesDeleted + ProjectModulesDeletedNonExistent;
}

