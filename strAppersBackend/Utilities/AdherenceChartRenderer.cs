using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace strAppersBackend.Utilities;

/// <summary>Renders a sprint checklist completion bar (done / total items) for Metrics Adherence.</summary>
public static class AdherenceChartRenderer
{
    private const int W = 460;
    private const int H = 120;
    private const int Pad = 16;
    private const int BarH = 28;
    private const int BarTop = 52;

    public static byte[] RenderPng(int done, int total)
    {
        using var img = new Image<Rgba32>(W, H);
        img.Mutate(ctx => ctx.Fill(Color.White));

        var titleFont = SystemFonts.CreateFont("Arial", 13, FontStyle.Bold);
        var subFont = SystemFonts.CreateFont("Arial", 10);

        img.Mutate(ctx =>
        {
            ctx.DrawText("Sprint checklist completion", titleFont, Color.FromRgb(25, 25, 112), new PointF(Pad, 12));
            var sub = total <= 0
                ? "No checklist items found."
                : $"{done} of {total} item(s) completed";
            ctx.DrawText(sub, subFont, Color.FromRgb(66, 66, 66), new PointF(Pad, 32));
        });

        var trackW = W - 2 * Pad;
        var trackColor = Color.FromRgb(236, 239, 241);
        var fillColor = Color.FromRgb(46, 125, 50);

        img.Mutate(ctx =>
        {
            ctx.Fill(trackColor, new Rectangle(Pad, BarTop, trackW, BarH));
            if (total > 0 && done > 0)
            {
                var fillW = Math.Max(2, (int)Math.Round(trackW * (double)done / total));
                fillW = Math.Min(fillW, trackW);
                ctx.Fill(fillColor, new Rectangle(Pad, BarTop, fillW, BarH));
            }
        });

        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    public static string ToBase64Png(byte[] pngBytes) => Convert.ToBase64String(pngBytes);
}
