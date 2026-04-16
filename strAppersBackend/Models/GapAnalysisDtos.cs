using System.Text.Json.Serialization;

namespace strAppersBackend.Models;

/// <summary>Expected JSON shape from the Gap Analysis LLM (single response).</summary>
public class GapAnalysisLlmResult
{
    [JsonPropertyName("categories")]
    public List<GapAnalysisCategoryScore> Categories { get; set; } = new();

    /// <summary>Markdown narrative: sprint-global gap analysis.</summary>
    [JsonPropertyName("narrative")]
    public string Narrative { get; set; } = "";
}

public class GapAnalysisCategoryScore
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>0–100 completeness vs requirements for this category.</summary>
    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("rationale")]
    public string Rationale { get; set; } = "";
}
