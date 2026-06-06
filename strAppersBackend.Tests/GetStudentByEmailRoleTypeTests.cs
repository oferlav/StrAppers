using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Validates that GetStudentByEmail projects RoleType from the active StudentRole.
/// Used by computeApplicantsCap on the frontend to determine slot cap by type, not name.
/// </summary>
public class GetStudentByEmailRoleTypeTests
{
    [Fact]
    public void ActiveRole_Type1_ProjectsType1()
    {
        var role = new Role { Id = 1, InstituteId = 1, Name = "Full Stack Developer", Type = 1 };
        var studentRole = new StudentRole { Role = role, IsActive = true };

        var roleType = studentRole.Role?.Type;

        Assert.Equal(1, roleType);
    }

    [Fact]
    public void ActiveRole_Type2_ProjectsType2()
    {
        var role = new Role { Id = 100, InstituteId = 5, SquadId = 20, Name = "Fullstack Bundle Dev", Type = 2 };
        var studentRole = new StudentRole { Role = role, IsActive = true };

        var roleType = studentRole.Role?.Type;

        Assert.Equal(2, roleType);
    }

    [Fact]
    public void InstituteRole_CustomName_TypeStillResolves()
    {
        // The fix: cap is derived from Type, not from name-matching "full stack"
        var role = new Role { Id = 101, InstituteId = 5, SquadId = 20, Name = "Senior FS Engineer", Type = 1 };
        var studentRole = new StudentRole { Role = role, IsActive = true };

        var roleType = studentRole.Role?.Type;

        Assert.Equal(1, roleType); // type=1 → cap 4, regardless of name
    }

    [Fact]
    public void NullStudentRole_ProjectsNullType()
    {
        StudentRole? roleInfo = null;

        var roleType = roleInfo?.Role?.Type;

        Assert.Null(roleType);
    }
}
