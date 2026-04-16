using System.Text.Json;

namespace strAppersBackend.Utilities;

/// <summary>Trello REST returns checkbox values as <c>{"checked":"true"}</c> (string) or JSON booleans. Do not call <see cref="JsonElement.GetBoolean"/> without checking <see cref="JsonElement.ValueKind"/>.</summary>
public static class TrelloCheckboxJson
{
    public static string CheckedToString(JsonElement checkedElement)
    {
        return checkedElement.ValueKind switch
        {
            JsonValueKind.String => (checkedElement.GetString() ?? "").Trim(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => checkedElement.TryGetInt32(out var n) ? (n != 0 ? "true" : "false") : checkedElement.GetRawText().Trim(),
            _ => checkedElement.GetRawText().Trim()
        };
    }
}
