using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;

namespace strAppersBackend.Utilities;

/// <summary>
/// The Roles catalog holds duplicate rows per conceptual role (global default, institute copies,
/// per-squad copies — A3). Anywhere code means "this role CONCEPT" (not "this exact row"), it must
/// match against all duplicates. Identity = trimmed, case-insensitive name (product decision:
/// role-scoped configuration such as mentor prompts is per-concept, resolved by name).
/// </summary>
public static class RoleConceptResolver
{
    /// <summary>
    /// All Roles ids sharing the given role row's name — i.e. the role's duplicates across scopes.
    /// Unknown/nameless id resolves to just itself (degrades to the old exact-row behavior).
    /// </summary>
    public static async Task<List<int>> ResolveConceptRoleIdsAsync(
        ApplicationDbContext ctx, int roleId, CancellationToken ct = default)
    {
        var name = await ctx.Roles.AsNoTracking()
            .Where(r => r.Id == roleId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(name))
            return new List<int> { roleId };

        var normalized = name.Trim().ToLower();
        return await ctx.Roles.AsNoTracking()
            .Where(r => r.Name != null && r.Name.Trim().ToLower() == normalized)
            .Select(r => r.Id)
            .ToListAsync(ct);
    }
}
