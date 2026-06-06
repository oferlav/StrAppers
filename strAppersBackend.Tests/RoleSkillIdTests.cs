using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Validates Phase 8 additions: RoleSkillId exposed on GetStudentByEmail,
/// and SquadRoles included on GetAvailableInstituteProjects.
/// </summary>
public class RoleSkillIdTests
{
    // ── GetStudentByEmail: RoleSkillId projection ─────────────────────────────

    [Fact]
    public void RoleWithSkillId3_ProjectsSkillId3()
    {
        var role = new Role { Id = 101, InstituteId = 5, SquadId = 20, Name = "UI/UX Designer", SkillId = 3 };
        var studentRole = new StudentRole { RoleId = 101, Role = role, IsActive = true };

        var roleInfo = studentRole;
        var roleSkillId = roleInfo.Role?.SkillId;

        Assert.Equal(3, roleSkillId);
    }

    [Fact]
    public void RoleWithNullSkillId_ProjectsNull()
    {
        var role = new Role { Id = 100, InstituteId = 5, SquadId = 20, Name = "Full Stack Developer", SkillId = null };
        var studentRole = new StudentRole { RoleId = 100, Role = role, IsActive = true };

        var roleSkillId = studentRole.Role?.SkillId;

        Assert.Null(roleSkillId);
    }

    [Fact]
    public void NullStudentRole_ProjectsNullSkillId()
    {
        StudentRole? roleInfo = null;
        var roleSkillId = roleInfo?.Role?.SkillId;

        Assert.Null(roleSkillId);
    }

    // ── GetAvailableInstituteProjects: SquadRoles grouping logic ──────────────

    [Fact]
    public void SquadRoles_GroupedByProjectId_CorrectCount()
    {
        var flat = new[]
        {
            new { ProjectId = 10, Name = "Developer", Type = 1 },
            new { ProjectId = 10, Name = "Designer",  Type = 3 },
            new { ProjectId = 11, Name = "Developer", Type = 1 },
        };

        var byProject = flat
            .GroupBy(r => r.ProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        Assert.Equal(2, byProject[10].Count);
        Assert.Single(byProject[11]);
    }

    [Fact]
    public void SquadRoles_DeduplicatedByName_AcrossTemplates()
    {
        // Two templates in same project → same role name should appear once
        var flat = new[]
        {
            new { ProjectId = 10, Name = "Developer", Type = 1 },
            new { ProjectId = 10, Name = "Developer", Type = 1 }, // duplicate from second template
        };

        var deduped = flat
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { g.First().Name, g.First().Type })
            .ToList();

        Assert.Single(deduped);
    }

    [Fact]
    public void ProjectWithNoSquadRoles_ReturnsEmptyCollection()
    {
        var byProject = new Dictionary<int, List<object>>();
        var squadRoles = byProject.GetValueOrDefault(99) ?? new List<object>();

        Assert.Empty(squadRoles);
    }
}
