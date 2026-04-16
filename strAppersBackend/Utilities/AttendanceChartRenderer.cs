using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace strAppersBackend.Utilities;

/// <summary>Renders a single participation bar (attended / scheduled team meetings) for Metrics Attendance.</summary>
public static class AttendanceChartRenderer
{
    private const int W = 460;
    private const int H = 120;
    private const int Pad = 16;
    private const int BarH = 28;
    private const int BarTop = 52;

    public static byte[] RenderPng(int attended, int total)
    {
        using var img = new Image<Rgba32>(W, H);
        img.Mutate(ctx => ctx.Fill(Color.White));

        var titleFont = SystemFonts.CreateFont("Arial", 13, FontStyle.Bold);
        var subFont = SystemFonts.CreateFont("Arial", 10);

        img.Mutate(ctx =>
        {
            ctx.DrawText("Team meeting attendance", titleFont, Color.FromRgb(25, 25, 112), new PointF(Pad, 12));
            var sub = total <= 0
                ? "No meetings scheduled in this sprint window."
                : $"{attended} of {total} meeting(s) attended";
            ctx.DrawText(sub, subFont, Color.FromRgb(66, 66, 66), new PointF(Pad, 32));
        });

        var trackLeft = Pad;
        var trackW = W - 2 * Pad;
        var trackColor = Color.FromRgb(236, 239, 241);
        var fillColor = Color.FromRgb(21, 101, 192);

        img.Mutate(ctx =>
        {
            ctx.Fill(trackColor, new Rectangle(trackLeft, BarTop, trackW, BarH));
            if (total > 0 && attended > 0)
            {
                var fillW = Math.Max(2, (int)Math.Round(trackW * (double)attended / total));
                fillW = Math.Min(fillW, trackW);
                ctx.Fill(fillColor, new Rectangle(trackLeft, BarTop, fillW, BarH));
            }
        });

        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    public static string ToBase64Png(byte[] pngBytes) => Convert.ToBase64String(pngBytes);
}
