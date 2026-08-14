namespace strAppersBackend.Models;

/// <summary>
/// Word limits for the institute hero headlines shown on Choose Your Squad. Bound from configuration,
/// mirroring <see cref="ProjectsInstituteMaxLengthFieldsOptions"/> so both limits are tunable without a
/// deploy and the frontend can read them from one endpoint instead of hard-coding them.
/// </summary>
public class InstituteHeadlineFieldsOptions
{
    /// <summary>Configuration path: <c>Institutes:HeadlineFields</c>.</summary>
    public const string SectionName = "Institutes:HeadlineFields";

    /// <summary>Primary headline: maximum number of words (default 10).</summary>
    public int PrimaryWords { get; set; } = 10;

    /// <summary>Secondary headline: maximum number of words (default 20).</summary>
    public int SecondaryWords { get; set; } = 20;
}
