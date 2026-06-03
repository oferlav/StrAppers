using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace strAppersBackend.Utilities;

/// <summary>
/// Renders a split horizontal bar (green = passed, red = failed) for Metrics Adherence.
/// Chosen over a pie because the data is a small set of binary criteria, not proportional composition.
/// </summary>
public static class AdherenceChartRenderer
{
    private const int W = 460;
    private const int H = 120;
    private const int Pad = 16;
    private const int BarH = 28;
    private const int BarTop = 52;

    private static readonly Color PassColor  = Color.FromRgb(46, 125, 50);   // green
    private static readonly Color FailColor  = Color.FromRgb(198, 40, 40);   // red
    private static readonly Color EmptyColor = Color.FromRgb(236, 239, 241); // gray

    public static byte[] RenderPng(int done, int total)
    {
        using var img = new Image<Rgba32>(W, H);
        img.Mutate(ctx => ctx.Fill(Color.White));

        var titleFont = SystemFonts.CreateFont("Arial", 13, FontStyle.Bold);
        var subFont   = SystemFonts.CreateFont("Arial", 10);

        img.Mutate(ctx => ctx.DrawText("Adherence criteria", titleFont, Color.FromRgb(25, 25, 112), new PointF(Pad, 12)));

        var trackW = W - 2 * Pad;

        if (total <= 0)
        {
            img.Mutate(ctx =>
            {
                ctx.Fill(EmptyColor, new Rectangle(Pad, BarTop, trackW, BarH));
                ctx.DrawText("No required criteria set for this sprint.", subFont, Color.FromRgb(100, 100, 100), new PointF(Pad, 32));
            });
        }
        else
        {
            var failed = total - done;
            var sub = $"{done} of {total} criteria passed";
            img.Mutate(ctx => ctx.DrawText(sub, subFont, Color.FromRgb(66, 66, 66), new PointF(Pad, 32)));

            // Gray background track
            img.Mutate(ctx => ctx.Fill(EmptyColor, new Rectangle(Pad, BarTop, trackW, BarH)));

            // Green (passed) from left
            if (done > 0)
            {
                var passW = Math.Max(2, (int)Math.Round(trackW * (double)done / total));
                passW = Math.Min(passW, trackW);
                img.Mutate(ctx => ctx.Fill(PassColor, new Rectangle(Pad, BarTop, passW, BarH)));
            }

            // Red (failed) from right
            if (failed > 0)
            {
                var failW = Math.Max(2, (int)Math.Round(trackW * (double)failed / total));
                failW = Math.Min(failW, trackW);
                img.Mutate(ctx => ctx.Fill(FailColor, new Rectangle(Pad + trackW - failW, BarTop, failW, BarH)));
            }

            // Legend dots below bar
            var legendY = BarTop + BarH + 8;
            var legendFont = SystemFonts.CreateFont("Arial", 9);
            if (done > 0)
            {
                img.Mutate(ctx =>
                {
                    ctx.Fill(PassColor, new Rectangle(Pad, legendY + 2, 8, 8));
                    ctx.DrawText("Passed", legendFont, Color.FromRgb(66, 66, 66), new PointF(Pad + 12, legendY));
                });
            }
            if (failed > 0)
            {
                img.Mutate(ctx =>
                {
                    ctx.Fill(FailColor, new Rectangle(Pad + 70, legendY + 2, 8, 8));
                    ctx.DrawText("Failed", legendFont, Color.FromRgb(66, 66, 66), new PointF(Pad + 82, legendY));
                });
            }
        }

        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    public static string ToBase64Png(byte[] pngBytes) => Convert.ToBase64String(pngBytes);
}
