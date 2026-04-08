using System.Text.Json.Nodes;

namespace strAppersBackend.Utilities;

/// <summary>
/// Logic-first Figma file JSON → compact semantic tree (IA, layout intent, text) for LLM consumption.
/// Based on “Figma-to-Semantic JSON” spec: drop geometry/noise; keep hierarchy, auto-layout signals, TEXT content, inferred roles.
/// </summary>
public static class FigmaSemanticJsonTransformer
{
    /// <summary>
    /// Wraps transformed tree plus metadata. On failure returns null.
    /// </summary>
    public static string? TryTransform(string rawFigmaFileJson)
    {
        try
        {
            var root = JsonNode.Parse(rawFigmaFileJson);
            if (root is not JsonObject fileObj)
                return null;

            var document = fileObj["document"] as JsonObject;
            if (document is null)
                return null;

            var tree = TransformNode(document);
            if (tree is null)
                return null;

            var wrapper = new JsonObject
            {
                ["semanticVersion"] = 1,
                ["instruction"] =
                    "Logic-first Figma projection: hierarchy + layout intent + text. Not pixel-accurate. " +
                    "Use parent/child relationships and names for spatial reasoning. " +
                    "When layer names include known services (e.g. linkedin), content.semanticIcon is set; generic names like Vector 102 carry no icon semantics.",
                ["tree"] = tree
            };

            return wrapper.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return null;
        }
    }

    private static JsonObject? TransformNode(JsonObject? node)
    {
        if (node is null) return null;
        if (!IsNodeVisible(node)) return null;

        var type = node["type"]?.GetValue<string>() ?? "";
        var children = node["children"] as JsonArray;

        // Flatten: FRAME-like with exactly one TEXT child and no meaningful background
        if (children is { Count: 1 } &&
            IsFrameLike(type) &&
            children[0] is JsonObject singleChild &&
            singleChild["type"]?.GetValue<string>() == "TEXT" &&
            !HasSignificantBackground(node))
        {
            return BuildFlattenedFrameWithText(node, singleChild);
        }

        var output = new JsonObject
        {
            ["nodeId"] = node["id"]?.GetValue<string>() ?? "",
            ["name"] = node["name"]?.GetValue<string>() ?? "",
            ["type"] = type,
            ["role"] = InferRole(node["name"]?.GetValue<string>() ?? "", type)
        };

        var content = BuildContent(node, type);
        if (content.Count > 0)
            output["content"] = content;

        var layoutLogic = BuildLayoutLogic(node);
        if (layoutLogic.Count > 0)
            output["layoutLogic"] = layoutLogic;

        var visualIntent = BuildVisualIntent(node);
        if (visualIntent.Count > 0)
            output["visualIntent"] = visualIntent;

        if (children is not null && children.Count > 0)
        {
            var outChildren = new JsonArray();
            foreach (var c in children)
            {
                if (c is JsonObject co)
                {
                    var t = TransformNode(co);
                    if (t is not null)
                        outChildren.Add(t);
                }
            }

            output["children"] = outChildren;
        }
        else
        {
            output["children"] = new JsonArray();
        }

        return output;
    }

    private static JsonObject BuildFlattenedFrameWithText(JsonObject frame, JsonObject textNode)
    {
        var type = frame["type"]?.GetValue<string>() ?? "FRAME";
        var name = frame["name"]?.GetValue<string>() ?? "";
        var output = new JsonObject
        {
            ["nodeId"] = frame["id"]?.GetValue<string>() ?? "",
            ["name"] = name,
            ["type"] = type,
            ["role"] = InferRole(name, type),
            ["flattenedFrom"] = "frame+single-text"
        };

        var content = new JsonObject();
        var chars = textNode["characters"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(chars))
            content["text"] = chars;
        var textName = textNode["name"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(textName) &&
            !string.Equals(textName, name, StringComparison.OrdinalIgnoreCase))
            content["textLayerName"] = textName;
        var icon = InferIconHint(textName ?? "", textNode["type"]?.GetValue<string>() ?? "");
        if (!string.IsNullOrEmpty(icon))
            content["icon"] = icon;
        var semanticFromFrame = TryGetSemanticIconSlug(name);
        var semanticFromText = TryGetSemanticIconSlug(textName ?? "");
        if (semanticFromFrame != null)
            content["semanticIcon"] = semanticFromFrame;
        else if (semanticFromText != null)
            content["semanticIcon"] = semanticFromText;
        if (content.Count > 0)
            output["content"] = content;

        var layoutLogic = BuildLayoutLogic(frame);
        if (layoutLogic.Count > 0)
            output["layoutLogic"] = layoutLogic;

        var visualIntent = BuildVisualIntent(frame);
        if (visualIntent.Count > 0)
            output["visualIntent"] = visualIntent;

        output["children"] = new JsonArray();
        return output;
    }

