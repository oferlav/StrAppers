using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace strAppersBackend.Utilities;

/// <summary>
/// Renders a per-criterion scorecard (✓/✗ per item) for Metrics Adherence.
/// Each criterion gets its own coloured row so reviewers see exactly which checks passed or failed.
/// </summary>
public static class AdherenceChartRenderer
{
    private const int W = 460;
    private const int HeaderH = 44;
    private const int RowH = 36;
    private const int DotR = 8;
    private const int Pad = 16;

    private static readonly Color HeaderBg   = Color.FromRgb(25, 25, 112);   // dark blue
    private static readonly Color PassBg     = Color.FromRgb(232, 245, 233); // light green
    private static readonly Color FailBg     = Color.FromRgb(255, 235, 238); // light red
    private static readonly Color PassDot    = Color.FromRgb(46, 125, 50);   // green
    private static readonly Color FailDot    = Color.FromRgb(198, 40, 40);   // red
    private static readonly Color PassLabel  = Color.FromRgb(27, 94, 32);
    private static readonly Color FailLabel  = Color.FromRgb(183, 28, 28);
    private static readonly Color DividerColor = Color.FromRgb(200, 200, 200);

    public static byte[] RenderPng(IReadOnlyList<(string Name, bool Passed)> criteria)
    {
        var rowCount = Math.Max(1, criteria.Count);
        var h = HeaderH + rowCount * RowH + 1; // +1 for bottom border

        using var img = new Image<Rgba32>(W, h);
        img.Mutate(ctx => ctx.Fill(Color.White));

        var headerFont  = SystemFonts.CreateFont("Arial", 13, FontStyle.Bold);
        var subFont     = SystemFonts.CreateFont("Arial", 10);
        var rowFont     = SystemFonts.CreateFont("Arial", 12);
        var labelFont   = SystemFonts.CreateFont("Arial", 10, FontStyle.Bold);

        // Header
        img.Mutate(ctx => ctx.Fill(HeaderBg, new Rectangle(0, 0, W, HeaderH)));

        if (criteria.Count == 0)
        {
            img.Mutate(ctx => ctx.DrawText("Adherence criteria", headerFont, Color.White, new PointF(Pad, 10)));
            img.Mutate(ctx => ctx.DrawText("No required criteria set for this sprint.", subFont, Color.FromRgb(180, 180, 220), new PointF(Pad, 28)));
        }
        else
        {
            var passed = criteria.Count(c => c.Passed);
            var pct = (int)Math.Round(100.0 * passed / criteria.Count);
            img.Mutate(ctx => ctx.DrawText("Adherence criteria", headerFont, Color.White, new PointF(Pad, 8)));
            img.Mutate(ctx => ctx.DrawText($"{pct}% passed  ({passed}/{criteria.Count})", subFont, Color.FromRgb(180, 220, 255), new PointF(Pad, 27)));

            for (int i = 0; i < criteria.Count; i++)
            {
                var (name, passed_) = criteria[i];
                var rowY = HeaderH + i * RowH;

                // Row background
                img.Mutate(ctx => ctx.Fill(passed_ ? PassBg : FailBg, new Rectangle(0, rowY, W, RowH)));

                // Divider line above row
                img.Mutate(ctx => ctx.Fill(DividerColor, new Rectangle(0, rowY, W, 1)));

                // Coloured dot
                var dotCx = Pad + DotR;
                var dotCy = rowY + RowH / 2f;
                img.Mutate(ctx => ctx.Fill(passed_ ? PassDot : FailDot, new EllipsePolygon(dotCx, dotCy, DotR)));

                // Criterion name
                img.Mutate(ctx => ctx.DrawText(name, rowFont, Color.FromRgb(30, 30, 30), new PointF(Pad + DotR * 2 + 8, rowY + (RowH - 12) / 2f)));

                // PASS / FAIL label (right-aligned)
                var tag = passed_ ? "PASS" : "FAIL";
                var tagColor = passed_ ? PassLabel : FailLabel;
                var tagX = W - Pad - tag.Length * 7f;
                img.Mutate(ctx => ctx.DrawText(tag, labelFont, tagColor, new PointF(tagX, rowY + (RowH - 10) / 2f)));
            }

            // Bottom border
            img.Mutate(ctx => ctx.Fill(DividerColor, new Rectangle(0, HeaderH + criteria.Count * RowH, W, 1)));
        }

        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    public static string ToBase64Png(byte[] pngBytes) => Convert.ToBase64String(pngBytes);
}
