using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests the generated-file exclusion used before diff selection. Lock files and build output
/// otherwise dominate a student's diff and spend the character budget on machine-written code.
/// </summary>
public class GitHubSensorDiffFilterTests
{
    private static readonly string[] Patterns = MetricsController.DefaultExcludedPathPatterns;

    [Theory]
    [InlineData("package-lock.json")]
    [InlineData("frontend/package-lock.json")]
    [InlineData("apps/web/yarn.lock")]
    [InlineData("pnpm-lock.yaml")]
    public void LockFiles_AreExcluded(string path)
    {
        Assert.True(MetricsController.IsExcludedDiffPath(path, Patterns));
    }

    [Theory]
    [InlineData("dist/bundle.js")]
    [InlineData("src/dist/app.js")]
    [InlineData("build/index.html")]
    [InlineData("node_modules/react/index.js")]
    [InlineData("backend/bin/Debug/app.dll")]
    [InlineData("backend/obj/project.assets.json")]
    [InlineData("vendor/lib/thing.js")]
    public void GeneratedDirectories_AreExcluded(string path)
    {
        Assert.True(MetricsController.IsExcludedDiffPath(path, Patterns));
    }

    [Theory]
    [InlineData("assets/logo.png")]
    [InlineData("public/icon.ico")]
    [InlineData("docs/spec.pdf")]
    [InlineData("images/hero.jpeg")]
    [InlineData("src/vendor.min.js")]
    [InlineData("styles/site.min.css")]
    public void BinaryAndMinifiedAssets_AreExcluded(string path)
    {
        Assert.True(MetricsController.IsExcludedDiffPath(path, Patterns));
    }

    [Theory]
    [InlineData("src/Controllers/OrdersController.cs")]
    [InlineData("src/components/ReportTable.jsx")]
    [InlineData("package.json")]
    [InlineData("README.md")]
    [InlineData("src/services/reportService.ts")]
    [InlineData("Program.cs")]
    public void AuthoredSourceFiles_AreKept(string path)
    {
        Assert.False(MetricsController.IsExcludedDiffPath(path, Patterns));
    }

    /// <summary>
    /// "distribution/report.cs" contains "dist" as a prefix but is not the dist directory. Matching
    /// on a bare substring would wrongly discard the student's work.
    /// </summary>
    [Theory]
    [InlineData("distribution/report.cs")]
    [InlineData("src/buildings/Building.cs")]
    [InlineData("src/binary-tree/Node.cs")]
    public void SimilarlyNamedDirectories_AreNotExcluded(string path)
    {
        Assert.False(MetricsController.IsExcludedDiffPath(path, Patterns));
    }

    [Fact]
    public void WindowsSeparators_AreNormalised()
    {
        Assert.True(MetricsController.IsExcludedDiffPath(@"frontend\dist\bundle.js", Patterns));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPath_IsNotExcluded(string path)
    {
        Assert.False(MetricsController.IsExcludedDiffPath(path, Patterns));
    }

    [Fact]
    public void EmptyPatternList_ExcludesNothing()
    {
        Assert.False(MetricsController.IsExcludedDiffPath("package-lock.json", Array.Empty<string>()));
    }
}
