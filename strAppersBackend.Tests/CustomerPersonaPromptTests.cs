using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// System-prompt precedence for the student-facing AI chat after personas were introduced.
///
/// The regression these guard against: institutes that never select a persona — and every B2C
/// student, who has no institute at all — must keep getting the configured
/// PromptConfig:Customer:SystemPrompt exactly as before. A persona row is an override, never a
/// precondition.
/// </summary>
public class CustomerPersonaPromptTests
{
    private const string ConfiguredPrompt = "You are the 'AI Customer,' the non-technical founder…";
    private const string PersonaPrompt = "You are the 'AI Professor,' the course instructor…";

    [Fact]
    public void PersonaPrompt_Wins_OverConfiguredPrompt()
    {
        var resolved = CustomerController.ResolveCustomerSystemPrompt(PersonaPrompt, ConfiguredPrompt);

        Assert.Equal(PersonaPrompt, resolved);
    }

    [Fact]
    public void NoPersona_FallsBackToConfiguredPrompt()
    {
        var resolved = CustomerController.ResolveCustomerSystemPrompt(null, ConfiguredPrompt);

        Assert.Equal(ConfiguredPrompt, resolved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t ")]
    public void BlankPersonaPrompt_FallsBackToConfiguredPrompt(string personaPrompt)
    {
        // A persona row created with an empty Prompt must not send the model an empty system message.
        var resolved = CustomerController.ResolveCustomerSystemPrompt(personaPrompt, ConfiguredPrompt);

        Assert.Equal(ConfiguredPrompt, resolved);
    }

    [Fact]
    public void PersonaPrompt_IsTrimmed()
    {
        var resolved = CustomerController.ResolveCustomerSystemPrompt($"\n  {PersonaPrompt}  \n", ConfiguredPrompt);

        Assert.Equal(PersonaPrompt, resolved);
    }

    [Fact]
    public void NeitherSet_FallsBackToTheBuiltInDefault()
    {
        var resolved = CustomerController.ResolveCustomerSystemPrompt(null, null);

        Assert.Equal(CustomerController.DefaultCustomerSystemPrompt, resolved);
    }

    [Fact]
    public void BlankConfiguredPrompt_FallsBackToTheBuiltInDefault()
    {
        // Same behaviour as before personas existed: blank config → the generic assistant prompt.
        var resolved = CustomerController.ResolveCustomerSystemPrompt(null, "   ");

        Assert.Equal(CustomerController.DefaultCustomerSystemPrompt, resolved);
    }
}

/// <summary>
/// Persona label fallback. Every UI label that used to read "Customer" now reads this, so an
/// institute with no persona — or any lookup that comes back empty — must still produce "Customer"
/// rather than a blank tab.
/// </summary>
public class PersonaLabelTests
{
    [Fact]
    public void NamedPersona_IsUsedVerbatim()
    {
        Assert.Equal("Professor", PersonasController.ResolvePersonaLabel("Professor"));
    }

    [Fact]
    public void PersonaName_IsTrimmed()
    {
        Assert.Equal("Professor", PersonasController.ResolvePersonaLabel("  Professor  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingOrBlankName_FallsBackToCustomer(string? personaName)
    {
        Assert.Equal("Customer", PersonasController.ResolvePersonaLabel(personaName));
        Assert.Equal(PersonasController.DefaultPersonaName, PersonasController.ResolvePersonaLabel(personaName));
    }
}
