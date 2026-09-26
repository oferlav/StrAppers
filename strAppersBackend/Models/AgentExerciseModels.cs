namespace strAppersBackend.Models;

// Agent exercises: API-level grading of student AI agents (see Services/GoogleProxy/AgentGrader.cs).
// Structure is relational; content (limits, world data, expectations, reports) is JSON text so a future
// scenario builder can evolve its shape without migrations.

/// <summary>The contract students build against (for example Dinner Scout): endpoint and limits.</summary>
public class AgentExercise
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EndpointPath { get; set; } = string.Empty;
    /// <summary>JSON: maxLatencyMs, maxDetailsCalls, minGeminiCalls, maxWhyChars.</summary>
    public string? LimitsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AgentRequirement> Requirements { get; set; } = new List<AgentRequirement>();
}

/// <summary>A requirement students must discover, and the persona who reveals it (null = platform rule).</summary>
public class AgentRequirement
{
    public int Id { get; set; }
    public int ExerciseId { get; set; }
    public AgentExercise Exercise { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int? PersonaId { get; set; }
    public Persona? Persona { get; set; }
}

/// <summary>Simulated Maps/Places data the proxy serves in fixture mode.</summary>
public class AgentWorld
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DataJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A versioned bundle of scenarios on one exercise and world. Published sets are frozen; changes get a new version.</summary>
public class AgentScenarioSet
{
    public const string StatusDraft = "draft";
    public const string StatusPublished = "published";
    public const string StatusArchived = "archived";

    public int Id { get; set; }
    public int ExerciseId { get; set; }
    public AgentExercise Exercise { get; set; } = null!;
    public int WorldId { get; set; }
    public AgentWorld World { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string Status { get; set; } = StatusDraft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }

    public ICollection<AgentScenario> Scenarios { get; set; } = new List<AgentScenario>();
}

/// <summary>One test case: the guest request, optional position, and expectations (JSON, tagged with requirement codes).</summary>
public class AgentScenario
{
    public int Id { get; set; }
    public int ScenarioSetId { get; set; }
    public AgentScenarioSet ScenarioSet { get; set; } = null!;
    public string Key { get; set; } = string.Empty;
    public string Request { get; set; } = string.Empty;
    public double? PositionLat { get; set; }
    public double? PositionLng { get; set; }
    public string ExpectationsJson { get; set; } = "{}";
    public int SortOrder { get; set; }
}

/// <summary>Links an institute project to a scenario set, as its practice set or its graded set (at most one of each).</summary>
public class ProjectAgentScenarioSet
{
    public const string PurposePractice = "practice";
    public const string PurposeGraded = "graded";

    public int Id { get; set; }
    public int InstituteProjectId { get; set; }
    public InstituteProject InstituteProject { get; set; } = null!;
    public int ScenarioSetId { get; set; }
    public AgentScenarioSet ScenarioSet { get; set; } = null!;
    public string Purpose { get; set; } = PurposeGraded;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A saved grading. No FK on BoardId so grades survive board deletion.</summary>
public class AgentGradingReport
{
    public int Id { get; set; }
    public string GradingId { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public int ScenarioSetId { get; set; }
    public AgentScenarioSet ScenarioSet { get; set; } = null!;
    public string Status { get; set; } = "running";
    public int? Score { get; set; }
    public bool? CriticalFailure { get; set; }
    public string? ReportJson { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}
