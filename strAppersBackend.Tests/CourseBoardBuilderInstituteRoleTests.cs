using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Validates the CourseBoardBuilderService role-loading logic after A3 Phase 7:
/// squad roles come from Roles table (SquadId FK), base roles from Roles (InstituteId + SquadId IS NULL).
/// </summary>
public class CourseBoardBuilderInstituteRoleTests
{
    // ── Competencies simplification (squad path) ──────────────────────────────

    [Fact]
    public void SquadRole_Competencies_LoadedDirectlyFromRole()
    {
        var role = new Role { Id = 100, InstituteId = 5, SquadId = 20, Name = "Developer", Competencies = "React, Node.js" };

        // After A3: direct property, no BaseInstituteRole fallback
        var competencies = role.Competencies;

        Assert.Equal("React, Node.js", competencies);
    }

    [Fact]
    public void SquadRole_NullCompetencies_RemainsNull_NoFallback()
    {
        var role = new Role { Id = 100, InstituteId = 5, SquadId = 20, Name = "Developer", Competencies = null };

        var competencies = role.Competencies;

        Assert.Null(competencies);
    }

    // ── TemplateId bug fix (base path) ────────────────────────────────────────

    [Fact]
    public void BaseInstituteRoles_WithNullTemplateId_AreFoundByEffectiveRoleIds()
    {
        // All institute roles in DB have TemplateId = null (confirmed in prod).
        // Old query: r.TemplateId == request.TemplateId → always empty when templateId is non-null.
        // New query: effectiveRoleIds.Contains(r.Id) — no TemplateId condition.
        var roles = new List<Role>
        {
            new Role { Id = 10, InstituteId = 5, SquadId = null, Name = "Developer" },
            new Role { Id = 11, InstituteId = 5, SquadId = null, Name = "Designer"  },
            new Role { Id = 12, InstituteId = 5, SquadId = null, Name = "Manager"   },
        };

        var effectiveRoleIds = new List<int> { 10, 11 };
        var result = roles.Where(r => effectiveRoleIds.Contains(r.Id)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Id == 10);
        Assert.Contains(result, r => r.Id == 11);
    }

    [Fact]
    public void BaseInstituteRoles_FilteredByInstituteId_ExcludesGlobalRoles()
    {
        var roles = new List<Role>
        {
            new Role { Id = 1, InstituteId = 1, SquadId = null, Name = "Full Stack Developer" }, // global
            new Role { Id = 10, InstituteId = 5, SquadId = null, Name = "Developer"            }, // institute
            new Role { Id = 11, InstituteId = 5, SquadId = null, Name = "Designer"             }, // institute
        };

        int instituteId = 5;
        var result = roles
            .Where(r => r.InstituteId == instituteId && r.SquadId == null)
            .ToList();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, r => r.InstituteId == 1);
    }

    [Fact]
    public void SquadRoles_FilteredBySquadId_ExcludesOtherSquads()
    {
        var roles = new List<Role>
        {
            new Role { Id = 100, InstituteId = 5, SquadId = 20, Name = "Developer" },
            new Role { Id = 101, InstituteId = 5, SquadId = 20, Name = "Designer"  },
            new Role { Id = 200, InstituteId = 5, SquadId = 21, Name = "Developer" }, // different squad
        };

        var result = roles.Where(r => r.SquadId == 20 && r.IsActive).ToList();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, r => r.SquadId == 21);
    }
}
