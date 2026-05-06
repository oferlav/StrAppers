using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Utilities;

/// <summary>
/// Resolves a module row whether the board uses a catalog <see cref="Project"/> id or an <see cref="InstituteProject"/> id in <see cref="Models.ProjectBoard.ProjectId"/>.
/// </summary>
public static class ProjectModuleLookup
{
    public static async Task<IProjectModuleRow?> FindByBoardScopeAsync(
        ApplicationDbContext context,
        int moduleId,
        int boardProjectId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await context.ProjectModules.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == moduleId && m.ProjectId == boardProjectId, cancellationToken);
        if (catalog != null)
            return catalog;

        return await context.InstituteProjectModules.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == moduleId && m.InstituteProjectId == boardProjectId, cancellationToken);
    }
}
