using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RolesController> _logger;
        private readonly KickoffConfig _kickoffConfig;

        public RolesController(
            ApplicationDbContext context,
            ILogger<RolesController> logger,
            IOptions<KickoffConfig> kickoffConfig)
        {
            _context = context;
            _logger = logger;
            _kickoffConfig = kickoffConfig.Value;
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
        /// Institute-scoped role rows for the roles configuration UI.
        /// </summary>
        [HttpGet("use/institute/{instituteId:int}")]
        public async Task<ActionResult<IEnumerable<InstituteRole>>> GetInstituteRoles(int instituteId)
        {
            try
            {
                var exists = await _context.Institutes.AsNoTracking().AnyAsync(i => i.Id == instituteId);
                if (!exists)
                    return NotFound($"Institute with ID {instituteId} not found");

                var list = await _context.InstituteRoles.AsNoTracking()
                    .Where(ir => ir.InstituteId == instituteId)
                    .OrderBy(ir => ir.Name)
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
        /// Saves institute role configuration: upserts rows in InstituteRoles (insert when Id omitted or new),
        /// updates when Id matches this institute, deletes rows not included in the payload.
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
                var templateIds = (await _context.InstituteTemplates.AsNoTracking()
                    .Where(t => t.InstituteId == request.InstituteId)
                    .Select(t => t.Id)
                    .ToListAsync())
                    .ToHashSet();

                foreach (var dto in request.Roles)
                {
                    if (!roleTypeIds.Contains(dto.Type))
                        return BadRequest($"Invalid Role Type {dto.Type}: not found in RoleTypes.");
                    if (dto.TemplateId.HasValue && dto.TemplateId.Value > 0 && !templateIds.Contains(dto.TemplateId.Value))
                        return BadRequest($"Invalid TemplateId {dto.TemplateId}: not found in InstituteTemplates for this institute.");
                }

                var now = DateTime.UtcNow;
                var payloadPositiveIds = request.Roles
                    .Where(r => r.Id.HasValue && r.Id.Value > 0)
                    .Select(r => r.Id!.Value)
                    .ToHashSet();

                await using var tx = await _context.Database.BeginTransactionAsync();

                var existingForInstitute = await _context.InstituteRoles
                    .Where(ir => ir.InstituteId == request.InstituteId)
                    .ToListAsync();

                foreach (var dto in request.Roles)
                {
                    var name = dto.Name.Trim();
                    if (name.Length == 0)
                        continue;

                    InstituteRole? row = null;
                    if (dto.Id.HasValue && dto.Id.Value > 0)
                    {
                        row = existingForInstitute.FirstOrDefault(er => er.Id == dto.Id.Value);
                        if (row == null)
                        {
                            await tx.RollbackAsync();
                            return BadRequest($"InstituteRole Id {dto.Id} not found for this institute.");
                        }
                    }

                    if (row != null)
                    {
                        row.Name = name;
                        row.Description = dto.Description;
                        row.Category = dto.Category;
                        row.TemplateId = dto.TemplateId > 0 ? dto.TemplateId : null;
                        row.Type = dto.Type;
                        row.IsActive = dto.IsActive;
                        row.UpdatedAt = now;
                    }
                    else
                    {
                        await _context.InstituteRoles.AddAsync(new InstituteRole
                        {
                            InstituteId = request.InstituteId,
                            Name = name,
                            Description = dto.Description,
                            Category = dto.Category,
                            TemplateId = dto.TemplateId > 0 ? dto.TemplateId : null,
                            Type = dto.Type,
                            IsActive = dto.IsActive,
                            CreatedAt = now,
                            UpdatedAt = null,
                        });
                    }
                }

                var toRemove = existingForInstitute.Where(er => !payloadPositiveIds.Contains(er.Id)).ToList();
                if (toRemove.Count > 0)
                    _context.InstituteRoles.RemoveRange(toRemove);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                var saved = await _context.InstituteRoles.AsNoTracking()
                    .Where(ir => ir.InstituteId == request.InstituteId)
                    .OrderBy(ir => ir.Name)
                    .ToListAsync();

                return Ok(new
                {
                    instituteId = request.InstituteId,
                    requireDeveloperRuleEcho = request.RequireDeveloperRule,
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
        /// Get all active roles
        /// </summary>
        [HttpGet("use")]
        public async Task<ActionResult<IEnumerable<Role>>> GetRoles()
        {
            try
            {
                var roles = await _context.Roles
                    .Where(r => r.IsActive)
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
                    .Where(r => r.Category == category && r.IsActive)
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

