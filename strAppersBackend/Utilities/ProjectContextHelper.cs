using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;

namespace strAppersBackend.Utilities;

/// <summary>
/// Resolves Pattern A project scalar fields (Description, CustomerPastStory) from the correct table.
/// For institute boards (<paramref name="boardInstituteProjectId"/> set) reads <c>InstituteProjects</c>;
/// for catalog/B2C boards reads <c>Projects</c>. Non-institute boards (InstituteId=0/1/null) always pass
/// null for <paramref name="boardInstituteProjectId"/> and will use the existing catalog path unchanged.
/// </summary>
public static class ProjectContextHelper
{
    public static async Task<(string? Description, string? CustomerPastStory, string DataSource)> GetEffectiveProjectDataAsync(
        ApplicationDbContext context,
        int boardProjectId,
        int? boardInstituteProjectId,
        CancellationToken cancellationToken = default)
    {
        if (boardInstituteProjectId.HasValue)
        {
            var ip = await context.InstituteProjects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == boardInstituteProjectId.Value, cancellationToken);
            if (ip != null)
                return (ip.Description, ip.CustomerPastStory, $"InstituteProjects.Id={boardInstituteProjectId.Value}");
        }

        var proj = await context.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == boardProjectId, cancellationToken);
        return (proj?.Description, proj?.CustomerPastStory, $"Projects.Id={boardProjectId}");
    }
}
