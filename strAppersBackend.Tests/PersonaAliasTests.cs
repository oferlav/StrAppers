using strAppersBackend.Controllers;
using strAppersBackend.Utilities;

namespace strAppersBackend.Tests;

/// <summary>
/// The alias block renames the simulated stakeholder for the LLM instead of rewriting the tuned
/// prompt prose that mentions the "AI Customer".
///
/// The regression that matters most is the no-op path: every institute that has not selected a
/// persona — and every B2C student, who has no institute at all — must get a system prompt that is
/// byte-identical to what it was before this existed. These tests assert that directly, including
/// reference equality, so "unchanged" cannot quietly become "equal but reallocated and reformatted".
/// </summary>
public class PersonaAliasTests
{
    private const string SystemPrompt = "You are a mentor.\n\nRules:\n- Be concise.";

    // ── No-op path: nothing to rename ────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t ")]
    public void NoPersona_ProducesNoAliasBlock(string? personaName)
    {
        Assert.Equal(string.Empty, PersonaAlias.BuildAliasBlock(personaName));
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("customer")]
    [InlineData("CUSTOMER")]
    [InlineData("  Customer  ")]
    public void PersonaAlreadyCalledCustomer_ProducesNoAliasBlock(string personaName)
    {
        // Renaming "Customer" to "Customer" is pure noise in every prompt on the platform.
        Assert.Equal(string.Empty, PersonaAlias.BuildAliasBlock(personaName));
    }

    [Fact]
    public void EmptyAlias_LeavesThePromptByteIdentical()
    {
        var result = PersonaAlias.Prepend(string.Empty, SystemPrompt);

        Assert.Equal(SystemPrompt, result);
        // Reference equality: the no-op path must not even reallocate, so no reformatting can creep in.
        Assert.Same(SystemPrompt, result);
    }

    [Fact]
    public void InstituteWithoutPersona_EndToEnd_LeavesThePromptUnchanged()
    {
        var result = PersonaAlias.Prepend(PersonaAlias.BuildAliasBlock(null), SystemPrompt);

        Assert.Same(SystemPrompt, result);
    }

    // ── Active path: a persona is selected ───────────────────────────────────

    [Fact]
    public void NamedPersona_BlockNamesBothTheNewAndTheOldTerm()
    {
        var block = PersonaAlias.BuildAliasBlock("Professor");

        Assert.Contains("AI Professor", block);
        Assert.Contains("AI Customer", block);          // it must say what is being renamed
        Assert.Contains("=== PERSONA NAMING ===", block);
        Assert.Contains("=== END PERSONA NAMING ===", block);
    }

    [Fact]
    public void PersonaName_IsTrimmed()
    {
        var block = PersonaAlias.BuildAliasBlock("  Professor  ");

        Assert.Contains("AI Professor", block);
        Assert.DoesNotContain("AI   Professor", block);
    }

    [Fact]
    public void MultiWordPersonaName_IsUsedWhole()
    {
        var block = PersonaAlias.BuildAliasBlock("Head of Product");

        Assert.Contains("AI Head of Product", block);
    }

    [Fact]
    public void Alias_GoesAtTheTop_SoItFramesEverythingAfterIt()
    {
        var result = PersonaAlias.Prepend(PersonaAlias.BuildAliasBlock("Professor"), SystemPrompt);

        Assert.StartsWith("=== PERSONA NAMING ===", result);
        Assert.EndsWith(SystemPrompt, result);
    }

    [Fact]
    public void Alias_IsAppliedExactlyOnce()
    {
        // Guards against a call site prepending twice (e.g. a helper applied inside and outside).
        var result = PersonaAlias.Prepend(PersonaAlias.BuildAliasBlock("Professor"), SystemPrompt);

        Assert.Equal(1, CountOccurrences(result, "=== PERSONA NAMING ==="));
        Assert.Equal(1, CountOccurrences(result, "=== END PERSONA NAMING ==="));
    }

    [Fact]
    public void OriginalPromptSurvivesVerbatim()
    {
        // The whole premise: the existing prompt text is never edited, only preceded.
        var result = PersonaAlias.Prepend(PersonaAlias.BuildAliasBlock("Professor"), SystemPrompt);

        Assert.Contains(SystemPrompt, result);
    }

    [Fact]
    public void AliasBlock_StaysSmall()
    {
        // Cost control: this is prepended to every mentor/metrics call. A block that grows into
        // paragraphs would start crowding long prompts against the model's context limit.
        var block = PersonaAlias.BuildAliasBlock("Professor");

        Assert.True(block.Length < 500, $"Alias block grew to {block.Length} chars; keep it terse.");
    }

    private static int CountOccurrences(string haystack, string needle) =>
        haystack.Split(needle).Length - 1;
}

/// <summary>
/// The assessment report's Data Sources list. It is built server-side from SensorCatalog, so it
/// needs its own rename to agree with the Data sensors form the staff sees next to it.
///
/// The catalog itself is deliberately left alone — those same labels feed the LLM prompt through
/// AppendEvidenceScopeHeader, where PersonaAlias already renames them.
/// </summary>
public class ReportSensorLabelTests
{
    private static readonly string[] Sensors =
    {
        "AI Customer Chat", "AI Mentor Chat", "Codebase & Github", "Figma Design",
    };

    [Fact]
    public void NamedPersona_RenamesOnlyTheAiChatSensor()
    {
        var result = MetricsController.ApplyPersonaToSensorLabels(Sensors, "Professor");

        Assert.Equal(
            new[] { "AI Professor Chat", "AI Mentor Chat", "Codebase & Github", "Figma Design" },
            result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Customer")]
    [InlineData("customer")]
    public void NoPersonaOrCustomer_LeavesEveryLabelUntouched(string? personaName)
    {
        var result = MetricsController.ApplyPersonaToSensorLabels(Sensors, personaName);

        Assert.Equal(Sensors, result);
        // Untouched means the same list instance — no rebuild, so no ordering or casing can drift.
        Assert.Same(Sensors, result);
    }

    [Fact]
    public void MentorChatSensor_IsNeverRenamed()
    {
        // "AI Mentor Chat" is a different platform entity and must survive verbatim.
        var result = MetricsController.ApplyPersonaToSensorLabels(Sensors, "Customer Success Lead");

        Assert.Contains("AI Mentor Chat", result);
        Assert.Contains("AI Customer Success Lead Chat", result);
    }

    [Fact]
    public void EmptySensorList_StaysEmpty()
    {
        // Summary rows report no sensors at all.
        Assert.Empty(MetricsController.ApplyPersonaToSensorLabels(Array.Empty<string>(), "Professor"));
    }
}
