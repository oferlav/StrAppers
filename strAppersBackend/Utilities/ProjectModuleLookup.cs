using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Utilities;

/// <summary>
/// Resolves module rows whether the board uses a catalog <see cref="Project"/> or an <see cref="InstituteProject"/>.
/// When <paramref name="boardInstituteProjectId"/> is set (institute boards), <see cref="InstituteProjectModules"/>
/// are tried first with the correct FK. Non-institute boards (InstituteId=0/1/null) pass null and get the
/// original catalog-first behavior unchanged.
/// </summary>
public static class ProjectModuleLookup
{
    /// <summary>Resolves a module row by Id.</summary>
    public static async Task<IProjectModuleRow?> FindByBoardScopeAsync(
        ApplicationDbContext context,
        int moduleId,
        int boardProjectId,
        int? boardInstituteProjectId = null,
        CancellationToken cancellationToken = default)
    {
        // Institute board: try institute modules with the correct InstituteProjectId first
        if (boardInstituteProjectId.HasValue)
        {
            var inst = await context.InstituteProjectModules.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == moduleId && m.InstituteProjectId == boardInstituteProjectId.Value, cancellationToken);
            if (inst != null)
                return inst;
        }

        // Catalog path (primary for non-institute boards; fallback for institute boards)
        var catalog = await context.ProjectModules.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == moduleId && m.ProjectId == boardProjectId, cancellationToken);
        if (catalog != null)
            return catalog;

        // Legacy fallback: preserves prior behavior for non-institute boards
        if (!boardInstituteProjectId.HasValue)
        {
            return await context.InstituteProjectModules.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == moduleId && m.InstituteProjectId == boardProjectId, cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// Resolves all module rows matching a Sequence value. Used by <c>CustomerController</c> which needs
    /// all modules for a given sprint sequence. For institute boards tries <see cref="InstituteProjectModules"/>
    /// first; falls back to <see cref="Data.ApplicationDbContext.ProjectModules"/>.
    /// </summary>
    public static async Task<List<IProjectModuleRow>> FindManyBySequenceAsync(
        ApplicationDbContext context,
        int sequence,
        int boardProjectId,
        int? boardInstituteProjectId = null,
        CancellationToken cancellationToken = default)
    {
        if (boardInstituteProjectId.HasValue)
        {
            var instList = await context.InstituteProjectModules.AsNoTracking()
                .Where(m => m.Sequence == sequence && m.InstituteProjectId == boardInstituteProjectId.Value)
                .OrderBy(m => m.Id)
                .ToListAsync(cancellationToken);
            if (instList.Count > 0)
                return instList.Cast<IProjectModuleRow>().ToList();
        }

        var catalogList = await context.ProjectModules.AsNoTracking()
            .Where(m => m.Sequence == sequence && m.ProjectId == boardProjectId)
            .OrderBy(m => m.Id)
            .ToListAsync(cancellationToken);
        return catalogList.Cast<IProjectModuleRow>().ToList();
    }
}
