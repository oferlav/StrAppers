using System.Text.Json.Nodes;

namespace strAppersBackend.Utilities;

/// <summary>
/// Removes bulky Figma file JSON keys that are rarely needed for UX/structure review but dominate payload size (bounds, vector geometry, overrides).
/// </summary>
public static class FigmaMetadataPruner
{
    private static readonly HashSet<string> HeavyPropertyNames = new(StringComparer.Ordinal)
    {
        "absoluteBoundingBox",
        "absoluteRenderBounds",
        "fillGeometry",
        "strokeGeometry",
        "effects",
        "vectorNetwork",
        "arcData",
        "fillOverrideTable",
        "characterStyleOverrides",
        "styleOverrideTable",
        "scrollTransition",
        "transitionNodeID",
        "scrollBehavior",
        "exportSettings",
        "sharedPluginData",
        "pluginData",
        "pluginRelaunchData",
        "boundVariables",
        "interactions",
        // Reactions can be large; structure/naming usually suffice for a first-pass UI review.
        "reactions",
    };

    /// <summary>
    /// Returns compact JSON (no indent). If parsing fails, returns <paramref name="json"/> unchanged.
    /// </summary>
    public static string PruneHeavyKeys(string json)
    {
        try
        {
            var root = JsonNode.Parse(json);
            if (root is null)
                return json;
            Prune(root);
            return root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return json;
        }
    }

    private static void Prune(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var key in o.Select(x => x.Key).ToList())
                {
                    if (HeavyPropertyNames.Contains(key))
                        o.Remove(key);
                    else
                        Prune(o[key]);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                    Prune(item);
                break;
        }
    }
}
