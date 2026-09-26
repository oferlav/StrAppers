using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using strAppersBackend.Controllers;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>Side personas: prompt assembly and the tab list a board shows.</summary>
public class SidePersonasTests
{
    [Fact]
    public void Prompt_AppendsOverviewAndPersonaContext()
    {
        var prompt = SidePersonasController.BuildSystemPrompt("IT Manager", "You are Jordan.", "Dinner Scout", "INTEGRATION SHEET");

        Assert.StartsWith("You are Jordan.", prompt);
        Assert.Contains("=== PROJECT OVERVIEW ===\nDinner Scout", prompt);
        Assert.EndsWith("=== IT MANAGER CONTEXT ===\nINTEGRATION SHEET\n=== END IT MANAGER CONTEXT ===", prompt);
    }

    [Fact]
    public void Prompt_UsesDescriptionPlaceholderWhenPresent()
    {
        var prompt = SidePersonasController.BuildSystemPrompt("IT Manager", "Project: [INSERT PROJECT DESCRIPTION HERE]. Be exact.", "Dinner Scout", null);

        Assert.StartsWith("Project: Dinner Scout. Be exact.", prompt);
        Assert.DoesNotContain("PROJECT OVERVIEW", prompt);
        Assert.Contains("=== IT MANAGER CONTEXT ===\n(None.)", prompt);
    }

    [Fact]
    public void Prompt_BlankPersonaPrompt_FallsBackToGenericInstruction()
    {
        var prompt = SidePersonasController.BuildSystemPrompt("IT Manager", "  ", null, "ctx");

        Assert.StartsWith("You are the IT Manager on this project.", prompt);
        Assert.Contains("(No project description.)", prompt);
    }

    private static (SidePersonasController Controller, ApplicationDbContext Db) CreateController()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Personas.AddRange(new Persona { Id = 1, Name = "Consumer" }, new Persona { Id = 2, Name = "IT Manager" }, new Persona { Id = 3, Name = "Legal" });
        db.InstituteProjects.Add(new InstituteProject { Id = 100, InstituteId = 10, Title = "Dinner Scout" });
        db.ProjectBoards.AddRange(
            new ProjectBoard { Id = "withSides", ProjectId = 1, InstituteProjectId = 100 },
            new ProjectBoard { Id = "noProject", ProjectId = 1 });
        db.InstituteProjectPersonas.AddRange(
            new InstituteProjectPersona { InstituteProjectId = 100, PersonaId = 3, SortOrder = 2 },
            new InstituteProjectPersona { InstituteProjectId = 100, PersonaId = 2, SortOrder = 1 });
        db.SaveChanges();

        var controller = new SidePersonasController(db, Mock.Of<IChatCompletionService>(),
            Options.Create(new PromptConfig()), Options.Create(new TestingConfig()),
            new ConfigurationBuilder().Build(), NullLogger<SidePersonasController>.Instance);
        return (controller, db);
    }

    private static List<string> Names(ActionResult<object> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = JsonSerializer.Serialize(ok.Value);
        return JsonDocument.Parse(json).RootElement.GetProperty("Personas").EnumerateArray()
            .Select(p => p.GetProperty("Name").GetString()!).ToList();
    }

    [Fact]
    public async Task ByBoard_ListsSidePersonasInSortOrder()
    {
        var (controller, db) = CreateController();
        using (db)
            Assert.Equal(new[] { "IT Manager", "Legal" }, Names(await controller.GetByBoard("withSides")));
    }

    [Theory]
    [InlineData("noProject")]
    [InlineData("unknownBoard")]
    public async Task ByBoard_WithoutSidePersonas_IsEmpty(string boardId)
    {
        var (controller, db) = CreateController();
        using (db)
            Assert.Empty(Names(await controller.GetByBoard(boardId)));
    }
}
