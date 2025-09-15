using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

public interface IDesignDocumentService
{
    Task<SystemDesignResponse> CreateSystemDesignAsync(SystemDesignRequest request);
    Task<DesignVersion?> GetLatestDesignVersionAsync(int projectId);
    Task<List<DesignVersion>> GetDesignVersionsAsync(int projectId);
    Task<DesignVersion?> GetDesignVersionByIdAsync(int designVersionId);
    Task<bool> UpdateProjectWithDesignAsync(int projectId, string designDocument, byte[]? designDocumentPdf);
}

public class DesignDocumentService : IDesignDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;
    private readonly ILogger<DesignDocumentService> _logger;

    public DesignDocumentService(
        ApplicationDbContext context, 
        IAIService aiService, 
        ILogger<DesignDocumentService> logger)
    {
        _context = context;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<SystemDesignResponse> CreateSystemDesignAsync(SystemDesignRequest request)
    {
        try
        {
            _logger.LogInformation("Creating system design for Project {ProjectId}", request.ProjectId);

            // Validate project exists
            var project = await _context.Projects
                .Include(p => p.Students)
                .ThenInclude(s => s.StudentRoles)
                .ThenInclude(sr => sr.Role)
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

            if (project == null)
            {
                return new SystemDesignResponse
                {
                    Success = false,
                    Message = $"Project with ID {request.ProjectId} not found"
                };
            }

            // Get the next version number
            var latestVersion = await _context.DesignVersions
                .Where(dv => dv.ProjectId == request.ProjectId)
                .OrderByDescending(dv => dv.VersionNumber)
                .FirstOrDefaultAsync();

            var nextVersionNumber = (latestVersion?.VersionNumber ?? 0) + 1;

            // Generate system design using AI
            var aiResponse = await _aiService.GenerateSystemDesignAsync(request);
            
            if (!aiResponse.Success)
            {
                return aiResponse;
            }

            // Create design version record
            var designVersion = new DesignVersion
            {
                ProjectId = request.ProjectId,
                VersionNumber = nextVersionNumber,
                DesignDocument = aiResponse.DesignDocument ?? string.Empty,
                DesignDocumentPdf = aiResponse.DesignDocumentPdf,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.DesignVersions.Add(designVersion);

            // Update project with design document
            project.SystemDesign = aiResponse.DesignDocument;
            project.SystemDesignDoc = aiResponse.DesignDocumentPdf;
            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("System design created successfully for Project {ProjectId}, Version {VersionNumber}", 
                request.ProjectId, nextVersionNumber);

            return new SystemDesignResponse
            {
                Success = true,
                Message = "System design created successfully",
                DesignVersionId = designVersion.Id,
                DesignDocument = aiResponse.DesignDocument,
                DesignDocumentPdf = aiResponse.DesignDocumentPdf
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating system design for Project {ProjectId}", request.ProjectId);
            return new SystemDesignResponse
            {
                Success = false,
                Message = $"Error creating system design: {ex.Message}"
            };
        }
    }

    public async Task<DesignVersion?> GetLatestDesignVersionAsync(int projectId)
    {
        try
        {
            return await _context.DesignVersions
                .Where(dv => dv.ProjectId == projectId && dv.IsActive)
                .OrderByDescending(dv => dv.VersionNumber)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest design version for Project {ProjectId}", projectId);
            return null;
        }
    }

    public async Task<List<DesignVersion>> GetDesignVersionsAsync(int projectId)
    {
        try
        {
            return await _context.DesignVersions
                .Where(dv => dv.ProjectId == projectId)
                .OrderByDescending(dv => dv.VersionNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving design versions for Project {ProjectId}", projectId);
            return new List<DesignVersion>();
        }
    }

    public async Task<DesignVersion?> GetDesignVersionByIdAsync(int designVersionId)
    {
        try
        {
            return await _context.DesignVersions
                .Include(dv => dv.Project)
                .FirstOrDefaultAsync(dv => dv.Id == designVersionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving design version {DesignVersionId}", designVersionId);
            return null;
        }
    }

    public async Task<bool> UpdateProjectWithDesignAsync(int projectId, string designDocument, byte[]? designDocumentPdf)
    {
        try
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                _logger.LogWarning("Project {ProjectId} not found for design update", projectId);
                return false;
            }

            project.SystemDesign = designDocument;
            project.SystemDesignDoc = designDocumentPdf;
            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Project {ProjectId} updated with design document", projectId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {ProjectId} with design document", projectId);
            return false;
        }
    }
}
