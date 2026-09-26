using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;

namespace strAppersBackend.Utilities;

/// <summary>
/// The one place that decides which persona is the main (assessed) chat persona:
/// the project's <c>InstituteProjects.MainAIPersonaId</c> override first, then the institute's
/// <c>Institutes.MainAIPersonaId</c>, then none (callers fall back to the configured prompt and the
/// "Customer" label exactly as before). With no project override, every result equals the old
/// institute-only lookup, so existing institutes see no change.
/// Queries project only the persona columns: InstituteProjects rows carry large documents.
/// </summary>
public static class PersonaResolver
{
    public sealed record MainPersona(string Name, string? Prompt);

    /// <summary>Precedence rule, kept pure so it is unit-testable without a database.</summary>
    public static MainPersona? Pick(MainPersona? projectPersona, MainPersona? institutePersona) =>
        projectPersona ?? institutePersona;

    /// <summary>Main persona for a student, through the student's board project and institute.</summary>
    public static async Task<MainPersona?> ForStudentAsync(ApplicationDbContext context, int studentId, CancellationToken ct = default)
    {
        if (studentId <= 0)
            return null;

        var row = await context.Students.AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new
            {
                ProjectName = s.ProjectBoard != null && s.ProjectBoard.InstituteProject != null && s.ProjectBoard.InstituteProject.MainAIPersona != null
                    ? s.ProjectBoard.InstituteProject.MainAIPersona.Name : null,
                ProjectPrompt = s.ProjectBoard != null && s.ProjectBoard.InstituteProject != null && s.ProjectBoard.InstituteProject.MainAIPersona != null
                    ? s.ProjectBoard.InstituteProject.MainAIPersona.Prompt : null,
                InstituteName = s.Institute != null && s.Institute.MainAIPersona != null ? s.Institute.MainAIPersona.Name : null,
                InstitutePrompt = s.Institute != null && s.Institute.MainAIPersona != null ? s.Institute.MainAIPersona.Prompt : null
            })
            .FirstOrDefaultAsync(ct);

        return row == null ? null : Pick(
            row.ProjectName == null ? null : new MainPersona(row.ProjectName, row.ProjectPrompt),
            row.InstituteName == null ? null : new MainPersona(row.InstituteName, row.InstitutePrompt));
    }

    /// <summary>Main persona for a known project (for example a board's InstituteProjectId) and institute.</summary>
    public static async Task<MainPersona?> ForProjectAsync(ApplicationDbContext context, int? instituteProjectId, int? instituteId, CancellationToken ct = default)
    {
        MainPersona? projectPersona = null;
        if (instituteProjectId is > 0)
        {
            projectPersona = await context.InstituteProjects.AsNoTracking()
                .Where(p => p.Id == instituteProjectId.Value && p.MainAIPersona != null)
                .Select(p => new MainPersona(p.MainAIPersona!.Name, p.MainAIPersona.Prompt))
                .FirstOrDefaultAsync(ct);
        }
        if (projectPersona != null)
            return projectPersona;

        if (instituteId is null or <= 0)
            return null;

        return await context.Institutes.AsNoTracking()
            .Where(i => i.Id == instituteId.Value && i.MainAIPersona != null)
            .Select(i => new MainPersona(i.MainAIPersona!.Name, i.MainAIPersona.Prompt))
            .FirstOrDefaultAsync(ct);
    }
}
