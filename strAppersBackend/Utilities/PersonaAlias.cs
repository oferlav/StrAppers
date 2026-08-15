using Microsoft.EntityFrameworkCore;
using strAppersBackend.Controllers;
using strAppersBackend.Data;

namespace strAppersBackend.Utilities;

/// <summary>
/// Renames the simulated stakeholder for the LLM without touching a single existing prompt.
///
/// The mentor reviews, the metrics prompts and the course builder all talk about the "AI Customer"
/// in prose that is heavily tuned (StoryReviewSystem.txt alone spends eleven lines on what may and
/// may not be said about the customer conversation). Rewriting that prose to say "AI Professor"
/// risks changing what those prompts actually evaluate. Prepending an alias block instead can only
/// change what the model calls things, never what it scores — so the blast radius of getting it
/// wrong is one word in generated prose.
///
/// <see cref="BuildAliasBlock"/> returns an empty string whenever no persona applies, and
/// <see cref="Prepend"/> is then a no-op: every institute without a persona gets a byte-identical
/// system prompt. That is the property <c>PersonaAliasTests</c> exists to prove.
///
/// Deliberately NOT applied to Gap Analysis: its prompt keys on the literal presence of
/// "### Customer chat history (AI Customer; …)" to decide whether to score a customer-alignment
/// category, and that coupling is not worth disturbing for a naming change.
/// </summary>
public static class PersonaAlias
{
    /// <summary>
    /// The alias instruction, or empty when there is nothing to rename — no persona selected, or a
    /// persona that is already called "Customer" (renaming Customer to Customer is pure noise).
    /// </summary>
    public static string BuildAliasBlock(string? personaName)
    {
        var name = (personaName ?? string.Empty).Trim();
        if (name.Length == 0)
            return string.Empty;
        if (string.Equals(name, PersonasController.DefaultPersonaName, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return $"""
            === PERSONA NAMING ===
            The simulated stakeholder in this deployment is the "AI {name}", not the "AI Customer".
            Wherever these instructions or the context below say "AI Customer", "the customer", or
            "customer chat", they refer to the AI {name}. Use "AI {name}" in everything you write.
            === END PERSONA NAMING ===
            """;
    }

    /// <summary>
    /// Puts the alias at the very top of a system prompt so it frames everything after it.
    /// A blank block returns the prompt unchanged — reference-equal, not merely equal.
    /// </summary>
    public static string Prepend(string aliasBlock, string systemPrompt) =>
        string.IsNullOrEmpty(aliasBlock) ? systemPrompt : $"{aliasBlock}\n\n{systemPrompt}";

    /// <summary>Alias block for the persona an institute selected. Empty for institutes with none.</summary>
    public static async Task<string> ResolveForInstituteAsync(
        ApplicationDbContext context, int? instituteId, CancellationToken ct = default)
    {
        if (instituteId is null or <= 0)
            return string.Empty;

        var name = await context.Institutes.AsNoTracking()
            .Where(i => i.Id == instituteId.Value && i.MainAIPersonaId != null)
            .Select(i => i.MainAIPersona!.Name)
            .FirstOrDefaultAsync(ct);

        return BuildAliasBlock(name);
    }

    /// <summary>
    /// Alias block for a student's institute. Empty for B2C students, who have no institute at all.
    /// </summary>
    public static async Task<string> ResolveForStudentAsync(
        ApplicationDbContext context, int studentId, CancellationToken ct = default)
    {
        if (studentId <= 0)
            return string.Empty;

        var name = await context.Students.AsNoTracking()
            .Where(s => s.Id == studentId && s.Institute != null && s.Institute.MainAIPersonaId != null)
            .Select(s => s.Institute!.MainAIPersona!.Name)
            .FirstOrDefaultAsync(ct);

        return BuildAliasBlock(name);
    }
}
