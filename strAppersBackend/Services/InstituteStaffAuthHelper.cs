using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

/// <summary>Resolves institute staff (<see cref="Teacher"/>) from the same <c>X-User-Email</c> / <c>X-User-Type</c> headers as project institute APIs.</summary>
public static class InstituteStaffAuthHelper
{
    public static async Task<Teacher?> ResolveActiveInstituteTeacherAsync(
        ApplicationDbContext context,
        Microsoft.AspNetCore.Http.HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var userType = request.Headers["X-User-Type"].FirstOrDefault()?.Trim();
        var userEmail = request.Headers["X-User-Email"].FirstOrDefault()?.Trim();

        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(userType) &&
            !string.Equals(userType, "institute", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await context.Teachers
            .AsNoTracking()
            .Include(t => t.Institute)
            .FirstOrDefaultAsync(
                t => t.Email.ToLower() == userEmail.ToLower() && t.Institute != null && t.Institute.IsActive,
                cancellationToken);
    }
}