    private static JsonObject BuildContent(JsonObject node, string type)
    {
        var content = new JsonObject();
        if (type == "TEXT")
        {
            var chars = node["characters"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(chars))
                content["text"] = chars;
        }

        var name = node["name"]?.GetValue<string>() ?? "";
        var icon = InferIconHint(name, type);
        if (!string.IsNullOrEmpty(icon))
            content["icon"] = icon;

        if (TryGetSemanticIconSlug(name) is { } semanticSlug)
            content["semanticIcon"] = semanticSlug;

        return content;
    }

    private static JsonObject BuildLayoutLogic(JsonObject node)
    {
        var layout = new JsonObject();
        var mode = node["layoutMode"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(mode) && mode != "NONE")
        {
            layout["direction"] = mode;
            if (node["itemSpacing"] is JsonValue sp && sp.TryGetValue(out int spacing))
                layout["spacing"] = spacing;
            else if (node["itemSpacing"] is JsonValue spD && spD.TryGetValue<double>(out var spacingD))
                layout["spacing"] = spacingD;

            var pad = new JsonObject();
            void Pad(string key, string figmaKey)
            {
                if (node[figmaKey] is JsonValue v && v.TryGetValue<double>(out var n) && n > 0)
                    pad[key] = n;
            }
            Pad("top", "paddingTop");
            Pad("right", "paddingRight");
            Pad("bottom", "paddingBottom");
            Pad("left", "paddingLeft");
            if (pad.Count > 0)
                layout["padding"] = pad;

            var pa = node["primaryAxisAlignItems"]?.GetValue<string>();
            var ca = node["counterAxisAlignItems"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(pa) || !string.IsNullOrEmpty(ca))
                layout["alignment"] = string.Join("/", new[] { pa, ca }.Where(s => !string.IsNullOrEmpty(s)));

            var h = node["layoutSizingHorizontal"]?.GetValue<string>();
            var v = node["layoutSizingVertical"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(h) || !string.IsNullOrEmpty(v))
                layout["sizing"] = $"{h ?? "?"}/{v ?? "?"}";
        }

        return layout;
    }

    private static JsonObject BuildVisualIntent(JsonObject node)
    {
        var vi = new JsonObject();
        vi["isVisible"] = IsNodeVisible(node);

        var token = InferVisualToken(node);
        if (!string.IsNullOrEmpty(token))
            vi["token"] = token;

        return vi;
    }

    private static bool IsNodeVisible(JsonObject node)
    {
        if (node["visible"] is JsonValue v)
        {
            if (v.TryGetValue<bool>(out var b))
                return b;
        }
        return true;
    }

    private static bool IsFrameLike(string type) =>
        type is "FRAME" or "COMPONENT" or "INSTANCE" or "COMPONENT_SET";

    private static bool HasSignificantBackground(JsonObject frame)
    {
        if (frame["fills"] is not JsonArray fills || fills.Count == 0)
            return false;
        foreach (var f in fills)
        {
            if (f is not JsonObject fo) continue;
            var visible = fo["visible"];
            if (visible is JsonValue vv && vv.TryGetValue<bool>(out var vis) && !vis)
                continue;
            if (fo["opacity"] is JsonValue op && op.TryGetValue<double>(out var o) && o < 0.02)
                continue;
            if (fo["type"]?.GetValue<string>() == "SOLID" &&
                fo["color"] is JsonObject c &&
                c["a"] is JsonValue av && av.TryGetValue<double>(out var a) && a < 0.02)
                continue;
            return true;
        }
        return false;
    }

