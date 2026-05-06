namespace strAppersBackend.Models;

/// <summary>
/// Shared shape for catalog <see cref="ProjectModule"/> rows and institute-owned <see cref="InstituteProjectModule"/> rows (course builder, assistants).
/// </summary>
public interface IProjectModuleRow
{
    int Id { get; }
    int? ModuleType { get; }
    string? Title { get; }
    string? Description { get; }
    int? Sequence { get; }
}
