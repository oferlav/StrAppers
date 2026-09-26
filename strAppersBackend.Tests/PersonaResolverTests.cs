using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Tests;

/// <summary>
/// Main-persona precedence: board project override, then institute, then none.
/// The regression that matters: with no project override, every lookup returns exactly what the old
/// institute-only lookup returned, so existing institutes see no change in prompts, labels or aliases.
/// </summary>
public class PersonaResolverTests
{
    private const int ProfessorId = 1, ConsumerId = 2, InstituteId = 10, ProjectId = 100, StudentId = 5;
    private const string BoardId = "board1";

    private static ApplicationDbContext CreateDb(int? institutePersonaId, int? projectPersonaId, string? studentBoardId = BoardId)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Personas.AddRange(
            new Persona { Id = ProfessorId, Name = "Professor", Prompt = "professor prompt" },
            new Persona { Id = ConsumerId, Name = "Consumer", Prompt = "consumer prompt" });
        db.Institutes.Add(new Institute { Id = InstituteId, Name = "Test Institute", MainAIPersonaId = institutePersonaId });
        db.InstituteProjects.Add(new InstituteProject { Id = ProjectId, InstituteId = InstituteId, Title = "Dinner Scout", MainAIPersonaId = projectPersonaId });
        db.ProjectBoards.Add(new ProjectBoard { Id = BoardId, ProjectId = 1, InstituteProjectId = ProjectId });
        db.Students.Add(new Student
        {
            Id = StudentId, FirstName = "S", LastName = "T", Email = "s@test.com", GithubUser = "gh",
            MajorId = 1, YearId = 1, BoardId = studentBoardId, InstituteId = InstituteId
        });
        db.SaveChanges();
        return db;
    }

    /// <summary>The lookup PersonaAlias / AssessmentReport used before the project override existed.</summary>
    private static Task<string?> OldInstituteOnlyName(ApplicationDbContext db) =>
        db.Students.AsNoTracking()
            .Where(s => s.Id == StudentId && s.Institute != null && s.Institute.MainAIPersonaId != null)
            .Select(s => s.Institute!.MainAIPersona!.Name)
            .FirstOrDefaultAsync();

    [Theory]
    [InlineData(null)]
    [InlineData(ProfessorId)]
    public async Task NoProjectOverride_StudentLookup_MatchesOldInstituteOnlyLookup(int? institutePersonaId)
    {
        using var db = CreateDb(institutePersonaId, projectPersonaId: null);

        var resolved = await PersonaResolver.ForStudentAsync(db, StudentId);

        Assert.Equal(await OldInstituteOnlyName(db), resolved?.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ProfessorId)]
    public async Task NoProjectOverride_AliasBlock_IsUnchanged(int? institutePersonaId)
    {
        using var db = CreateDb(institutePersonaId, projectPersonaId: null);

        var alias = await PersonaAlias.ResolveForStudentAsync(db, StudentId);

        Assert.Equal(PersonaAlias.BuildAliasBlock(await OldInstituteOnlyName(db)), alias);
    }

    [Fact]
    public async Task ProjectOverride_WinsOverInstitute()
    {
        using var db = CreateDb(institutePersonaId: ProfessorId, projectPersonaId: ConsumerId);

        var byStudent = await PersonaResolver.ForStudentAsync(db, StudentId);
        var byProject = await PersonaResolver.ForProjectAsync(db, ProjectId, InstituteId);

        Assert.Equal(new PersonaResolver.MainPersona("Consumer", "consumer prompt"), byStudent);
        Assert.Equal(byStudent, byProject);
        Assert.Contains("AI Consumer", await PersonaAlias.ResolveForStudentAsync(db, StudentId));
    }

    [Fact]
    public async Task ProjectOverride_WithNoInstitutePersona_StillApplies()
    {
        using var db = CreateDb(institutePersonaId: null, projectPersonaId: ConsumerId);

        Assert.Equal("Consumer", (await PersonaResolver.ForStudentAsync(db, StudentId))?.Name);
    }

    [Fact]
    public async Task StudentWithoutBoard_FallsBackToInstitute()
    {
        using var db = CreateDb(institutePersonaId: ProfessorId, projectPersonaId: ConsumerId, studentBoardId: null);

        Assert.Equal("Professor", (await PersonaResolver.ForStudentAsync(db, StudentId))?.Name);
    }

    [Fact]
    public async Task ForProject_WithoutProjectId_UsesInstitute()
    {
        using var db = CreateDb(institutePersonaId: ProfessorId, projectPersonaId: ConsumerId);

        Assert.Equal("professor prompt", (await PersonaResolver.ForProjectAsync(db, null, InstituteId))?.Prompt);
    }

    [Fact]
    public async Task NothingSelected_ReturnsNull()
    {
        using var db = CreateDb(institutePersonaId: null, projectPersonaId: null);

        Assert.Null(await PersonaResolver.ForStudentAsync(db, StudentId));
        Assert.Null(await PersonaResolver.ForProjectAsync(db, ProjectId, InstituteId));
        Assert.Null(await PersonaResolver.ForProjectAsync(db, null, null));
    }

    [Fact]
    public async Task UnknownStudent_ReturnsNull()
    {
        using var db = CreateDb(institutePersonaId: ProfessorId, projectPersonaId: null);

        Assert.Null(await PersonaResolver.ForStudentAsync(db, 999));
        Assert.Null(await PersonaResolver.ForStudentAsync(db, 0));
    }

    [Fact]
    public void Pick_PrefersProject()
    {
        var project = new PersonaResolver.MainPersona("Consumer", "c");
        var institute = new PersonaResolver.MainPersona("Professor", "p");

        Assert.Same(project, PersonaResolver.Pick(project, institute));
        Assert.Same(institute, PersonaResolver.Pick(null, institute));
        Assert.Null(PersonaResolver.Pick(null, null));
    }

    /// <summary>The EF model must map the new entities onto the tables and constraint names the SQL script created.</summary>
    [Fact]
    public void Model_MapsNewTablesAndTruncatedConstraintNames()
    {
        using var db = CreateDb(null, null);
        var model = db.Model;

        Assert.Equal("InstituteProjectPersonas", model.FindEntityType(typeof(InstituteProjectPersona))!.GetTableName());
        Assert.Equal("PersonaChatHistory", model.FindEntityType(typeof(PersonaChatHistory))!.GetTableName());
        Assert.Equal("AgentScenarioSets", model.FindEntityType(typeof(AgentScenarioSet))!.GetTableName());
        Assert.Equal("ProjectAgentScenarioSets", model.FindEntityType(typeof(ProjectAgentScenarioSet))!.GetTableName());
        Assert.Equal("AgentGradingReports", model.FindEntityType(typeof(AgentGradingReport))!.GetTableName());

        var fkNames = model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()).Select(fk => fk.GetConstraintName()).ToHashSet();
        Assert.Contains("FK_InstituteProjectPersonas_InstituteProjects_InstituteProjectI", fkNames);
        Assert.Contains("FK_ProjectAgentScenarioSets_InstituteProjects_InstituteProjectI", fkNames);
        Assert.Contains("FK_InstituteProjects_AIPersonas_MainAIPersonaId", fkNames);
    }
}
