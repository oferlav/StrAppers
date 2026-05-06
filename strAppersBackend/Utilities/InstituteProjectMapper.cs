using strAppersBackend.Models;

namespace strAppersBackend.Utilities;

internal static class InstituteProjectMapper
{
    /// <summary>Clips strings to PostgreSQL varchar limits on <see cref="InstituteProject"/> (avoids activate/duplicate failures).</summary>
    private static string? Clip(string? value, int maxChars)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxChars ? value : value[..maxChars];
    }

    /// <summary>
    /// Built-in catalog header uses <see cref="Organization.Logo"/> when <see cref="Project.Logo"/> is empty;
    /// institute copies should persist the same visible logo when Organization is included on <paramref name="source"/>.
    /// </summary>
    private static string? LogoForInstituteCopyFromProject(Project source)
    {
        if (!string.IsNullOrWhiteSpace(source.Logo)) return source.Logo;
        if (source.InstituteId == null && !string.IsNullOrWhiteSpace(source.Organization?.Logo))
            return source.Organization.Logo;
        return source.Logo;
    }

    /// <summary>Deep copy scalar fields from a catalog <see cref="Project"/> into a new <see cref="InstituteProject"/> row.</summary>
    public static InstituteProject CopyFromProject(Project source, int instituteId, int? baseProjectId)
    {
        return new InstituteProject
        {
            InstituteId = instituteId,
            BaseProjectId = baseProjectId,
            Title = Clip(source.Title, 200) ?? string.Empty,
            BuiltInCourseName = Clip(source.CourseName, 100),
            Mission = source.Mission,
            OneLiner = Clip(source.OneLiner, 250),
            Description = source.Description,
            ExtendedDescription = source.ExtendedDescription,
            SystemDesign = source.SystemDesign,
            DataSchema = source.DataSchema,
            Logo = LogoForInstituteCopyFromProject(source),
            SystemDesignDoc = source.SystemDesignDoc,
            SystemDesignFormatted = source.SystemDesignFormatted,
            Priority = Clip(source.Priority, 50) ?? "Medium",
            OrganizationId = source.OrganizationId,
            IsAvailable = true,
            InUse = true,
            IsBuiltIn = false,
            Kickoff = source.Kickoff,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TrelloBoardJson = source.TrelloBoardJson,
            CustomerPastStory = source.CustomerPastStory,
            ShortBrief = source.ShortBrief,
            DeploymentManifest = source.DeploymentManifest,
            IdeGenerationStatus = Clip(source.IdeGenerationStatus, 50) ?? "not_started",
            TotalChunks = source.TotalChunks,
            CompletedChunks = source.CompletedChunks,
            MockRecordsCount = source.MockRecordsCount,
            CriteriaIds = Clip(source.CriteriaIds, 500),
        };
    }

    /// <summary>Duplicate from a catalog or legacy <see cref="Project"/> row into a new institute-owned row (Project Designs copy).</summary>
    public static InstituteProject ForDuplicateFromProject(
        Project source,
        int instituteId,
        int? baseProjectId,
        string title,
        int? organizationId)
    {
        var x = CopyFromProject(source, instituteId, baseProjectId);
        x.Title = Clip(title, 200) ?? string.Empty;
        x.OrganizationId = organizationId ?? x.OrganizationId;
        x.IsAvailable = false;
        x.InUse = true;
        x.IsBuiltIn = false;
        x.CreatedAt = DateTime.UtcNow;
        x.UpdatedAt = DateTime.UtcNow;
        x.TrelloBoardJson = null;
        return x;
    }

    /// <summary>Duplicate from an existing <see cref="InstituteProject"/> (another copy or custom row).</summary>
    public static InstituteProject ForDuplicateFromInstituteProject(
        InstituteProject source,
        int instituteId,
        string title,
        int? organizationId)
    {
        return new InstituteProject
        {
            InstituteId = instituteId,
            BaseProjectId = source.BaseProjectId,
            Title = Clip(title, 200) ?? string.Empty,
            BuiltInCourseName = Clip(source.BuiltInCourseName, 100),
            Mission = source.Mission,
            OneLiner = Clip(source.OneLiner, 250),
            Description = source.Description,
            ExtendedDescription = source.ExtendedDescription,
            SystemDesign = source.SystemDesign,
            DataSchema = source.DataSchema,
            Logo = source.Logo,
            SystemDesignDoc = source.SystemDesignDoc,
            SystemDesignFormatted = source.SystemDesignFormatted,
            Priority = Clip(source.Priority, 50) ?? "Medium",
            OrganizationId = organizationId ?? source.OrganizationId,
            IsAvailable = false,
            InUse = true,
            IsBuiltIn = false,
            Kickoff = source.Kickoff,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TrelloBoardJson = null,
            CustomerPastStory = source.CustomerPastStory,
            ShortBrief = source.ShortBrief,
            DeploymentManifest = source.DeploymentManifest,
            IdeGenerationStatus = Clip(source.IdeGenerationStatus, 50) ?? "not_started",
            TotalChunks = source.TotalChunks,
            CompletedChunks = source.CompletedChunks,
            MockRecordsCount = source.MockRecordsCount,
            CriteriaIds = Clip(source.CriteriaIds, 500),
        };
    }
}
