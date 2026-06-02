using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RolesController> _logger;
        private readonly KickoffConfig _kickoffConfig;
        private readonly IAIService _aiService;

        public RolesController(
            ApplicationDbContext context,
            ILogger<RolesController> logger,
            IOptions<KickoffConfig> kickoffConfig,
            IAIService aiService)
        {
            _context = context;
            _logger = logger;
            _kickoffConfig = kickoffConfig.Value;
            _aiService = aiService;
        }

        /// <summary>
        /// UI bundle / developer-coverage rule flag from app settings (KickoffConfig:RequireDeveloperRule).
        /// </summary>
        [HttpGet("use/configuration")]
        public ActionResult<object> GetRolesUseConfiguration()
        {
            return Ok(new { requireDeveloperRule = _kickoffConfig.RequireDeveloperRule });
        }

        /// <summary>
        /// Skills lookup for roles configuration.
        /// </summary>
        [HttpGet("use/skills")]
        public async Task<ActionResult<IEnumerable<Skill>>> GetSkills()
        {
            try
            {
                var skills = await _context.Skills
                    .AsNoTracking()
                    .OrderBy(s => s.Name)
                    .ToListAsync();
                return Ok(skills);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving skills");
                return StatusCode(500, "An error occurred while retrieving skills.");
            }
        }

        /// <summary>
        /// Institute-scoped role rows for the roles configuration UI.
        /// Optional <paramref name="templateId"/> filters to roles saved for that institute template's squad.
        /// </summary>
        [HttpGet("use/institute/{instituteId:int}")]
        public async Task<ActionResult<IEnumerable<Role>>> GetInstituteRoles(
            int instituteId,
            [FromQuery] int? templateId = null)
        {
            try
            {
                var exists = await _context.Institutes.AsNoTracking().AnyAsync(i => i.Id == instituteId);
                if (!exists)
                    return NotFound($"Institute with ID {instituteId} not found");

                if (templateId is > 0)
                {
                    var template = await _context.InstituteTemplates
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == templateId.Value && t.InstituteId == instituteId);
                    if (template == null)
                        return NotFound($"InstituteTemplate {templateId.Value} not found for institute {instituteId}.");

                    if (template.SquadId is > 0)
                    {
                        var squadRoles = await _context.Roles
                            .AsNoTracking()
                            .Where(r => r.InstituteId == instituteId && r.SquadId == template.SquadId.Value)
                            .OrderBy(r => r.Name)
                            .ToListAsync();
                        return Ok(squadRoles);
                    }

                    return Ok(new List<Role>());
                }

                var list = await _context.Roles.AsNoTracking()
                    .Where(r => r.InstituteId == instituteId && r.SquadId == null)
                    .OrderBy(r => r.Name)
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving institute roles for institute {InstituteId}", instituteId);
                return StatusCode(500, "An error occurred while retrieving institute roles.");
            }
        }

        /// <summary>
        /// Distinct base role names currently assigned to at least one template squad for this institute.
        /// Used by UI to block deleting base roles that are already used in templates.
        /// </summary>
        [HttpGet("use/institute/{instituteId:int}/assigned-role-names")]
        public async Task<ActionResult<IEnumerable<string>>> GetAssignedRoleNames(int instituteId)
        {
            try
            {
                var exists = await _context.Institutes.AsNoTracking().AnyAsync(i => i.Id == instituteId);
                if (!exists)
                    return NotFound($"Institute with ID {instituteId} not found");

                var fromSquadTemplates = await _context.Roles
                    .AsNoTracking()
                    .Where(r => r.InstituteId == instituteId && r.SquadId != null)
                    .Join(
                        _context.InstituteTemplates.AsNoTracking().Where(t => t.SquadId != null),
                        r => r.SquadId,
                        t => t.SquadId,
                        (r, t) => r.Name)
                    .ToListAsync();

                var names = fromSquadTemplates
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList();

                return Ok(names);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assigned role names for institute {InstituteId}", instituteId);
                return StatusCode(500, "An error occurred while retrieving assigned role names.");
            }
        }

        /// <summary>
        /// Saves institute role configuration: upserts rows in InstituteRoles (insert when Id omitted or new),
        /// updates when Id matches this institute, deletes rows not included in the payload.
        /// When <see cref="SaveInstituteRolesRequest.TemplateScopeId"/> is set, only rows for that template are replaced;
        /// other institute rows are left unchanged.
        /// </summary>
        [HttpPost("use/institute")]
        public async Task<ActionResult<object>> SaveInstituteRoles([FromBody] SaveInstituteRolesRequest request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            try
            {
                var institute = await _context.Institutes.AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == request.InstituteId);
                if (institute == null)
                    return NotFound($"Institute with ID {request.InstituteId} not found");

                var roleTypeIds = (await _context.RoleTypes.Select(rt => rt.Id).ToListAsync()).ToHashSet();
                var skillIds = (await _context.Skills.Select(s => s.Id).ToListAsync()).ToHashSet();
                var templateIds = (await _context.InstituteTemplates.AsNoTracking()
                    .Where(t => t.InstituteId == request.InstituteId)
                    .Select(t => t.Id)
                    .ToListAsync())
                    .ToHashSet();

                var baseInstituteRoleIdSet = (await _context.Roles.AsNoTracking()
                    .Where(r => r.InstituteId == request.InstituteId && r.SquadId == null)
                    .Select(r => r.Id)
                    .ToListAsync())
                    .ToHashSet();

                int? forceTemplateId = null;
                if (request.TemplateScopeId is > 0)
                {
                    if (!templateIds.Contains(request.TemplateScopeId.Value))
                        return BadRequest(
                            $"Invalid TemplateScopeId {request.TemplateScopeId}: not found in InstituteTemplates for this institute.");
                    forceTemplateId = request.TemplateScopeId.Value;
                }

                foreach (var dto in request.Roles)
                {
                    if (!roleTypeIds.Contains(dto.Type))
                        return BadRequest($"Invalid Role Type {dto.Type}: not found in RoleTypes.");
                    if (dto.SkillId is > 0 && !skillIds.Contains(dto.SkillId.Value))
                        return BadRequest($"Invalid SkillId {dto.SkillId}: not found in Skills.");
                    if (dto.BaseInstituteRoleId is > 0 && !baseInstituteRoleIdSet.Contains(dto.BaseInstituteRoleId.Value))
                    {
                        return BadRequest(
                            $"Invalid BaseInstituteRoleId {dto.BaseInstituteRoleId}: not an institute base role for this institute.");
                    }

                    var effectiveTid = forceTemplateId ?? (dto.TemplateId is > 0 ? dto.TemplateId : null);
                    if (effectiveTid is > 0 && !templateIds.Contains(effectiveTid.Value))
                        return BadRequest($"Invalid TemplateId {effectiveTid}: not found in InstituteTemplates for this institute.");
                }

                var now = DateTime.UtcNow;
                var payloadPositiveIds = request.Roles
                    .Where(r => r.Id.HasValue && r.Id.Value > 0)
                    .Select(r => r.Id!.Value)
                    .ToHashSet();

                await using var tx = await _context.Database.BeginTransactionAsync();

                if (forceTemplateId is int templateScopeId)
                {
                    var template = await _context.InstituteTemplates
                        .FirstOrDefaultAsync(t => t.Id == templateScopeId && t.InstituteId == request.InstituteId);
                    if (template == null)
                    {
                        await tx.RollbackAsync();
                        return BadRequest($"InstituteTemplate {templateScopeId} not found for this institute.");
                    }

                    InstituteSquad squad;
                    if (template.SquadId is > 0)
                    {
                        squad = await _context.InstituteSquads
                            .FirstOrDefaultAsync(s => s.Id == template.SquadId.Value && s.InstituteId == request.InstituteId)
                            ?? new InstituteSquad
                            {
                                InstituteId = request.InstituteId,
                                Name = $"{template.CourseName} Squad",
                                IsActive = true,
                                CreatedAt = now,
                            };
                        if (squad.Id == 0)
                            await _context.InstituteSquads.AddAsync(squad);
                    }
                    else
                    {
                        squad = new InstituteSquad
                        {
                            InstituteId = request.InstituteId,
                            Name = $"{template.CourseName} Squad",
                            IsActive = true,
                            CreatedAt = now,
                        };
                        await _context.InstituteSquads.AddAsync(squad);
                    }

                    if (request.RequireDeveloperRule.HasValue)
                        squad.RequireDeveloperRule = request.RequireDeveloperRule.Value;
                    else if (squad.Id == 0)
                        squad.RequireDeveloperRule = false;

                    await _context.SaveChangesAsync();

                    template.SquadId = squad.Id;

                    var existingSquadRoles = await _context.Roles
                        .Where(r => r.InstituteId == request.InstituteId && r.SquadId == squad.Id)
                        .ToListAsync();
                    var existingSquadIds = existingSquadRoles.Select(er => er.Id).ToHashSet();
                    var payloadSquadIds = request.Roles
                        .Where(r => r.Id.HasValue && r.Id.Value > 0 && existingSquadIds.Contains(r.Id.Value))
                        .Select(r => r.Id!.Value)
                        .ToHashSet();

                    foreach (var dto in request.Roles)
                    {
                        var name = dto.Name.Trim();
                        if (name.Length == 0)
                            continue;

                        Role? row = null;
                        if (dto.Id.HasValue && dto.Id.Value > 0 && existingSquadIds.Contains(dto.Id.Value))
                        {
                            row = existingSquadRoles.FirstOrDefault(er => er.Id == dto.Id.Value);
                            if (row == null)
                            {
                                await tx.RollbackAsync();
                                return BadRequest($"Role Id {dto.Id} not found for this squad scope.");
                            }
                        }

                        if (row != null)
                        {
                            row.Name = name;
                            row.Description = dto.Description;
                            row.Competencies = dto.Competencies;
                            row.Category = dto.Category ?? string.Empty;
                            row.Type = dto.Type;
                            row.SkillId = dto.SkillId is > 0 ? dto.SkillId : null;
                            row.CustomerEngagement = dto.CustomerEngagement;
                            row.IsTechnical = dto.IsTechnical;
                            row.IsActive = dto.IsActive;
                            row.UpdatedAt = now;
                        }
                        else
                        {
                            await _context.Roles.AddAsync(new Role
                            {
                                InstituteId = request.InstituteId,
                                SquadId = squad.Id,
                                Name = name,
                                Description = dto.Description,
                                Competencies = dto.Competencies,
                                Category = dto.Category ?? string.Empty,
                                Type = dto.Type,
                                SkillId = dto.SkillId is > 0 ? dto.SkillId : null,
                                CustomerEngagement = dto.CustomerEngagement,
                                IsTechnical = dto.IsTechnical,
                                IsActive = dto.IsActive,
                                CreatedAt = now,
                                UpdatedAt = null,
                            });
                        }
                    }

                    var toRemoveSquad = existingSquadRoles.Where(er => !payloadSquadIds.Contains(er.Id)).ToList();
                    if (toRemoveSquad.Count > 0)
                        _context.Roles.RemoveRange(toRemoveSquad);

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    var savedSquadRoles = await _context.Roles.AsNoTracking()
                        .Where(r => r.InstituteId == request.InstituteId && r.SquadId == squad.Id)
                        .OrderBy(r => r.Name)
                        .ToListAsync();

                    return Ok(new
                    {
                        instituteId = request.InstituteId,
                        requireDeveloperRule = squad.RequireDeveloperRule,
                        requireDeveloperRuleEcho = request.RequireDeveloperRule,
                        templateScopeId = request.TemplateScopeId,
                        squadId = squad.Id,
                        instituteRoles = savedSquadRoles,
                    });
                }

                // Base path: only base-scoped rows (SquadId=null) for this institute.
                // Safety: never touch InstituteId=1 rows — they are global/B2C.
                var existingForInstitute = await _context.Roles
                    .Where(r => r.InstituteId == request.InstituteId && r.SquadId == null)
                    .ToListAsync();

                foreach (var dto in request.Roles)
                {
                    var name = dto.Name.Trim();
                    if (name.Length == 0)
                        continue;

                    Role? row = null;
                    if (dto.Id.HasValue && dto.Id.Value > 0)
                    {
                        row = existingForInstitute.FirstOrDefault(er => er.Id == dto.Id.Value);
                        // dto.Id may be a global role Id — if not found in this institute's rows,
                        // create a new institute-specific row instead of erroring.
                    }

                    if (row != null)
                    {
                        // Safety guard: never update a global row
                        if (row.InstituteId == 1)
                        {
                            await tx.RollbackAsync();
                            return BadRequest("Cannot modify a global role via institute endpoint.");
                        }
                        row.Name = name;
                        row.Description = dto.Description;
                        row.Competencies = dto.Competencies;
                        row.Category = dto.Category ?? string.Empty;
                        row.Type = dto.Type;
                        row.SkillId = dto.SkillId is > 0 ? dto.SkillId : null;
                        row.CustomerEngagement = dto.CustomerEngagement;
                        row.IsTechnical = dto.IsTechnical;
                        row.IsActive = dto.IsActive;
                        row.UpdatedAt = now;
                    }
                    else
                    {
                        await _context.Roles.AddAsync(new Role
                        {
                            InstituteId = request.InstituteId,
                            SquadId = null,
                            Name = name,
                            Description = dto.Description,
                            Competencies = dto.Competencies,
                            Category = dto.Category ?? string.Empty,
                            Type = dto.Type,
                            SkillId = dto.SkillId is > 0 ? dto.SkillId : null,
                            CustomerEngagement = dto.CustomerEngagement,
                            IsTechnical = dto.IsTechnical,
                            IsActive = dto.IsActive,
                            CreatedAt = now,
                            UpdatedAt = null,
                        });
                    }
                }

                var toRemove = existingForInstitute.Where(er => !payloadPositiveIds.Contains(er.Id)).ToList();
                if (toRemove.Count > 0)
                    _context.Roles.RemoveRange(toRemove);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                var saved = await _context.Roles.AsNoTracking()
                    .Where(r => r.InstituteId == request.InstituteId && r.SquadId == null)
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                return Ok(new
                {
                    instituteId = request.InstituteId,
                    requireDeveloperRuleEcho = request.RequireDeveloperRule,
                    templateScopeId = request.TemplateScopeId,
                    instituteRoles = saved,
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "DB error saving institute roles for institute {InstituteId}", request.InstituteId);
                return Conflict("Could not save institute roles (constraint violation). Check RoleTypes and duplicates.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving institute roles for institute {InstituteId}", request.InstituteId);
                return StatusCode(500, "An error occurred while saving institute roles.");
            }
        }

        /// <summary>
        /// Deletes one institute-only custom role (<see cref="InstituteRole"/> with <c>TemplateId == null</c>).
        /// Refuses if the role name exists in the global <see cref="Role"/> catalog, or if any squad references it.
        /// </summary>
        [HttpDelete("use/institute/{instituteId:int}/role/{roleId:int}")]
        public async Task<ActionResult<object>> DeleteInstituteRole(int instituteId, int roleId)
        {
            try
            {
                var row = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Id == roleId && r.InstituteId == instituteId && r.SquadId == null);
                if (row == null)
                    return NotFound($"Institute base role {roleId} was not found for institute {instituteId}.");

                var roleName = row.Name.Trim();
                if (!string.IsNullOrWhiteSpace(roleName))
                {
                    var isGlobalCatalogRole = await _context.Roles
                        .AsNoTracking()
                        .AnyAsync(r => r.InstituteId == 1 && r.Name.ToLower() == roleName.ToLower());
                    if (isGlobalCatalogRole)
                    {
                        return BadRequest(
                            $"Role \"{row.Name}\" is part of the global role catalog and cannot be deleted. You can turn it off for this institute or remove it from squads, but the system role itself stays in the catalog.");
                    }
                }

                var usedBySquad = !string.IsNullOrWhiteSpace(roleName) &&
                    await _context.Roles
                        .AsNoTracking()
                        .AnyAsync(r => r.InstituteId == instituteId && r.SquadId != null &&
                                       r.Name.Trim().ToLower() == roleName.ToLower());

                if (usedBySquad)
                {
                    return BadRequest(
                        $"Role \"{row.Name}\" is used in one or more squads. Remove it from those squad(s) first, then delete it.");
                }

                _context.Roles.Remove(row);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = $"Role \"{row.Name}\" deleted." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting institute role {RoleId} for institute {InstituteId}", roleId, instituteId);
                return StatusCode(500, "An error occurred while deleting the role.");
            }
        }

        /// <summary>
        /// AI assistance for improving role competencies text (spelling, grammar, clarity, structure, tech stack, and AI-related expectations).
        /// </summary>
        [HttpPost("use/ai-competencies-assistance")]
        public async Task<ActionResult<RoleCompetenciesAssistanceResponse>> AssistCompetencies(
            [FromBody] RoleCompetenciesAssistanceRequest request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var roleName = request.RoleName.Trim();
            if (roleName.Length == 0)
            {
                return BadRequest(new RoleCompetenciesAssistanceResponse
                {
                    Success = false,
                    Message = "RoleName is required.",
                    Competencies = string.Empty,
                });
            }

            try
            {
                var source = (request.Competencies ?? string.Empty).Trim();
                var instruction = (request.AiInstruction ?? string.Empty).Trim();
                var prompt = $"""
You are an education product assistant helping teachers define role competencies.

Rewrite and improve the competencies text for this role:
- Role: {roleName}
- Technical role: {request.IsTechnical}
{(instruction.Length > 0 ? $"- Teacher instruction: {instruction}" : string.Empty)}

Requirements:
1) Fix English grammar and spelling.
2) Keep the original intent; do not invent unrelated skills.
3) Make the text clear, concise, and practical for students.
4) Use a clean bullet list format.
5) If the input is very short or empty, propose a strong baseline competencies list for this role.
6) For technical roles, emphasize technical capabilities. For non-technical roles, emphasize communication, planning, and domain competencies.
7) Always weave in explicit tech-stack expectations when the role is technical or engineering-oriented: languages, frameworks, platforms, and tooling that fit the role name and teacher instruction. If no stack is stated, infer a concise, realistic baseline; if the instruction names specific technologies, align to those.
8) Always include AI-related expectations where they matter for the role: for technical roles, cover responsible use of AI coding assistants, testing/review with AI, and relevant ML/GenAI literacy; for non-technical roles, cover AI literacy, safe and ethical use, and using AI tools appropriately for that discipline—not low-level implementation unless the role demands it.
9) If the teacher instruction specifies tech stack or AI constraints, reflect them directly in the bullets.

Teacher draft:
{source}

Return only the final competencies text.
""";

                var assisted = (await _aiService.GenerateTextResponseAsync(prompt))?.Trim() ?? string.Empty;
                if (assisted.Length == 0)
                {
                    return StatusCode(502, new RoleCompetenciesAssistanceResponse
                    {
                        Success = false,
                        Message = "AI service returned an empty response.",
                        Competencies = string.Empty,
                    });
                }

                return Ok(new RoleCompetenciesAssistanceResponse
                {
                    Success = true,
                    Message = "Competencies text improved successfully.",
                    Competencies = assisted,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assisting competencies text for role {RoleName}", roleName);
                return StatusCode(500, new RoleCompetenciesAssistanceResponse
                {
                    Success = false,
                    Message = "An error occurred while generating competencies assistance.",
                    Competencies = string.Empty,
                });
            }
        }

        /// <summary>
        /// Get all active roles
        /// </summary>
        [HttpGet("use")]
        public async Task<ActionResult<IEnumerable<Role>>> GetRoles()
        {
            try
            {
                var roles = await _context.Roles
                    .Where(r => r.InstituteId == 1 && r.IsActive)
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles");
                return StatusCode(500, "An error occurred while retrieving roles");
            }
        }

        /// <summary>
        /// Get all roles (including inactive)
        /// </summary>
        [HttpGet("use/all")]
        public async Task<ActionResult<IEnumerable<Role>>> GetAllRoles()
        {
            try
            {
                var roles = await _context.Roles
                    .Where(r => r.InstituteId == 1)
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all roles");
                return StatusCode(500, "An error occurred while retrieving all roles");
            }
        }

        /// <summary>
        /// Get a specific role by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Role>> GetRole(int id)
        {
            try
            {
                var role = await _context.Roles.FindAsync(id);

                if (role == null)
                {
                    return NotFound($"Role with ID {id} not found");
                }

                return Ok(role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving role with ID {RoleId}", id);
                return StatusCode(500, "An error occurred while retrieving the role");
            }
        }

        /// <summary>
        /// Get roles by category
        /// </summary>
        [HttpGet("by-category/{category}")]
        public async Task<ActionResult<IEnumerable<Role>>> GetRolesByCategory(string category)
        {
            try
            {
                var roles = await _context.Roles
                    .Where(r => r.InstituteId == 1 && r.Category == category && r.IsActive)
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles for category {Category}", category);
                return StatusCode(500, "An error occurred while retrieving roles for the category");
            }
        }

        /// <summary>
        /// Get students with a specific role
        /// </summary>
        [HttpGet("{id}/students")]
        public async Task<ActionResult<IEnumerable<Student>>> GetRoleStudents(int id)
        {
            try
            {
                var role = await _context.Roles.FindAsync(id);
                if (role == null)
                {
                    return NotFound($"Role with ID {id} not found");
                }

                var students = await _context.Students
                    .Include(s => s.Major)
                    .Include(s => s.Year)
                    .Include(s => s.ProjectBoard)
                .ThenInclude(pb => pb.Project)
                    .Include(s => s.StudentRoles)
                    .Where(s => s.StudentRoles.Any(sr => sr.RoleId == id))
                    .OrderBy(s => s.LastName)
                    .ThenBy(s => s.FirstName)
                    .ToListAsync();

                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving students for role {RoleId}", id);
                return StatusCode(500, "An error occurred while retrieving students for the role");
            }
        }
    }
}

