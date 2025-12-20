using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

public interface IKickoffService
{
    Task<bool> ShouldKickoffBeTrue(int projectId, IEnumerable<int> studentIds);
}

public class KickoffService : IKickoffService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<KickoffService> _logger;
    private readonly KickoffConfig _kickoffConfig;

    public KickoffService(ApplicationDbContext context, ILogger<KickoffService> logger, IOptions<KickoffConfig> kickoffConfig)
    {
        _context = context;
        _logger = logger;
        _kickoffConfig = kickoffConfig.Value;
    }

    /// <summary>
    /// Determines if the Kickoff flag should be true based on configuration rules
    /// Rules (configurable):
    /// 1. Minimum number of students (default: 2)
    /// 2. At least one admin required (default: true)
    /// 3. UI/UX Designer required (default: true, Type=3) - exactly 1 required
    /// 4. Product Manager required (default: false, Type=4) - exactly 1 required
    /// 5. Developer rule required (default: true) - at least 1 student with Type=1 OR at least 2 students with Type=2
    /// </summary>
    /// <param name="projectId">The project ID to check</param>
    /// <param name="studentIds">Collection of student IDs to evaluate</param>
    /// <returns>True if Kickoff should be set to true, false otherwise</returns>
    public async Task<bool> ShouldKickoffBeTrue(int projectId, IEnumerable<int> studentIds)
    {
        try
        {
            _logger.LogInformation("========== KICKOFF CHECK START ==========");
            _logger.LogInformation("ProjectId: {ProjectId}", projectId);
            _logger.LogInformation("Student IDs provided: [{StudentIds}]", string.Join(", ", studentIds));
            _logger.LogInformation("Total student IDs provided: {Count}", studentIds.Count());
            _logger.LogInformation("Configuration: MinStudents={MinStudents}, RequireAdmin={RequireAdmin}, RequireUIUX={RequireUIUX}, RequireProductManager={RequireProductManager}, RequireDeveloper={RequireDeveloper}", 
                _kickoffConfig.MinimumStudents, _kickoffConfig.RequireAdmin, _kickoffConfig.RequireUIUXDesigner, _kickoffConfig.RequireProductManager, _kickoffConfig.RequireDeveloperRule);

            if (!studentIds.Any())
            {
                _logger.LogInformation("❌ No students provided for Kickoff check on Project {ProjectId}", projectId);
                _logger.LogInformation("========== KICKOFF CHECK END: FALSE ==========");
                return false;
            }

            // Get students without board assignment from the provided student IDs
            var studentsWithoutBoard = await _context.Students
                .Include(s => s.StudentRoles)
                .ThenInclude(sr => sr.Role)
                .Where(s => studentIds.Contains(s.Id) && 
                           s.ProjectId == projectId && 
                           (s.BoardId == null || s.BoardId == ""))
                .ToListAsync();

            _logger.LogInformation("Students without board found: {Count}", studentsWithoutBoard.Count);
            
            foreach (var student in studentsWithoutBoard)
            {
                var activeRoles = student.StudentRoles.Where(sr => sr.IsActive).ToList();
                _logger.LogInformation("  - Student ID: {StudentId}, Name: {FirstName} {LastName}, BoardId: '{BoardId}', ProjectId: {ProjectId}, IsAdmin: {IsAdmin}", 
                    student.Id, student.FirstName, student.LastName, student.BoardId ?? "NULL", student.ProjectId, student.IsAdmin);
                _logger.LogInformation("    Active Roles: [{RoleDetails}]", 
                    string.Join(", ", activeRoles.Select(r => $"RoleId={r.RoleId}, Type={r.Role?.Type}, Name={r.Role?.Name}")));
            }

            // Rule 1: Minimum number of students
            if (studentsWithoutBoard.Count < _kickoffConfig.MinimumStudents)
            {
                _logger.LogInformation("❌ RULE 1 FAILED: Only {Count} student(s) without board (need at least {MinStudents})", 
                    studentsWithoutBoard.Count, _kickoffConfig.MinimumStudents);
                _logger.LogInformation("========== KICKOFF CHECK END: FALSE ==========");
                return false;
            }
            _logger.LogInformation("✅ RULE 1 PASSED: Found {Count} students without board (>= {MinStudents})", 
                studentsWithoutBoard.Count, _kickoffConfig.MinimumStudents);

            // Rule 2: Admin requirement (if enabled)
            if (_kickoffConfig.RequireAdmin)
            {
                bool hasAdmin = studentsWithoutBoard.Any(s => s.IsAdmin);
                if (!hasAdmin)
                {
                    _logger.LogInformation("❌ RULE 2 FAILED: No students with IsAdmin = true (admin required)");
                    _logger.LogInformation("========== KICKOFF CHECK END: FALSE ==========");
                    return false;
                }
                _logger.LogInformation("✅ RULE 2 PASSED: Found at least one admin");
            }
            else
            {
                _logger.LogInformation("✅ RULE 2 SKIPPED: Admin requirement disabled");
            }

            // Rule 3: UI/UX Designer requirement (if enabled) - exactly 1 required
            if (_kickoffConfig.RequireUIUXDesigner)
            {
                var uiuxDesignerCount = studentsWithoutBoard.Count(s => 
                    s.StudentRoles.Any(sr => sr.IsActive && sr.Role?.Type == 3));
                
                if (uiuxDesignerCount != 1)
                {
                    _logger.LogInformation("❌ RULE 3 FAILED: UI/UX Designer count is {Count} (exactly 1 required, Type=3)", uiuxDesignerCount);
                    _logger.LogInformation("========== KICKOFF CHECK END: FALSE ==========");
                    return false;
                }
                _logger.LogInformation("✅ RULE 3 PASSED: Found exactly one UI/UX Designer");
            }
            else
            {
                _logger.LogInformation("✅ RULE 3 SKIPPED: UI/UX Designer requirement disabled");
            }

            // Rule 4: Product Manager requirement (if enabled) - exactly 1 required
            if (_kickoffConfig.RequireProductManager)
            {
                var productManagerCount = studentsWithoutBoard.Count(s => 
                    s.StudentRoles.Any(sr => sr.IsActive && sr.Role?.Type == 4));
                
                if (productManagerCount != 1)
                {
                    _logger.LogInformation("❌ RULE 4 FAILED: Product Manager count is {Count} (exactly 1 required, Type=4)", productManagerCount);
                    _logger.LogInformation("========== KICKOFF CHECK END: FALSE ==========");
                    return false;
                }
                _logger.LogInformation("✅ RULE 4 PASSED: Found exactly one Product Manager");
            }
            else
            {
                _logger.LogInformation("✅ RULE 4 SKIPPED: Product Manager requirement disabled");
            }

            // Rule 5: Developer rule (if enabled)
            if (_kickoffConfig.RequireDeveloperRule)
            {
                // Check for at least 1 student with Type=1 (Developer) OR at least 2 students with Type=2 (Junior Developer)
                var developers = studentsWithoutBoard.Where(s => 
                    s.StudentRoles.Any(sr => sr.IsActive && sr.Role?.Type == 1)).Count();
                var juniorDevelopers = studentsWithoutBoard.Where(s => 
                    s.StudentRoles.Any(sr => sr.IsActive && sr.Role?.Type == 2)).Count();

                bool developerRuleMet = developers >= 1 || juniorDevelopers >= 2;

                if (!developerRuleMet)
                {
                    _logger.LogInformation("❌ RULE 5 FAILED: Developer rule not met - Developers: {Developers}, Junior Developers: {JuniorDevelopers} (need 1+ Developer OR 2+ Junior Developer)", 
                        developers, juniorDevelopers);
                    _logger.LogInformation("========== KICKOFF CHECK END: FALSE ==========");
                    return false;
                }
                _logger.LogInformation("✅ RULE 5 PASSED: Developer rule met - Developers: {Developers}, Junior Developers: {JuniorDevelopers}", 
                    developers, juniorDevelopers);
            }
            else
            {
                _logger.LogInformation("✅ RULE 5 SKIPPED: Developer rule requirement disabled");
            }

            _logger.LogInformation("✅ ALL RULES PASSED: Kickoff should be TRUE for Project {ProjectId}", projectId);
            _logger.LogInformation("========== KICKOFF CHECK END: TRUE ==========");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Kickoff status for Project {ProjectId}", projectId);
            _logger.LogInformation("========== KICKOFF CHECK END: ERROR (returning FALSE) ==========");
            return false;
        }
    }
}

