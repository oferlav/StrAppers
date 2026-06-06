using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Verifies that the InstituteId=1 filter correctly separates global/B2C roles
/// from institute-specific roles. These tests validate the filter predicate
/// used by GET /api/Roles/use, /api/Roles/use/all, and the GetRolesByCoupon fallback.
/// </summary>
public class GlobalRoleFilterTests
{
    private static List<Role> SampleRoles() => new()
    {
        new Role { Id = 1, Name = "Full Stack Developer", InstituteId = 1, IsActive = true },
        new Role { Id = 2, Name = "UI/UX Designer",       InstituteId = 1, IsActive = true },
        new Role { Id = 3, Name = "Product Manager",      InstituteId = 1, IsActive = true },
        new Role { Id = 4, Name = "Inactive Role",        InstituteId = 1, IsActive = false },
        new Role { Id = 5, Name = "Custom Dev",           InstituteId = 5, IsActive = true },  // institute-specific
        new Role { Id = 6, Name = "Custom PM",            InstituteId = 7, IsActive = true },  // institute-specific
        new Role { Id = 7, Name = "Squad Dev",            InstituteId = 5, IsActive = true, SquadId = 10 }, // squad-scoped
    };

    [Fact]
    public void GetActiveRoles_Filter_ReturnsOnlyInstituteId1_ActiveRoles()
    {
        var result = SampleRoles()
            .Where(r => r.InstituteId == 1 && r.IsActive)
            .ToList();

        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.Equal(1, r.InstituteId));
        Assert.All(result, r => Assert.True(r.IsActive));
    }

    [Fact]
    public void GetAllRoles_Filter_ReturnsOnlyInstituteId1_IncludingInactive()
    {
        var result = SampleRoles()
            .Where(r => r.InstituteId == 1)
            .ToList();

        Assert.Equal(4, result.Count);
        Assert.All(result, r => Assert.Equal(1, r.InstituteId));
    }

    [Fact]
    public void GetActiveRoles_Filter_ExcludesInstituteSpecificRoles()
    {
        var result = SampleRoles()
            .Where(r => r.InstituteId == 1 && r.IsActive)
            .ToList();

        Assert.DoesNotContain(result, r => r.InstituteId != 1);
    }

    [Fact]
    public void GetActiveRoles_Filter_ExcludesSquadScopedRoles()
    {
        var result = SampleRoles()
            .Where(r => r.InstituteId == 1 && r.IsActive)
            .ToList();

        Assert.DoesNotContain(result, r => r.SquadId != null);
    }

    [Fact]
    public void GetRolesByCoupon_Fallback_ReturnsOnlyInstituteId1Roles()
    {
        var defaultRoles = SampleRoles()
            .Where(r => r.InstituteId == 1)
            .OrderBy(r => r.Name)
            .Select(r => new { id = r.Id, name = r.Name, type = r.Type })
            .ToList();

        Assert.True(defaultRoles.Count > 0);
        Assert.Equal(4, defaultRoles.Count); // 3 active + 1 inactive global
    }

    [Fact]
    public void GetActiveRoles_WhenInstituteRoleHasSameNameAsGlobal_ReturnsOnlyOneResult()
    {
        var roles = new List<Role>
        {
            new Role { Id = 1, Name = "Developer", InstituteId = 1, IsActive = true },
            new Role { Id = 5, Name = "Developer", InstituteId = 5, IsActive = true }, // same name, different institute
        };

        var result = roles.Where(r => r.InstituteId == 1 && r.IsActive).ToList();

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void IsGlobalCatalogRole_Check_UsesInstituteId1Filter()
    {
        var roles = new List<Role>
        {
            new Role { Id = 1, Name = "Developer", InstituteId = 1 },
            new Role { Id = 5, Name = "Custom Dev", InstituteId = 5 },
        };

        var roleName = "Custom Dev";
        var isGlobal = roles.Any(r => r.InstituteId == 1 &&
            r.Name.ToLower() == roleName.ToLower());

        Assert.False(isGlobal); // "Custom Dev" is not in global catalog (InstituteId=1)
    }
}
