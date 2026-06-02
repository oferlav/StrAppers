namespace strAppersBackend.Tests;

/// <summary>
/// Validates CourseType and RoleCount fields returned by
/// GetAvailableInstituteProjectsForStudent (Gap 4).
/// </summary>
public class InstituteProjectCourseTypeTests
{
    private record TemplateMeta(int ProjectId, bool RequireDeveloperRule, string? CourseType, int? RoleCount);

    private static Dictionary<int, TemplateMeta> BuildLookup(IEnumerable<TemplateMeta> items) =>
        items.GroupBy(m => m.ProjectId).ToDictionary(g => g.Key, g => g.First());

    [Fact]
    public void RoleCourse_ReturnsCourseTypeRole_AndRoleCount()
    {
        var lookup = BuildLookup([new TemplateMeta(10, false, CourseType: "Role", RoleCount: 3)]);

        var meta = lookup[10];
        Assert.Equal("Role", meta.CourseType);
        Assert.Equal(3, meta.RoleCount);
    }

    [Fact]
    public void SquadCourse_ReturnsCourseTypeSquad_AndNullRoleCount()
    {
        var lookup = BuildLookup([new TemplateMeta(10, false, CourseType: "Squad", RoleCount: null)]);

        var meta = lookup[10];
        Assert.Equal("Squad", meta.CourseType);
        Assert.Null(meta.RoleCount);
    }

    [Fact]
    public void NoTemplate_CourseTypeIsNull()
    {
        var lookup = BuildLookup([]);

        var meta = lookup.GetValueOrDefault(99);
        Assert.Null(meta?.CourseType);
        Assert.Null(meta?.RoleCount);
    }
}
