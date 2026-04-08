using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace strAppersBackend.Utilities;

/// <summary>
/// Shrinks and re-encodes mentor review images before inline vision payloads (token/cost control).
/// </summary>
public static class MentorVisionImagePreparer
{
    public const int DefaultMaxWidthPx = 1024;
    public const int DefaultJpegQuality = 85;

    /// <summary>
    /// Resizes to max width (never upscales). PNG/WebP/GIF without partial transparency → JPEG;
    /// JPEG stays JPEG (re-encoded). Other raster types → PNG if not sent as JPEG.
    /// On failure, returns original bytes and <paramref name="outputMime"/> equal to <paramref name="declaredMime"/>.
    /// </summary>
    public static (byte[] Bytes, string OutputMime, string DetailNote) PrepareForVision(
        ReadOnlyMemory<byte> source,
        string declaredMime,
        int maxWidthPx = DefaultMaxWidthPx,
        int jpegQuality = DefaultJpegQuality)
    {
        if (source.Length == 0)
            return (Array.Empty<byte>(), declaredMime, "");

        try
        {
            using var image = Image.Load(source.Span);

            var origW = image.Width;
            var origH = image.Height;
            if (image.Width > maxWidthPx)
            {
                var newH = Math.Max(1, (int)Math.Round(image.Height * (maxWidthPx / (double)image.Width)));
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxWidthPx, newH),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.Lanczos3,
                }));
            }

            var mime = (declaredMime ?? "").Split(';')[0].Trim().ToLowerInvariant();

            var hasPartialAlpha = false;
            if (!mime.StartsWith("image/jpeg", StringComparison.OrdinalIgnoreCase))
            {
                using var rgba = image.CloneAs<Rgba32>();
                hasPartialAlpha = HasPartialTransparency(rgba);
            }

            var useJpeg = ShouldUseJpegEncoding(mime, hasPartialAlpha);

            using var ms = new MemoryStream();
            if (useJpeg)
            {
                image.SaveAsJpeg(ms, new JpegEncoder { Quality = jpegQuality });
                var note = BuildNote(origW, origH, image.Width, image.Height, true);
                return (ms.ToArray(), "image/jpeg", note);
            }

            image.SaveAsPng(ms, new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression });
            var notePng = BuildNote(origW, origH, image.Width, image.Height, false);
            return (ms.ToArray(), "image/png", notePng);
        }
        catch
        {
            return (source.ToArray(), declaredMime, "Vision image optimize skipped (decode failed); original bytes used.");
        }
    }

    private static bool ShouldUseJpegEncoding(string mimeLower, bool hasPartialAlpha)
    {
        if (hasPartialAlpha)
            return false;
        if (mimeLower.StartsWith("image/jpeg", StringComparison.OrdinalIgnoreCase))
            return true;
        if (mimeLower == "image/png" || mimeLower == "image/webp" || mimeLower == "image/gif")
            return true;
        return false;
    }

    private static bool HasPartialTransparency(Image<Rgba32> rgba)
    {
        var found = false;
        rgba.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height && !found; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].A < byte.MaxValue)
                    {
                        found = true;
                        return;
                    }
                }
            }
        });
        return found;
    }

    private static string BuildNote(int origW, int origH, int outW, int outH, bool asJpeg)
    {
        var parts = new List<string>();
        if (origW != outW || origH != outH)
            parts.Add($"resized {origW}×{origH} → {outW}×{outH}");
        parts.Add(asJpeg ? "JPEG" : "PNG");
        return "Vision optimize: " + string.Join("; ", parts) + ".";
    }
}
