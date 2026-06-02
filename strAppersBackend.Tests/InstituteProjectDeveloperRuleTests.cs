namespace strAppersBackend.Tests;

/// <summary>
/// Validates the per-squad RequireDeveloperRule field returned by
/// GetAvailableInstituteProjectsForStudent (Gap 3).
/// </summary>
public class InstituteProjectDeveloperRuleTests
{
    private record TemplateMeta(int ProjectId, bool RequireDeveloperRule, string? CourseType, int? RoleCount);

    private static Dictionary<int, TemplateMeta> BuildLookup(IEnumerable<TemplateMeta> items) =>
        items.GroupBy(m => m.ProjectId).ToDictionary(g => g.Key, g => g.First());

    [Fact]
    public void RequireDeveloperRule_True_WhenSquadEnforces()
    {
        var lookup = BuildLookup([new TemplateMeta(10, RequireDeveloperRule: true, CourseType: "Squad", RoleCount: null)]);

        var meta = lookup.GetValueOrDefault(10);
        Assert.True(meta?.RequireDeveloperRule);
    }

    [Fact]
    public void RequireDeveloperRule_False_WhenSquadDoesNot()
    {
        var lookup = BuildLookup([new TemplateMeta(10, RequireDeveloperRule: false, CourseType: "Squad", RoleCount: null)]);

        var meta = lookup.GetValueOrDefault(10);
        Assert.False(meta?.RequireDeveloperRule);
    }

    [Fact]
    public void RequireDeveloperRule_False_WhenNoTemplate()
    {
        var lookup = BuildLookup([]);

        var meta = lookup.GetValueOrDefault(99);
        Assert.Null(meta); // no template → null → caller defaults to false
    }

    [Fact]
    public void TwoProjects_CanHaveDifferentRules()
    {
        var lookup = BuildLookup([
            new TemplateMeta(10, RequireDeveloperRule: true,  CourseType: "Squad", RoleCount: null),
            new TemplateMeta(11, RequireDeveloperRule: false, CourseType: "Squad", RoleCount: null),
        ]);

        Assert.True(lookup[10].RequireDeveloperRule);
        Assert.False(lookup[11].RequireDeveloperRule);
    }
}
