namespace strAppersBackend.Models;

/// <summary>Word limits for project design header fields (Institute "General" tab). Bound from configuration.</summary>
public class ProjectsInstituteMaxLengthFieldsOptions
{
    /// <summary>Configuration path: <c>ProjectsInstitute:MaxLengthFields</c>.</summary>
    public const string SectionName = "ProjectsInstitute:MaxLengthFields";

    /// <summary>Project name: maximum number of words (default 1).</summary>
    public int ProjectNameWords { get; set; } = 1;

    /// <summary>Mission: maximum number of words (default 45).</summary>
    public int MissionWords { get; set; } = 45;

    /// <summary>One-liner: maximum number of words (default 8).</summary>
    public int OneLinerWords { get; set; } = 8;

    /// <summary>Short brief: maximum number of words (default 150).</summary>
    public int ShortBriefWords { get; set; } = 150;
}
