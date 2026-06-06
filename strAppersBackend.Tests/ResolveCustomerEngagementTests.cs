using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Validates the simplified CE resolution after A3 Phase 6:
/// CustomerEngagement is read directly from Role — no InstituteSquadRole name-match fallback.
/// </summary>
public class ResolveCustomerEngagementTests
{
    private static bool Resolve(Role? role) => role?.CustomerEngagement == true;

    [Fact]
    public void InstituteRole_CeTrue_ReturnsTrue()
    {
        var role = new Role { Id = 100, InstituteId = 5, SquadId = 20, Name = "UI/UX Designer", CustomerEngagement = true };
        Assert.True(Resolve(role));
    }

    [Fact]
    public void InstituteRole_CeFalse_ReturnsFalse()
    {
        var role = new Role { Id = 101, InstituteId = 5, SquadId = 20, Name = "Full Stack Developer", CustomerEngagement = false };
        Assert.False(Resolve(role));
    }

    [Fact]
    public void B2cRole_CeTrue_ReturnsTrue()
    {
        var role = new Role { Id = 2, InstituteId = 1, SquadId = null, Name = "Product Manager", CustomerEngagement = true };
        Assert.True(Resolve(role));
    }

    [Fact]
    public void B2cRole_CeFalse_ReturnsFalse()
    {
        var role = new Role { Id = 1, InstituteId = 1, SquadId = null, Name = "Full Stack Developer", CustomerEngagement = false };
        Assert.False(Resolve(role));
    }

    [Fact]
    public void NullRole_ReturnsFalse()
    {
        Assert.False(Resolve(null));
    }
}