    private static string InferRole(string name, string type)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("button", StringComparison.Ordinal) || n.Contains("btn_", StringComparison.Ordinal) || n.StartsWith("btn", StringComparison.Ordinal))
            return "cta";
        if (n.Contains("nav", StringComparison.Ordinal) || n.Contains("navbar", StringComparison.Ordinal) || n.Contains("tab bar", StringComparison.Ordinal))
            return "navigation";
        if (n.Contains("badge", StringComparison.Ordinal) || n.Contains("tag", StringComparison.Ordinal) || n.Contains("pill", StringComparison.Ordinal) || n.Contains("chip", StringComparison.Ordinal))
            return "status-badge";
        if (n.Contains("hot", StringComparison.Ordinal) && (n.Contains("project", StringComparison.Ordinal) || n.Contains("badge", StringComparison.Ordinal)))
            return "hot-priority-badge";
        if (n.Contains("header", StringComparison.Ordinal)) return "header";
        if (n.Contains("footer", StringComparison.Ordinal)) return "footer";
        if (n.Contains("sidebar", StringComparison.Ordinal) || n.Contains("drawer", StringComparison.Ordinal))
            return "sidebar";
        if (n.Contains("input", StringComparison.Ordinal) || n.Contains("field", StringComparison.Ordinal) || n.Contains("textfield", StringComparison.Ordinal))
            return "input";
        if (n.Contains("card", StringComparison.Ordinal)) return "card";
        if (n.Contains("list", StringComparison.Ordinal) || n.Contains("row", StringComparison.Ordinal))
            return "list-item";
        if (n.Contains("modal", StringComparison.Ordinal) || n.Contains("dialog", StringComparison.Ordinal))
            return "modal";
        // Named social / external icons (vectors, instances) — after CTA so "Apply on LinkedIn" stays cta
        if (TryGetSemanticIconSlug(name) != null && IsIconGraphicOrComponentType(type))
            return "external_link";
        if (type == "TEXT") return "text";
        if (type == "VECTOR" || type == "BOOLEAN_OPERATION") return "graphic";
        return "container";
    }

    /// <summary>Layers that are typically a single icon glyph or icon component instance.</summary>
    private static bool IsIconGraphicOrComponentType(string type) =>
        type is "VECTOR"
            or "BOOLEAN_OPERATION"
            or "STAR"
            or "LINE"
            or "ELLIPSE"
            or "REGULAR_POLYGON"
            or "INSTANCE"
            or "COMPONENT";

    /// <summary>
    /// Stable slug from layer <paramref name="name"/> when it clearly references a known external/social affordance.
    /// Generic Figma names (Vector 102) return null — designers should rename for semantics.
    /// </summary>
    private static string? TryGetSemanticIconSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var n = name.ToLowerInvariant();

        // Specific substrings first (avoid false positives, e.g. "mail" inside unrelated words)
        if (n.Contains("linked-in", StringComparison.Ordinal) || n.Contains("linkedin", StringComparison.Ordinal))
            return "linkedin";
        if (n.Contains("github", StringComparison.Ordinal)) return "github";
        if (n.Contains("instagram", StringComparison.Ordinal)) return "instagram";
        if (n.Contains("facebook", StringComparison.Ordinal)) return "facebook";
        if (n.Contains("twitter", StringComparison.Ordinal) || n.Contains("x.com", StringComparison.Ordinal))
            return "twitter";
        if (n.Contains("youtube", StringComparison.Ordinal)) return "youtube";
        if (n.Contains("medium", StringComparison.Ordinal)) return "medium";
        if (n.Contains("dribbble", StringComparison.Ordinal)) return "dribbble";
        if (n.Contains("behance", StringComparison.Ordinal)) return "behance";
        if (n.Contains("slack", StringComparison.Ordinal)) return "slack";
        if (n.Contains("discord", StringComparison.Ordinal)) return "discord";

        if (n.Contains("mailto", StringComparison.Ordinal)
            || n.Contains("envelope", StringComparison.Ordinal)
            || n.Contains("e-mail", StringComparison.Ordinal)
            || n.Contains("email_icon", StringComparison.Ordinal)
            || n.Contains("mail_icon", StringComparison.Ordinal))
            return "email";

        if (n.Contains("open-in-new", StringComparison.Ordinal)
            || n.Contains("open_in_new", StringComparison.Ordinal)
            || n.Contains("external-link", StringComparison.Ordinal)
            || n.Contains("external_link", StringComparison.Ordinal)
            || n.Contains("arrow-up-right", StringComparison.Ordinal)
            || n.Contains("arrow_up_right", StringComparison.Ordinal)
            || n.Contains("new-window", StringComparison.Ordinal)
            || n.Contains("new_window", StringComparison.Ordinal)
            || n.Contains("outbound", StringComparison.Ordinal))
            return "open_external";

        if (n.Contains("globe", StringComparison.Ordinal)
            || n.Contains("website", StringComparison.Ordinal)
            || n.Contains("www.", StringComparison.Ordinal)
            || n.Contains("world wide", StringComparison.Ordinal))
            return "website";

        return null;
    }

    private static string? InferIconHint(string name, string type)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("icon", StringComparison.Ordinal) || n.Contains("glyph", StringComparison.Ordinal) || n.Contains("ico_", StringComparison.Ordinal))
            return name.Trim();
        if (type == "VECTOR" && name.Length > 0 && name.Length < 48)
            return name.Trim();
        return null;
    }

    private static string? InferVisualToken(JsonObject node)
    {
        if (node["boundVariables"] is JsonObject bv && bv.Count > 0)
            return "variable-bound";

        if (node["fills"] is not JsonArray fills || fills.Count == 0)
            return null;

        foreach (var f in fills)
        {
            if (f is not JsonObject fo) continue;
            if (fo["boundVariables"] is JsonObject b && b.Count > 0)
                return "variable-bound-fill";

            var fn = fo["name"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(fn))
                return $"fill-style:{fn}";

            if (fo["type"]?.GetValue<string>() == "SOLID" && fo["color"] is JsonObject c &&
                c["r"] is JsonValue rv && c["g"] is JsonValue gv && c["b"] is JsonValue bv2 &&
                rv.TryGetValue<double>(out var r) && gv.TryGetValue<double>(out var g) && bv2.TryGetValue<double>(out var bch))
                return RoughColorToken(r, g, bch);
        }

        return "fill-present";
    }

    private static string RoughColorToken(double r, double g, double b)
    {
        if (r > 0.85 && g > 0.85 && b > 0.85) return "neutral-light";
        if (r < 0.15 && g < 0.15 && b < 0.15) return "neutral-dark";
        if (g > r + 0.12 && g > b + 0.12) return "green-success-ish";
        if (r > g + 0.12 && r > b + 0.12) return "red-danger-ish";
        if (b > r + 0.1 && b > g + 0.1) return "blue-brand-ish";
        return "accent-fill";
    }
}
