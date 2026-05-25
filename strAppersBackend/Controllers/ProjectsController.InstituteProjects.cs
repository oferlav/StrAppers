using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

public partial class ProjectsController
{
    private async Task<ProjectReadyValidationDto> BuildProjectReadyValidationForCatalogProjectAsync(
        int catalogProjectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == catalogProjectId, cancellationToken);

        if (project == null)
        {
            return new ProjectReadyValidationDto
            {
                IsReady = false,
                MissingRequirements = new List<string> { "Project not found" },
            };
        }

        if (project.InstituteId != null)
        {
            return new ProjectReadyValidationDto
            {
                IsReady = false,
                MissingRequirements = new List<string> { "Not a built-in catalog project" },
            };
        }

        // Global catalog projects are always considered ready to activate for an institute.
        // (Header DTO uses Organization.Logo when Projects.Logo is empty; strict row checks were a false negative.)
        return new ProjectReadyValidationDto
        {
            IsReady = true,
            MissingRequirements = new List<string>(),
        };
    }

    private async Task<ProjectReadyValidationDto> BuildProjectReadyValidationForInstituteProjectRowAsync(
        int instituteProjectId,
        int expectedInstituteId,
        CancellationToken cancellationToken = default)
    {
        var missing = new List<string>();
        var ip = await _context.InstituteProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == instituteProjectId && p.InstituteId == expectedInstituteId, cancellationToken);

        if (ip == null)
        {
            return new ProjectReadyValidationDto
            {
                IsReady = false,
                MissingRequirements = new List<string> { "Institute project not found" },
            };
        }

        if (!HasMeaningfulText(ip.Title)) missing.Add("Title");
        if (!HasMeaningfulText(ip.Description)) missing.Add("Description");
        if (!HasMeaningfulText(ip.CustomerPastStory)) missing.Add("CustomerPastStory");
        if (!HasMeaningfulText(ip.ShortBrief)) missing.Add("ShortBrief");
        if (!HasMeaningfulText(ip.Mission)) missing.Add("Mission");
        if (!HasMeaningfulText(ip.OneLiner)) missing.Add("OneLiner");
        if (!HasMeaningfulText(ip.Logo)) missing.Add("Logo");

        var hasType2Module = await _context.InstituteProjectModules
            .AsNoTracking()
            .AnyAsync(pm => pm.InstituteProjectId == instituteProjectId && pm.ModuleType == 2, cancellationToken);
        if (!hasType2Module) missing.Add("At least one ProjectModule with ModuleType 2");

        return new ProjectReadyValidationDto
        {
            IsReady = missing.Count == 0,
            MissingRequirements = missing,
        };
    }

    private async Task<bool> InstituteProjectHasSyllabusForCatalogAsync(
        int catalogProjectId,
        int instituteId,
        CancellationToken cancellationToken = default)
    {
        // Only an explicit InstituteTemplates selection counts as a course assignment.
        return await _context.InstituteTemplates
            .AsNoTracking()
            .AnyAsync(t => t.InstituteId == instituteId && t.ProjectId == catalogProjectId, cancellationToken);
    }

    private async Task<bool> InstituteProjectHasSyllabusForInstituteProjectRowAsync(
        int instituteProjectId,
        int instituteId,
        CancellationToken cancellationToken = default)
    {
        // Only an explicit InstituteTemplates selection counts as a course assignment.
        // TrelloBoardJson on the InstituteProjects row is NOT sufficient — modules may have diverged after copy.
        return await _context.InstituteTemplates
            .AsNoTracking()
            .AnyAsync(t => t.InstituteId == instituteId && t.InstituteProjectId == instituteProjectId, cancellationToken);
    }

    /// <summary>
    /// Copies a built-in <see cref="Project"/> into <see cref="InstituteProjects"/> for the institute (activation).
    /// </summary>
    private async Task<ActionResult<object>> ActivateCatalogProjectIntoInstituteTableAsync(int catalogProjectId, int instituteId)
    {
        var catalog = await _context.Projects
            .AsNoTracking()
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == catalogProjectId && p.InstituteId == null && p.IsAvailable);
        if (catalog == null)
        {
            return NotFound($"Catalog project with ID {catalogProjectId} was not found or is not available.");
        }

        var catalogTitleKey = NormalizeProjectTitleKey(catalog.Title);
        var sameBaseRows = await _context.InstituteProjects
            .AsNoTracking()
            .Where(ip => ip.InstituteId == instituteId && ip.BaseProjectId == catalogProjectId)
            .Select(ip => new { ip.Id, ip.Title, ip.IsAvailable, ip.IsBuiltIn })
            .ToListAsync();
        var sameTitleRows = sameBaseRows
            .Where(t => NormalizeProjectTitleKey(t.Title) == catalogTitleKey)
            .ToList();
        // Prefer reusing an inactive draft mirror (saved course / modules) even when IsBuiltIn was false on older rows.
        var dormantSameTitle = sameTitleRows
            .Where(t => !t.IsAvailable)
            .OrderByDescending(t => t.IsBuiltIn)
            .FirstOrDefault();
        if (dormantSameTitle != null)
        {
            var existing = await _context.InstituteProjects.FirstAsync(ip => ip.Id == dormantSameTitle.Id);
            existing.IsAvailable = true;
            existing.IsBuiltIn = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new
            {
                Success = true,
                Message = "Project activated successfully.",
                Id = existing.Id,
                InstituteProject = true,
                BaseProjectId = catalogProjectId,
            });
        }

        if (sameTitleRows.Any(t => t.IsAvailable))
        {
            return Conflict("This catalog project is already activated for your institute with the same project name.");
        }

        // Do not gate catalog activation on header/module readiness; syllabus/template is enforced below.

        if (!await InstituteProjectHasSyllabusForCatalogAsync(catalogProjectId, instituteId))
        {
            return Ok(new
            {
                success = false,
                title = "Course assignment required",
                detail =
                    "This project cannot be activated because no course is assigned yet. " +
                    "In Project Designs, open the General tab and assign a course in the Course field, then try activating again.",
            });
        }

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var entity = InstituteProjectMapper.CopyFromProject(catalog, instituteId, catalogProjectId);
            entity.IsBuiltIn = true;
            _context.InstituteProjects.Add(entity);
            await _context.SaveChangesAsync();

            var sourceModules = await _context.ProjectModules
                .AsNoTracking()
                .Where(pm => pm.ProjectId == catalogProjectId)
                .OrderBy(pm => pm.Sequence ?? int.MaxValue)
                .ThenBy(pm => pm.Id)
                .ToListAsync();

            foreach (var pm in sourceModules)
            {
                _context.InstituteProjectModules.Add(new InstituteProjectModule
                {
                    InstituteProjectId = entity.Id,
                    ModuleType = pm.ModuleType,
                    Title = pm.Title,
                    Description = pm.Description,
                    Sequence = pm.Sequence,
                    OriginalModuleId = pm.Id,
                });
            }

            await _context.SaveChangesAsync();

            var newMods = await _context.InstituteProjectModules
                .Where(m => m.InstituteProjectId == entity.Id && m.OriginalModuleId != null)
                .ToListAsync();
            var map = newMods.ToDictionary(m => m.OriginalModuleId!.Value, m => m.Id);

            if (!string.IsNullOrWhiteSpace(entity.TrelloBoardJson))
            {
                var updatedJson = TryRemapTrelloJsonAfterProjectDuplicate(
                    entity.TrelloBoardJson!,
                    map,
                    catalogProjectId,
                    entity.Id);
                if (!string.IsNullOrWhiteSpace(updatedJson))
                {
                    entity.TrelloBoardJson = updatedJson;
                    entity.UpdatedAt = DateTime.UtcNow;
                }
            }

            var templates = await _context.InstituteTemplates
                .Where(t => t.InstituteId == instituteId && t.ProjectId == catalogProjectId)
                .ToListAsync();
            foreach (var t in templates)
            {
                t.ProjectId = null;
                t.InstituteProjectId = entity.Id;
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation(
                "Activated catalog Project {CatalogId} into InstituteProject {InstituteProjectId} for Institute {InstituteId}",
                catalogProjectId,
                entity.Id,
                instituteId);

            return Ok(new
            {
                Success = true,
                Message = "Project activated successfully.",
                Id = entity.Id,
                InstituteProject = true,
                BaseProjectId = catalogProjectId,
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "ActivateCatalogProjectIntoInstituteTableAsync failed for catalog {CatalogId}", catalogProjectId);
            throw;
        }
    }
}
