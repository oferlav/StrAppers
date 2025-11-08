using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

public class ProjectsIDE
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("project_id")]
    public int ProjectId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("chunk_id")]
    public string ChunkId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("chunk_type")]
    public string ChunkType { get; set; } = string.Empty;

    [Column("chunk_description")]
    public string? ChunkDescription { get; set; }

    [Required]
    [Column("generation_order")]
    public int GenerationOrder { get; set; }

    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("files_json", TypeName = "jsonb")]
    public string? FilesJson { get; set; }

    [Column("files_count")]
    public int FilesCount { get; set; } = 0;

    [Column("dependencies", TypeName = "text[]")]
    public string[]? Dependencies { get; set; }

    [Column("error_message", TypeName = "text")]
    public string? ErrorMessage { get; set; }

    [Column("tokens_used")]
    public int? TokensUsed { get; set; }

    [Column("generation_time_ms")]
    public int? GenerationTimeMs { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("generated_at")]
    public DateTime? GeneratedAt { get; set; }

    // Navigation property
    public Project? Project { get; set; }
}

// DTOs for file structure
public class IDEFile
{
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ChunkDefinition
{
    public string ChunkId { get; set; } = string.Empty;
    public string ChunkType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int GenerationOrder { get; set; }
    public string[] Dependencies { get; set; } = Array.Empty<string>();
    public int EstimatedTokens { get; set; }
    public IDEFileDefinition[] Files { get; set; } = Array.Empty<IDEFileDefinition>();
}

public class IDEFileDefinition
{
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
}

public class ManifestResponse
{
    public string ProjectName { get; set; } = string.Empty;
    public string ProgrammingLanguage { get; set; } = string.Empty;
    public string GitHubPagesUrl { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public ChunkDefinition[] Chunks { get; set; } = Array.Empty<ChunkDefinition>();
    public string[] GenerationOrder { get; set; } = Array.Empty<string>();
    public SqlTableDefinition[] SqlTables { get; set; } = Array.Empty<SqlTableDefinition>();
    public ModuleDefinition[] Modules { get; set; } = Array.Empty<ModuleDefinition>();
}

public class SqlTableDefinition
{
    public string TableName { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public SqlColumnDefinition[] Columns { get; set; } = Array.Empty<SqlColumnDefinition>();
}

public class SqlColumnDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsNullable { get; set; }
    public bool IsUnique { get; set; }
}

public class ModuleDefinition
{
    public string ModuleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Inputs { get; set; } = string.Empty;
    public string Outputs { get; set; } = string.Empty;
    public string Functionality { get; set; } = string.Empty;
}

public class ChunkGenerationResponse
{
    public string ChunkId { get; set; } = string.Empty;
    public int FilesGenerated { get; set; }
    public IDEFile[] Files { get; set; } = Array.Empty<IDEFile>();
}


