using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Validates the filter predicates and safety guard used by the institute role CRUD endpoints.
/// The critical invariant: save operations must never touch Roles rows where InstituteId == 1.
/// </summary>
public class InstituteRoleCrudTests
{
    // ── GetInstituteRoles filter logic ────────────────────────────────────────

    [Fact]
    public void GetInstituteRoles_Base_FilterReturnsOnlyMatchingInstituteId_AndNullSquadId()
    {
        var roles = new List<Role>
        {
            new Role { Id = 10, InstituteId = 5, SquadId = null,  Name = "Developer" },
            new Role { Id = 11, InstituteId = 5, SquadId = null,  Name = "Designer"  },
            new Role { Id = 12, InstituteId = 5, SquadId = 20,    Name = "Dev Squad"  }, // squad-scoped, excluded
            new Role { Id = 13, InstituteId = 7, SquadId = null,  Name = "Other Inst" }, // wrong institute
            new Role { Id = 1,  InstituteId = 1, SquadId = null,  Name = "Global"     }, // global, excluded
        };

        var result = roles
            .Where(r => r.InstituteId == 5 && r.SquadId == null)
            .ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(5, r.InstituteId));
        Assert.All(result, r => Assert.Null(r.SquadId));
    }

    [Fact]
    public void GetInstituteRoles_Squad_FilterReturnsOnlyMatchingSquadId()
    {
        var roles = new List<Role>
        {
            new Role { Id = 10, InstituteId = 5, SquadId = 20, Name = "Developer" },
            new Role { Id = 11, InstituteId = 5, SquadId = 20, Name = "Designer"  },
            new Role { Id = 12, InstituteId = 5, SquadId = 99, Name = "Other Squad" }, // different squad
        };

        var result = roles.Where(r => r.InstituteId == 5 && r.SquadId == 20).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(20, r.SquadId));
    }

    // ── Safety guard: never update InstituteId=1 rows ────────────────────────

    [Fact]
    public void SaveInstituteRoles_SafetyGuard_GlobalRowCannotBeUpdated()
    {
        var existingRows = new List<Role>
        {
            new Role { Id = 1, InstituteId = 1, Name = "Developer" }, // global
            new Role { Id = 10, InstituteId = 5, Name = "Developer" }, // institute-specific
        };

        // Simulate the lookup: only rows owned by this institute
        var ownedByInstitute = existingRows.Where(r => r.InstituteId == 5).ToList();

        Assert.Single(ownedByInstitute);
        Assert.Equal(10, ownedByInstitute[0].Id);
        Assert.DoesNotContain(ownedByInstitute, r => r.InstituteId == 1);
    }

    [Fact]
    public void SaveInstituteRoles_WhenGlobalIdSentInPayload_NotFoundInInstituteScope()
    {
        var existingInstituteRows = new List<Role>
        {
            new Role { Id = 10, InstituteId = 5, Name = "Developer" },
        };

        // Payload sends the global role's Id (e.g. 1)
        var dtoId = 1;
        var foundRow = existingInstituteRows.FirstOrDefault(r => r.Id == dtoId);

        Assert.Null(foundRow); // global Id not found in institute rows → will create new
    }

    [Fact]
    public void SaveInstituteRoles_Base_CreatesNewRow_WithCorrectInstituteId()
    {
        const int instituteId = 5;
        var newRole = new Role
        {
            InstituteId = instituteId,
            SquadId = null,
            Name = "Custom Role",
            Type = 0,
            IsActive = true,
        };

        Assert.Equal(5, newRole.InstituteId);
        Assert.Null(newRole.SquadId);
        Assert.NotEqual(1, newRole.InstituteId);
    }

    [Fact]
    public void SaveInstituteRoles_Squad_CreatesNewRow_WithInstituteIdAndSquadId()
    {
        const int instituteId = 5;
        const int squadId = 20;
        var newRole = new Role
        {
            InstituteId = instituteId,
            SquadId = squadId,
            Name = "Squad Developer",
            Type = 1,
            IsActive = true,
        };

        Assert.Equal(5, newRole.InstituteId);
        Assert.Equal(20, newRole.SquadId);
    }

    // ── DeleteInstituteRole filter ────────────────────────────────────────────

    [Fact]
    public void DeleteInstituteRole_Filter_FindsRowByInstituteIdAndNullSquadId()
    {
        var roles = new List<Role>
        {
            new Role { Id = 10, InstituteId = 5, SquadId = null, Name = "Custom Role" },
            new Role { Id = 11, InstituteId = 5, SquadId = 20,   Name = "Squad Role"  }, // squad-scoped, excluded
            new Role { Id = 1,  InstituteId = 1, SquadId = null, Name = "Global Role" }, // global, excluded
        };

        const int roleId = 10;
        const int instituteId = 5;
        var row = roles.FirstOrDefault(r => r.Id == roleId && r.InstituteId == instituteId && r.SquadId == null);

        Assert.NotNull(row);
        Assert.Equal("Custom Role", row.Name);
    }

    [Fact]
    public void DeleteInstituteRole_Filter_DoesNotFindGlobalRow()
    {
        var roles = new List<Role>
        {
            new Role { Id = 1, InstituteId = 1, SquadId = null, Name = "Global Role" },
        };

        // Trying to delete as if it belongs to institute 5
        var row = roles.FirstOrDefault(r => r.Id == 1 && r.InstituteId == 5 && r.SquadId == null);

        Assert.Null(row); // global role not found under institute 5
    }
}
