using System.Text.Json;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Regression tests for the empty-board bug: CreateBoard deserialises the saved course template with
/// JsonSerializer.Deserialize&lt;TrelloProjectCreationRequest&gt;(json). Stored TrelloBoardJson holds
/// ModuleId as a JSON number in some rows and a string in others — the module-id remap
/// (ApplyModuleIdMapNumericRegex) and the sanitiser (TryGetValue&lt;int&gt; first) both accept either
/// shape, but TrelloCard.ModuleId is a string and deserialisation did not.
///
/// The observed failure, on a 40,212-char template:
///   JsonException: The JSON value could not be converted to System.String.
///   Path: $.SprintPlan.Cards[0].ModuleId
///
/// It threw on the FIRST card, so the whole template was discarded and the board was built from the
/// AI/fallback plan instead — a board with one "Basic development task for ..." card and no checklists.
/// </summary>
public class TrelloCardModuleIdConverterTests
{
    private static TrelloCard DeserializeCard(string moduleIdJson)
    {
        var json = $$"""
        { "Title": "Card", "ModuleId": {{moduleIdJson}}, "RoleName": "History Student 1" }
        """;
        var card = JsonSerializer.Deserialize<TrelloCard>(json);
        Assert.NotNull(card);
        return card!;
    }

    [Fact]
    public void NumericModuleId_Deserializes_WasTheEmptyBoardCause()
    {
        Assert.Equal("6941", DeserializeCard("6941").ModuleId);
    }

    [Fact]
    public void StringModuleId_StillDeserializes()
    {
        Assert.Equal("6941", DeserializeCard("\"6941\"").ModuleId);
    }

    [Fact]
    public void NullModuleId_StaysNull_UnchangedByTheConverter()
    {
        // System.Text.Json short-circuits a null token and never invokes the converter, so its
        // null branch is dead for this property and the value lands as null — exactly as it did
        // before the converter was applied. Asserted so the behaviour is stated rather than assumed:
        // making it empty would mean changing FlexibleStringConverter.HandleNull, which is shared
        // with ModuleInfo.Inputs/Outputs and the AI deserialiser.
        Assert.Null(DeserializeCard("null").ModuleId);
    }

    [Fact]
    public void MissingModuleId_KeepsTheDefault()
    {
        var card = JsonSerializer.Deserialize<TrelloCard>("""{ "Title": "Card" }""");
        Assert.NotNull(card);
        Assert.Equal(string.Empty, card!.ModuleId);
    }

    [Fact]
    public void ModuleId_AlwaysSerializesBackAsAString()
    {
        var json = JsonSerializer.Serialize(new TrelloCard { ModuleId = "6941" });
        Assert.Contains("\"ModuleId\":\"6941\"", json);
        Assert.DoesNotContain("\"ModuleId\":6941", json);
    }

    [Fact]
    public void FullRequestGraph_WithNumericModuleId_ParsesInsteadOfThrowing()
    {
        // Mirrors the real shape and the exact path from the exception: $.SprintPlan.Cards[0].ModuleId
        var json = """
        {
          "ProjectId": 78,
          "ProjectTitle": "Chapter1",
          "SprintPlan": {
            "Cards": [
              { "Title": "Sprint 1 task", "ModuleId": 6941, "RoleName": "History Student 1", "ListName": "Sprint 1" },
              { "Title": "Sprint 2 task", "ModuleId": "6942", "RoleName": "History Student 2", "ListName": "Sprint 2" }
            ]
          }
        }
        """;

        var request = JsonSerializer.Deserialize<TrelloProjectCreationRequest>(json);

        Assert.NotNull(request);
        Assert.NotNull(request!.SprintPlan);
        Assert.Equal(2, request.SprintPlan!.Cards.Count);
        Assert.Equal("6941", request.SprintPlan.Cards[0].ModuleId);
        Assert.Equal("6942", request.SprintPlan.Cards[1].ModuleId);
    }

    [Fact]
    public void AiProjectTask_NumericModuleId_AlsoParses()
    {
        var task = JsonSerializer.Deserialize<ProjectTask>("""{ "Title": "T", "ModuleId": 12 }""");

        Assert.NotNull(task);
        Assert.Equal("12", task!.ModuleId);
    }
}
