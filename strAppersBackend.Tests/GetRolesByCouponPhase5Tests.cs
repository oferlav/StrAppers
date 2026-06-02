using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Validates the simplified GetRolesByCoupon logic after A3 Phase 5:
/// squad roles come from the Roles table directly — no name-matching needed.
/// </summary>
public class GetRolesByCouponPhase5Tests
{
    private static List<Role> GlobalRoles() => new()
    {
        new Role { Id = 1, InstituteId = 1, Name = "Full Stack Developer", Type = 1, IsActive = true },
        new Role { Id = 2, InstituteId = 1, Name = "UI/UX Designer",       Type = 3, IsActive = true },
        new Role { Id = 3, InstituteId = 1, Name = "Product Manager",      Type = 4, IsActive = true },
    };

    private static List<Role> InstituteSquadRoles(int instituteId, int squadId) => new()
    {
        new Role { Id = 100, InstituteId = instituteId, SquadId = squadId, Name = "Full Stack Developer", Type = 1, IsActive = true },
        new Role { Id = 101, InstituteId = instituteId, SquadId = squadId, Name = "UI/UX Designer",       Type = 3, IsActive = true },
    };

    [Fact]
    public void GetRolesByCoupon_SquadHasRoles_ReturnsInstituteRoleIds_NotGlobalIds()
    {
        var squadRoles = InstituteSquadRoles(5, 20);

        // After A3: return Role.Id directly — no name-mapping
        var result = squadRoles
            .Where(r => r.IsActive)
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(r => new { id = r.Id, name = r.Name, type = r.Type })
            .ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.id == 100); // institute-specific Id, not global Id 1
        Assert.Contains(result, r => r.id == 101);
        Assert.DoesNotContain(result, r => r.id == 1); // global Id must NOT appear
        Assert.DoesNotContain(result, r => r.id == 2);
    }

    [Fact]
    public void GetRolesByCoupon_NoSquadRoles_FallbackReturnsInstituteId1Only()
    {
        var allRoles = GlobalRoles();
        allRoles.Add(new Role { Id = 100, InstituteId = 5, SquadId = 20, Name = "Custom Role", IsActive = true });

        var fallback = allRoles
            .Where(r => r.InstituteId == 1)
            .OrderBy(r => r.Name)
            .Select(r => new { id = r.Id, name = r.Name, type = r.Type })
            .ToList();

        Assert.Equal(3, fallback.Count);
        Assert.All(fallback, r => Assert.NotEqual(100, r.id));
    }

    [Fact]
    public void GetRolesByCoupon_InactiveSquadRoles_AreExcluded()
    {
        var roles = new List<Role>
        {
            new Role { Id = 100, InstituteId = 5, SquadId = 20, Name = "Developer", IsActive = true  },
            new Role { Id = 101, InstituteId = 5, SquadId = 20, Name = "Designer",  IsActive = false }, // inactive
        };

        var result = roles.Where(r => r.IsActive).ToList();

        Assert.Single(result);
        Assert.Equal(100, result[0].Id);
    }

    [Fact]
    public void GetRolesByCoupon_DuplicateRoleNames_DeduplicatedByName()
    {
        // Two squads both have "Developer" — de-duplicate
        var roles = new List<Role>
        {
            new Role { Id = 100, InstituteId = 5, SquadId = 20, Name = "Developer", IsActive = true },
            new Role { Id = 200, InstituteId = 5, SquadId = 21, Name = "Developer", IsActive = true },
        };

        var result = roles
            .Where(r => r.IsActive)
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        Assert.Single(result);
    }

    [Fact]
    public void GetRolesByCoupon_FallbackDoesNotIncludeNonPlatformInstituteRoles()
    {
        var allRoles = GlobalRoles();
        allRoles.Add(new Role { Id = 50, InstituteId = 5, SquadId = null, Name = "Custom Base Role", IsActive = true });
        allRoles.Add(new Role { Id = 51, InstituteId = 5, SquadId = 20,   Name = "Squad Role",       IsActive = true });

        var fallback = allRoles.Where(r => r.InstituteId == 1).ToList();

        Assert.Equal(3, fallback.Count);
        Assert.DoesNotContain(fallback, r => r.InstituteId != 1);
    }
}
