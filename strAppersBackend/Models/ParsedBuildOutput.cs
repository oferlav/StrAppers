namespace strAppersBackend.Models;

/// <summary>
/// Represents parsed build output from AI analysis
/// </summary>
public class ParsedBuildOutput
{
    /// <summary>
    /// File path where the error occurred (if available)
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// Line number where the error occurred (if available)
    /// </summary>
    public int? Line { get; set; }

    /// <summary>
    /// Stack trace of the error (if available)
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Human-readable summary of the error
    /// </summary>
    public string? LatestErrorSummary { get; set; }
}
