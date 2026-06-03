using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace strAppersBackend.Utilities;

/// <summary>Renders a donut pie chart (passed/total adherence criteria) for Metrics Adherence.</summary>
public static class AdherenceChartRenderer
{
    private const int W = 300;
    private const int H = 300;
    private const float Cx = W / 2f;
    private const float Cy = H / 2f;
    private const float OuterR = 100f;
    private const float InnerR = 52f;

    private static readonly Color DoneColor = Color.FromRgb(46, 125, 50);   // green
    private static readonly Color GapColor  = Color.FromRgb(198, 40, 40);   // red
    private static readonly Color TrackColor = Color.FromRgb(220, 220, 220); // light gray

    public static byte[] RenderPng(int done, int total)
    {
        using var img = new Image<Rgba32>(W, H);
        img.Mutate(ctx => ctx.Fill(Color.White));

        var titleFont = SystemFonts.CreateFont("Arial", 12, FontStyle.Bold);
        var countFont = SystemFonts.CreateFont("Arial", 22, FontStyle.Bold);
        var subFont   = SystemFonts.CreateFont("Arial", 10);

        // Title
        img.Mutate(ctx => ctx.DrawText("Adherence criteria", titleFont, Color.FromRgb(25, 25, 112), new PointF(14, 10)));

        if (total <= 0)
        {
            // Gray full circle — no criteria
            img.Mutate(ctx => ctx.Fill(TrackColor, new EllipsePolygon(Cx, Cy, OuterR)));
            img.Mutate(ctx => ctx.Fill(Color.White, new EllipsePolygon(Cx, Cy, InnerR)));
            img.Mutate(ctx => ctx.DrawText("—", countFont, Color.FromRgb(100, 100, 100), new PointF(Cx - 10f, Cy - 14f)));
            img.Mutate(ctx => ctx.DrawText("No criteria set", subFont, Color.FromRgb(100, 100, 100), new PointF(Cx - 38f, Cy + 6f)));
        }
        else if (done >= total)
        {
            // Full green circle
            img.Mutate(ctx => ctx.Fill(DoneColor, new EllipsePolygon(Cx, Cy, OuterR)));
            img.Mutate(ctx => ctx.Fill(Color.White, new EllipsePolygon(Cx, Cy, InnerR)));
            DrawCenterText(img, done, total, countFont, subFont);
        }
        else if (done <= 0)
        {
            // Full red circle
            img.Mutate(ctx => ctx.Fill(GapColor, new EllipsePolygon(Cx, Cy, OuterR)));
            img.Mutate(ctx => ctx.Fill(Color.White, new EllipsePolygon(Cx, Cy, InnerR)));
            DrawCenterText(img, done, total, countFont, subFont);
        }
        else
        {
            // Background (gap = red full circle)
            img.Mutate(ctx => ctx.Fill(GapColor, new EllipsePolygon(Cx, Cy, OuterR)));
            // Done slice (green), starting from top (-90°)
            var sweepDeg = 360f * done / total;
            img.Mutate(ctx => ctx.Fill(DoneColor, BuildPieSlice(Cx, Cy, OuterR, -90f, sweepDeg)));
            // Donut hole
            img.Mutate(ctx => ctx.Fill(Color.White, new EllipsePolygon(Cx, Cy, InnerR)));
            DrawCenterText(img, done, total, countFont, subFont);
        }

        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static void DrawCenterText(Image<Rgba32> img, int done, int total, Font countFont, Font subFont)
    {
        var pct = total > 0 ? $"{(int)Math.Round(100.0 * done / total)}%" : "—";
        var sub = $"{done}/{total} passed";
        // Approximate centering
        img.Mutate(ctx => ctx.DrawText(pct, countFont, Color.FromRgb(25, 25, 112), new PointF(Cx - pct.Length * 7f, Cy - 16f)));
        img.Mutate(ctx => ctx.DrawText(sub, subFont, Color.FromRgb(66, 66, 66), new PointF(Cx - sub.Length * 3.2f, Cy + 6f)));
    }

    private static IPath BuildPieSlice(float cx, float cy, float r, float startDeg, float sweepDeg)
    {
        var steps = Math.Max(3, (int)(Math.Abs(sweepDeg) / 2f));
        var pts = new PointF[steps + 2];
        pts[0] = new PointF(cx, cy);
        for (int i = 0; i <= steps; i++)
        {
            var rad = (startDeg + sweepDeg * i / steps) * MathF.PI / 180f;
            pts[i + 1] = new PointF(cx + r * MathF.Cos(rad), cy + r * MathF.Sin(rad));
        }
        return new Polygon(new LinearLineSegment(pts));
    }

    public static string ToBase64Png(byte[] pngBytes) => Convert.ToBase64String(pngBytes);
}
