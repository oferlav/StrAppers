using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

/// <summary>Builds Task Builder (Templates source) prompts and parses model JSON — shared with the institute project assistant endpoint.</summary>
public static class InstituteTaskBuilderAssistantHelper
{
    public const int TrelloJsonMaxChars = 120_000;

    public static string StripDebugMarkers(string s) => Regex.Replace(s, @"<<[^>]+>>\s*", string.Empty, RegexOptions.CultureInvariant);

    /// <summary>Loads <c>Prompts/Mentor/TaskBuilderAssistant.txt</c> (same as legacy Mentor path).</summary>
    public static string? LoadTaskBuilderSystemPrompt()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "Mentor", "TaskBuilderAssistant.txt");
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    public static string BuildUserPrompt(
        Project project,
        IProjectModuleRow? module,
        ProjectInstituteTemplatesAssistantRequest request,
        string? historyBlock)
    {
        var trelloRaw = request.TrelloBoardJson?.Trim() ?? "{}";
        var sprintScopedContext = BuildSprintScopedContextBlock(trelloRaw, request.SprintName);

        var userSb = new StringBuilder();
        userSb.AppendLine("## Selected sprint / role");
        userSb.AppendLine($"- Sprint list name: {(string.IsNullOrWhiteSpace(request.SprintName) ? "(none)" : request.SprintName.Trim())}");
        userSb.AppendLine($"- Role: {(string.IsNullOrWhiteSpace(request.RoleName) ? "(none)" : request.RoleName.Trim())}");
        userSb.AppendLine("- Task Builder may only return updates for this card’s task description + checklist (Sprint Overview + Checklist in the UI). Module and project text below is context only — not editable via this endpoint.");
        userSb.AppendLine();
        userSb.AppendLine("## Project");
        userSb.AppendLine($"- Id: {project.Id}");
        userSb.AppendLine($"- Title: {project.Title ?? "(none)"}");
        userSb.AppendLine("- ExtendedDescription:");
        userSb.AppendLine(string.IsNullOrWhiteSpace(project.ExtendedDescription)
            ? "(none)"
            : project.ExtendedDescription!.Trim());
        userSb.AppendLine();
        userSb.AppendLine("## Selected module");
        if (request.ModuleId <= 0 || module == null)
        {
            userSb.AppendLine("(No module selected or module not found for this project.)");
        }
        else
        {
            userSb.AppendLine($"- ModuleId: {module.Id}");
            userSb.AppendLine($"- Title: {module.Title ?? "(none)"}");
            userSb.AppendLine("- Description:");
            userSb.AppendLine(string.IsNullOrWhiteSpace(module.Description) ? "(none)" : module.Description!.Trim());
        }

        userSb.AppendLine();
        userSb.AppendLine("## Current card fields in Task Builder (may be empty)");
        userSb.AppendLine("- Description:");
        userSb.AppendLine(string.IsNullOrWhiteSpace(request.CurrentDescription)
            ? "(none)"
            : request.CurrentDescription!.Trim());
        userSb.AppendLine("- Checklist items:");
        if (request.CurrentChecklistItems == null || request.CurrentChecklistItems.Count == 0)
        {
            userSb.AppendLine("(none)");
        }
        else
        {
            foreach (var line in request.CurrentChecklistItems)
            {
                userSb.AppendLine($"  - {line}");
            }
        }

        userSb.AppendLine();
        userSb.AppendLine("## Sprint-scoped template context");
        userSb.AppendLine(sprintScopedContext);
        userSb.AppendLine();
        if (!string.IsNullOrWhiteSpace(historyBlock))
        {
            userSb.AppendLine(historyBlock);
        }
        userSb.AppendLine("## User message");
        userSb.AppendLine(request.UserMessage.Trim());
        return userSb.ToString();
    }

    /// <inheritdoc cref="BuildUserPrompt(Project, ProjectModule?, ProjectInstituteTemplatesAssistantRequest, string?)"/>
    public static string BuildUserPrompt(
        InstituteProject project,
        IProjectModuleRow? module,
        ProjectInstituteTemplatesAssistantRequest request,
        string? historyBlock)
    {
        var synthetic = new Project
        {
            Id = project.Id,
            Title = project.Title,
            ExtendedDescription = project.ExtendedDescription,
        };
        return BuildUserPrompt(synthetic, module, request, historyBlock);
    }

    public static string TruncateForPreview(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "…";

    public static bool TryParseTaskBuilderMentorJson(string llmText, out TaskBuilderMentorLlmResult? dto)
    {
        dto = null;
        var json = ExtractJsonObjectForTaskBuilder(llmText);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            dto = JsonSerializer.Deserialize<TaskBuilderMentorLlmResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            return dto != null;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractJsonObjectForTaskBuilder(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            t = t["```json".Length..].Trim();
        }

        if (t.StartsWith("```"))
        {
            t = t[3..].Trim();
        }

        if (t.EndsWith("```", StringComparison.Ordinal))
        {
            t = t[..^3].Trim();
        }

        var i = t.IndexOf('{');
        var j = t.LastIndexOf('}');
        if (i < 0 || j <= i)
        {
            return null;
        }

        return t[i..(j + 1)];
    }

    private static string BuildSprintScopedContextBlock(string trelloRaw, string? sprintNameRaw)
    {
        var selectedSprint = sprintNameRaw?.Trim() ?? string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine($"- Selected sprint requested: {(string.IsNullOrWhiteSpace(selectedSprint) ? "(none)" : selectedSprint)}");
        sb.AppendLine("- Context policy: include only selected sprint, with all roles/cards inside that sprint.");
        sb.AppendLine("- Edit policy: assistant may only propose updates for the selected role/card output fields.");

        if (string.IsNullOrWhiteSpace(selectedSprint))
        {
            sb.AppendLine("- Sprint context status: missing sprint selection; no sprint cards were provided to the model.");
            return sb.ToString().TrimEnd();
        }

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(trelloRaw) ? "{}" : trelloRaw);
            var root = doc.RootElement;
            var availableSprints = CollectAvailableSprintNames(root);
            var matchingListNames = availableSprints
                .Where(s => NormalizedKey(s) == NormalizedKey(selectedSprint))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var selectedCards = CollectCardsForSprint(root, selectedSprint);

            if (matchingListNames.Count == 0)
            {
                sb.AppendLine("- Sprint context status: selected sprint was not found in template; no sprint cards were provided.");
                if (availableSprints.Count > 0)
                {
                    sb.AppendLine($"- Available sprints: {string.Join(", ", availableSprints)}");
                }
                else
                {
                    sb.AppendLine("- Available sprints: (none detected)");
                }
                return sb.ToString().TrimEnd();
            }

            var sprintOnlyObject = new
            {
                selectedSprint = matchingListNames[0],
                matchedSprintLists = matchingListNames,
                cards = selectedCards,
            };

            var sprintJson = JsonSerializer.Serialize(sprintOnlyObject, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            if (sprintJson.Length > TrelloJsonMaxChars)
            {
                sprintJson = sprintJson[..TrelloJsonMaxChars] +
                             "\n… [truncated for prompt size; only selected sprint context is included.]";
            }

            sb.AppendLine($"- Sprint context status: found selected sprint; included {selectedCards.Count} card(s) across all roles in this sprint.");
            sb.AppendLine("- Selected sprint JSON:");
            sb.AppendLine(sprintJson);
            return sb.ToString().TrimEnd();
        }
        catch (JsonException)
        {
            sb.AppendLine("- Sprint context status: template JSON is malformed; sprint cards were not available.");
            return sb.ToString().TrimEnd();
        }
        catch
        {
            sb.AppendLine("- Sprint context status: failed to prepare sprint context; sprint cards were not available.");
            return sb.ToString().TrimEnd();
        }
    }

    private static List<string> CollectAvailableSprintNames(JsonElement root)
    {
        var names = new List<string>();

        if (root.TryGetProperty("sprintPlan", out var sprintPlan))
        {
            names.AddRange(ReadListNames(sprintPlan, "lists"));
        }

        if (root.TryGetProperty("SprintPlan", out var sprintPlanPascal))
        {
            names.AddRange(ReadListNames(sprintPlanPascal, "Lists"));
        }

        return names
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<object> CollectCardsForSprint(JsonElement root, string sprintName)
    {
        var cards = new List<object>();
        cards.AddRange(ReadCardsForSprint(root, "sprintPlan", "cards", sprintName));
        cards.AddRange(ReadCardsForSprint(root, "SprintPlan", "Cards", sprintName));
        return cards;
    }

    private static IEnumerable<object> ReadCardsForSprint(
        JsonElement root,
        string sprintPlanProperty,
        string cardsProperty,
        string sprintName)
    {
        if (!root.TryGetProperty(sprintPlanProperty, out var sprintPlan) ||
            !sprintPlan.TryGetProperty(cardsProperty, out var cardsElement) ||
            cardsElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var card in cardsElement.EnumerateArray())
        {
            if (card.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var listName = ReadString(card, "listName", "ListName");
            if (NormalizedKey(listName) != NormalizedKey(sprintName))
            {
                continue;
            }

            var checklistItems = ReadStringArray(card, "checklistItems", "ChecklistItems");
            var labels = ReadStringArray(card, "labels", "Labels");
            var roleName = ReadString(card, "roleName", "RoleName");
            if (string.IsNullOrWhiteSpace(roleName) && labels.Count > 0)
            {
                roleName = labels[0];
            }

            yield return new
            {
                listName = listName?.Trim() ?? sprintName.Trim(),
                roleName = roleName?.Trim(),
                labels,
                title = ReadString(card, "title", "Title", "name", "Name"),
                description = ReadString(card, "description", "Description"),
                checklistItems,
            };
        }
    }

    private static IEnumerable<string> ReadListNames(JsonElement sprintPlan, string listsProperty)
    {
        if (!sprintPlan.TryGetProperty(listsProperty, out var listsElement) || listsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return listsElement
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => ReadString(item, "name", "Name"))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim());
    }

    private static string? ReadString(JsonElement obj, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (obj.TryGetProperty(propertyName, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }

                if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
                {
                    var raw = value.ToString();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        return raw;
                    }
                }
            }
        }

        return null;
    }

    private static List<string> ReadStringArray(JsonElement obj, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!obj.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            return value
                .EnumerateArray()
                .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .ToList();
        }

        return new List<string>();
    }

    private static string NormalizedKey(string? s) =>
        Regex.Replace((s ?? string.Empty).Trim(), @"\s+", " ", RegexOptions.CultureInvariant).ToLowerInvariant();
}
