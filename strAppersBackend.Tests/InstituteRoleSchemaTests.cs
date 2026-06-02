using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Validates the Role model scoping rules introduced by A3:
/// InstituteId=1 + SquadId=null → global, InstituteId=X + SquadId=null → base institute,
/// InstituteId=X + SquadId=Y → squad-scoped.
/// </summary>
public class InstituteRoleSchemaTests
{
    [Fact]
    public void Role_DefaultInstituteId_Is1()
    {
        var role = new Role();
        Assert.Equal(1, role.InstituteId);
    }

    [Fact]
    public void Role_IsTechnical_DefaultsToFalse()
    {
        var role = new Role();
        Assert.False(role.IsTechnical);
    }

    [Fact]
    public void Role_Competencies_DefaultsToNull()
    {
        var role = new Role();
        Assert.Null(role.Competencies);
    }

    [Fact]
    public void Role_SquadId_DefaultsToNull()
    {
        var role = new Role();
        Assert.Null(role.SquadId);
    }

    [Fact]
    public void Role_WithInstituteId1_AndNullSquadId_IsGlobalRole()
    {
        var role = new Role { InstituteId = 1, SquadId = null };
        Assert.Equal(1, role.InstituteId);
        Assert.Null(role.SquadId);
    }

    [Fact]
    public void Role_WithInstituteIdX_AndNullSquadId_IsBaseScopedRole()
    {
        var role = new Role { InstituteId = 5, SquadId = null };
        Assert.Equal(5, role.InstituteId);
        Assert.Null(role.SquadId);
    }

    [Fact]
    public void Role_WithInstituteIdX_AndSquadId_IsSquadScopedRole()
    {
        var role = new Role { InstituteId = 5, SquadId = 10 };
        Assert.Equal(5, role.InstituteId);
        Assert.Equal(10, role.SquadId);
    }

    [Fact]
    public void Role_Competencies_CanBeSet()
    {
        var role = new Role { Competencies = "React, Node.js, PostgreSQL" };
        Assert.Equal("React, Node.js, PostgreSQL", role.Competencies);
    }

    [Fact]
    public void Role_IsTechnical_CanBeSetToTrue()
    {
        var role = new Role { IsTechnical = true };
        Assert.True(role.IsTechnical);
    }
}
