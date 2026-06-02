using System.Text.Json.Serialization;

namespace strAppersBackend.Models;

/// <summary>Inbound payload from the employer plugin. Contains an aggregated, anonymised Skillset — no raw employee data, no credentials.</summary>
public class EmployerSkillsetSubmitRequest
{
    /// <summary>Stable, anonymised company slug — never a real company name in the payload.</summary>
    [JsonPropertyName("companyIdentifier")]
    public string CompanyIdentifier { get; set; } = string.Empty;

    /// <summary>e.g. "Senior Backend Engineer"</summary>
    [JsonPropertyName("roleName")]
    public string RoleName { get; set; } = string.Empty;

    [JsonPropertyName("assessedPeriod")]
    public AssessedPeriod? AssessedPeriod { get; set; }

    /// <summary>Which normalised data buckets were used to produce this Skillset.</summary>
    [JsonPropertyName("dataSourcesUsed")]
    public List<string> DataSourcesUsed { get; set; } = new();

    /// <summary>Number of employees assessed. No names or IDs leave the employer environment.</summary>
    [JsonPropertyName("employeeSampleSize")]
    public int EmployeeSampleSize { get; set; }

    [JsonPropertyName("skills")]
    public List<EmployerSkillData> Skills { get; set; } = new();

    [JsonPropertyName("metadata")]
    public SkillsetMetadata? Metadata { get; set; }
}

public class AssessedPeriod
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;
}

/// <summary>
/// A single assessed dimension of the Skillset.
/// Scores are aggregated across all sampled employees — individual scores stay in the employer environment.
/// </summary>
public class EmployerSkillData
{
    /// <summary>Human-readable metric name. e.g. "Task Delivery", "Communication Quality".</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Normalised data bucket this agent drew from: tasks_and_sla | communication | design_content | artifacts</summary>
    [JsonPropertyName("metricCategory")]
    public string MetricCategory { get; set; } = string.Empty;

    /// <summary>Specific tools whose data was consumed: jira, slack, github, confluence, etc.</summary>
    [JsonPropertyName("dataSources")]
    public List<string> DataSources { get; set; } = new();

    /// <summary>Aggregated 0–100 score across all sampled employees for this dimension.</summary>
    [JsonPropertyName("aggregatedScore")]
    public double AggregatedScore { get; set; }

    /// <summary>Employer's manual score for this dimension (human rating anchor). Used to calibrate matching.</summary>
    [JsonPropertyName("humanRatingAnchor")]
    public double? HumanRatingAnchor { get; set; }

    /// <summary>Relative importance multiplier. 1.0 = baseline; 2.0 = twice as important.</summary>
    [JsonPropertyName("weight")]
    public double Weight { get; set; } = 1.0;

    /// <summary>0–1 agent confidence level based on data completeness.</summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>Brief narrative from the assessment agent for this dimension.</summary>
    [JsonPropertyName("rationale")]
    public string Rationale { get; set; } = string.Empty;
}

public class SkillsetMetadata
{
    [JsonPropertyName("pluginVersion")]
    public string? PluginVersion { get; set; }

    [JsonPropertyName("generatedAt")]
    public string? GeneratedAt { get; set; }

    /// <summary>Tool name for the connector that fetched the data: skill-in-connector, jira-mcp, etc.</summary>
    [JsonPropertyName("connectorType")]
    public string? ConnectorType { get; set; }
}
