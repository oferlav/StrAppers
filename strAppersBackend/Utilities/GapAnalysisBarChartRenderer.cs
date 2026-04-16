using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace strAppersBackend.Utilities;

/// <summary>
/// Renders 0–100 bar charts for Gap Analysis from structured scores (not from the LLM).
/// Thin bars, distinct colors, legend below (no crowded labels under bars).
/// </summary>
public static class GapAnalysisBarChartRenderer
{
    private const int ChartWidth = 460;
    private const int Padding = 14;
    private const int BarPlotH = 152;
    private const int GapAfterBars = 10;
    private const int TopPad = 10;
    private const int SectionHeadlineH = 22;
    private const int GapScoresLineH = 22;
    private const int GapBeforeBarPlot = 8;

    private static readonly Color[] BarPalette =
    {
        Color.FromRgb(21, 101, 192),   // blue
        Color.FromRgb(46, 125, 50),    // green
        Color.FromRgb(245, 124, 0),    // orange
        Color.FromRgb(123, 31, 162),   // purple
        Color.FromRgb(198, 40, 40),    // red
        Color.FromRgb(0, 131, 143),    // teal
        Color.FromRgb(109, 76, 65),    // brown
        Color.FromRgb(0, 137, 123),    // cyan-green
    };

    /// <param name="sectionTitle">Optional headline above the chart (e.g. <c>Backend</c> / <c>Frontend</c>).</param>
    public static byte[] RenderSingleChart(IReadOnlyList<(string Label, int Score)> categories, string? sectionTitle = null)
    {
        using var img = RenderChartSurface(categories, sectionTitle);
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>Stacks two charts vertically → one tall PNG (Backend above, Frontend below).</summary>
    public static byte[] RenderStackedCharts(
        IReadOnlyList<(string Label, int Score)> backendCategories,
        IReadOnlyList<(string Label, int Score)> frontendCategories)
    {
        using var top = RenderChartSurface(backendCategories, "Backend");
        using var bottom = RenderChartSurface(frontendCategories, "Frontend");
        var h = top.Height + bottom.Height;
        using var combined = new Image<Rgba32>(ChartWidth, h);
        combined.Mutate(ctx =>
        {
            ctx.DrawImage(top, new Point(0, 0), 1f);
            ctx.DrawImage(bottom, new Point(0, top.Height), 1f);
        });
        using var ms = new MemoryStream();
        combined.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>Base64 PNG for <c>CacheMetrics.Graph</c> / <c>CacheMetrics.Graph2</c>.</summary>
    public static string ToBase64Png(byte[] pngBytes) => Convert.ToBase64String(pngBytes);

    private static int GetBarPlotTop(string? sectionTitle)
    {
        var t = TopPad;
        if (!string.IsNullOrWhiteSpace(sectionTitle))
            t += SectionHeadlineH;
        t += GapScoresLineH + GapBeforeBarPlot;
        return t;
    }

    /// <summary>Single shared baseline (bottom of bar plot); track and fills use the same pixel height so 0 and &gt;0 align.</summary>
    private static Image<Rgba32> RenderChartSurface(IReadOnlyList<(string Label, int Score)> categories, string? sectionTitle)
    {
        var legendFont = SystemFonts.CreateFont("Arial", 9);
        var legendWrapW = ChartWidth - 2 * Padding - 22f;
        var chartHeight = ComputeChartHeight(categories, legendFont, legendWrapW, sectionTitle);

        var surface = new Image<Rgba32>(ChartWidth, chartHeight);
        surface.Mutate(ctx => ctx.Fill(Color.White));

        if (categories == null || categories.Count == 0)
        {
            surface.Mutate(ctx => ctx.DrawText(
                "No scores",
                SystemFonts.CreateFont("Arial", 11),
                Color.Gray,
                new PointF(Padding, chartHeight / 2f)));
            return surface;
        }

        var barPlotTop = GetBarPlotTop(sectionTitle);
        var baselineY = barPlotTop + BarPlotH; // exclusive: fills end at this y (last row index = baselineY - 1)

        var yLine = (float)TopPad;
        if (!string.IsNullOrWhiteSpace(sectionTitle))
        {
            var headFont = SystemFonts.CreateFont("Arial", 13, FontStyle.Bold);
            surface.Mutate(ctx =>
                ctx.DrawText(sectionTitle.Trim(), headFont, Color.FromRgb(25, 25, 112), new PointF(Padding, yLine)));
            yLine += SectionHeadlineH;
        }

        var titleFont = SystemFonts.CreateFont("Arial", 11, FontStyle.Bold);
        surface.Mutate(ctx =>
            ctx.DrawText("Gap scores (0–100)", titleFont, Color.FromRgb(33, 33, 33), new PointF(Padding, yLine)));

        var n = categories.Count;
        var slotW = (ChartWidth - 2 * Padding) / (float)Math.Max(n, 1);
        var barW = Math.Min(22f, Math.Max(10f, slotW * 0.28f));
        var barWpx = Math.Max(10, (int)Math.Ceiling(barW));
        var trackColor = Color.FromRgb(236, 239, 241);

        for (var i = 0; i < n; i++)
        {
            var (label, score) = categories[i];
            var clamped = Math.Clamp(score, 0, 100);
            var fillH = 0;
            if (clamped > 0)
            {
                fillH = (int)Math.Ceiling(BarPlotH * clamped / 100.0);
                fillH = Math.Max(2, fillH);
                fillH = Math.Min(fillH, BarPlotH);
            }

            var centerX = Padding + (i + 0.5f) * slotW;
            var x0 = (int)(centerX - barWpx / 2f);
            var yFill = baselineY - fillH;
            var fill = BarPalette[i % BarPalette.Length];

            surface.Mutate(ctx =>
            {
                ctx.Fill(trackColor, new Rectangle(x0, barPlotTop, barWpx, BarPlotH));
                if (fillH > 0)
                    ctx.Fill(fill, new Rectangle(x0, yFill, barWpx, fillH));
            });

            if (clamped > 0)
            {
                var scoreFont = SystemFonts.CreateFont("Arial", 8, FontStyle.Bold);
                var scoreText = $"{clamped}";
                var sw = TextMeasurer.MeasureSize(scoreText, new TextOptions(scoreFont)).Width;
                var sx = centerX - sw / 2f;
                var sy = Math.Max(barPlotTop + 2, yFill - 13);
                surface.Mutate(ctx =>
                    ctx.DrawText(scoreText, scoreFont, Color.FromRgb(55, 55, 55), new PointF(sx, sy)));
            }
        }

        var axisFont = SystemFonts.CreateFont("Arial", 7);
        surface.Mutate(ctx =>
        {
            ctx.DrawText("0", axisFont, Color.Gray, new PointF(Math.Max(2, Padding - 2), baselineY - 11));
            ctx.DrawText("100", axisFont, Color.Gray, new PointF(Math.Max(2, Padding - 2), barPlotTop + 1));
        });

        float legendY = baselineY + GapAfterBars;
        var smallFont = SystemFonts.CreateFont("Arial", 9);
        var lineH = TextMeasurer.MeasureSize("Mg", new TextOptions(smallFont)).Height;

        for (var i = 0; i < n; i++)
        {
            var (label, _) = categories[i];
            var text = string.IsNullOrWhiteSpace(label) ? "—" : label.Trim();
            var fill = BarPalette[i % BarPalette.Length];
            const int swatch = 11;
            var textX = Padding + swatch + 8;

            surface.Mutate(ctx =>
                ctx.Fill(fill, new Rectangle(Padding, (int)legendY + 1, swatch, swatch)));

            var lines = WrapLabelToColumn(text, smallFont, legendWrapW, maxLines: 6);
            float yy = legendY;
            foreach (var line in lines)
            {
                surface.Mutate(ctx =>
                    ctx.DrawText(line, smallFont, Color.FromRgb(40, 40, 40), new PointF(textX, yy)));
                yy += lineH;
            }

            legendY = yy + 8;
        }

        return surface;
    }

    private static int ComputeChartHeight(
        IReadOnlyList<(string Label, int Score)> categories,
        Font legendFont,
        float legendWrapW,
        string? sectionTitle)
    {
        if (categories == null || categories.Count == 0)
            return 200;

        var lineH = TextMeasurer.MeasureSize("Mg", new TextOptions(legendFont)).Height;
        float legendH = GapAfterBars + 4;
        foreach (var (label, _) in categories)
        {
            var text = string.IsNullOrWhiteSpace(label) ? "—" : label.Trim();
            var lines = WrapLabelToColumn(text, legendFont, legendWrapW, maxLines: 6);
            legendH += lines.Count * lineH + 8;
        }

        return GetBarPlotTop(sectionTitle) + BarPlotH + (int)Math.Ceiling(legendH) + Padding;
    }

    private static List<string> WrapLabelToColumn(string text, Font font, float maxWidth, int maxLines)
    {
        var lines = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            lines.Add("—");
            return lines;
        }

        var line = "";
        foreach (var w in words)
        {
            var trial = string.IsNullOrEmpty(line) ? w : line + " " + w;
            var tw = TextMeasurer.MeasureSize(trial, new TextOptions(font)).Width;
            if (tw <= maxWidth)
            {
                line = trial;
                continue;
            }

            if (!string.IsNullOrEmpty(line))
            {
                lines.Add(line);
                line = w;
            }
            else
            {
                lines.Add(TruncateWithEllipsis(w, font, maxWidth));
                line = "";
            }

            if (lines.Count >= maxLines)
                goto Finish;
        }

        if (!string.IsNullOrEmpty(line) && lines.Count < maxLines)
            lines.Add(line);

        Finish:
        if (lines.Count == 0)
            lines.Add(TruncateWithEllipsis(text, font, maxWidth));
        return lines;
    }

    private static string TruncateWithEllipsis(string word, Font font, float maxWidth)
    {
        if (TextMeasurer.MeasureSize(word, new TextOptions(font)).Width <= maxWidth)
            return word;
        const string ell = "…";
        var t = word;
        while (t.Length > 1 && TextMeasurer.MeasureSize(t + ell, new TextOptions(font)).Width > maxWidth)
            t = t[..^1];
        return t + ell;
    }
}
