using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;

namespace strAppersBackend.Helpers;

/// <summary>
/// Shrinks student profile photos (base64 or data-URL) so stored payload is at most 2 MiB.
/// HTTP(S) URLs are returned unchanged.
/// </summary>
public static class StudentPhotoHelper
{
    private const long MaxPhotoBytes = 2L * 1024 * 1024; // 2 MB
    private const int InitialMaxDimension = 2048;
    private const int MinDimension = 320;

    /// <summary>
    /// If <paramref name="photo"/> is a base64 image or data-URL image larger than 2 MB, re-encodes as JPEG
    /// with reduced dimensions and/or quality until under the limit.
    /// </summary>
    public static string? CompressStudentPhotoIfNeeded(string? photo, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(photo))
        {
            logger?.LogInformation("Student photo compression skipped: empty photo value.");
            return photo;
        }

        var trimmed = photo.Trim();

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogInformation("Student photo compression skipped: photo is URL.");
            return trimmed;
        }

        if (!TryParseBase64Image(trimmed, out var rawBytes, out var hadDataUri))
        {
            logger?.LogInformation("Student photo compression skipped: photo is not valid base64 image payload.");
            return trimmed;
        }

        if (rawBytes.Length <= MaxPhotoBytes)
        {
            logger?.LogInformation(
                "Student photo compression not needed: decoded size {OriginalBytes} bytes <= {MaxBytes} bytes.",
                rawBytes.Length,
                MaxPhotoBytes);
            return trimmed;
        }

        logger?.LogInformation(
            "Student photo compression started: decoded size {OriginalBytes} bytes (max {MaxBytes}). DataUri={HadDataUri}.",
            rawBytes.Length,
            MaxPhotoBytes,
            hadDataUri);

        try
        {
            using var inputMs = new MemoryStream(rawBytes, writable: false);
            using var image = Image.FromStream(inputMs);

            var outBytes = CompressImageUnderLimit(image, logger);
            if (outBytes == null || outBytes.Length > MaxPhotoBytes)
            {
                logger?.LogWarning("Student photo could not be compressed to at most {MaxBytes} bytes; storing original.", MaxPhotoBytes);
                return trimmed;
            }

            logger?.LogInformation(
                "Student photo compressed successfully: {OriginalBytes} -> {CompressedBytes} bytes (saved {SavedBytes} bytes, {SavedPercent:F1}%).",
                rawBytes.Length,
                outBytes.Length,
                rawBytes.Length - outBytes.Length,
                ((double)(rawBytes.Length - outBytes.Length) / rawBytes.Length) * 100d);

            var b64 = Convert.ToBase64String(outBytes);
            return hadDataUri
                ? $"data:image/jpeg;base64,{b64}"
                : b64;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Student photo compression skipped (invalid image or decode error); storing original.");
            return trimmed;
        }
    }

    private static byte[]? CompressImageUnderLimit(Image original, ILogger? logger)
    {
        var maxDim = InitialMaxDimension;
        long quality = 88L;

        using var work = new Bitmap(original);
        logger?.LogInformation(
            "Compression loop started for image {Width}x{Height}. InitialMaxDimension={InitialMaxDimension}, MinDimension={MinDimension}.",
            work.Width,
            work.Height,
            InitialMaxDimension,
            MinDimension);

        for (var round = 0; round < 40; round++)
        {
            using var scaled = ResizeToMaxDimension(work, maxDim);
            var jpeg = EncodeJpeg(scaled, quality);
            if (jpeg.Length <= MaxPhotoBytes)
                return jpeg;

            if (quality > 35L)
            {
                quality -= 8L;
                continue;
            }

            if (maxDim > MinDimension)
            {
                maxDim = Math.Max(MinDimension, (int)(maxDim * 0.85));
                quality = 82L;
                continue;
            }

            // Last resort: lowest quality at minimum dimension
            var lastTry = EncodeJpeg(scaled, 20L);
            if (lastTry.Length <= MaxPhotoBytes)
                return lastTry;

            logger?.LogWarning("JPEG at min dimension still exceeds {MaxBytes} bytes.", MaxPhotoBytes);
            return null;
        }

        logger?.LogWarning("Student photo compression loop reached max rounds without success.");
        return null;
    }

    private static Bitmap ResizeToMaxDimension(Image source, int maxDimension)
    {
        var w = source.Width;
        var h = source.Height;
        if (w <= 0 || h <= 0)
            return new Bitmap(source);

        if (w <= maxDimension && h <= maxDimension)
            return new Bitmap(source);

        var scale = maxDimension / (double)Math.Max(w, h);
        var nw = Math.Max(1, (int)Math.Round(w * scale));
        var nh = Math.Max(1, (int)Math.Round(h * scale));

        var bmp = new Bitmap(nw, nh);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(source, 0, 0, nw, nh);
        }

        return bmp;
    }

    private static byte[] EncodeJpeg(Image image, long quality)
    {
        using var ms = new MemoryStream();
        var jpegCodec = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
        if (jpegCodec == null)
        {
            image.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }

        using var encParams = new EncoderParameters(1);
        encParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        image.Save(ms, jpegCodec, encParams);
        return ms.ToArray();
    }

    private static bool TryParseBase64Image(string trimmed, out byte[] rawBytes, out bool hadDataUri)
    {
        rawBytes = Array.Empty<byte>();
        hadDataUri = false;

        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            hadDataUri = true;
            var comma = trimmed.IndexOf(',');
            if (comma < 0)
                return false;

            var header = trimmed[..comma];
            if (!header.Contains("base64", StringComparison.OrdinalIgnoreCase))
                return false;

            var b64 = trimmed[(comma + 1)..].Trim();
            try
            {
                rawBytes = Convert.FromBase64String(b64);
                return true;
            }
            catch
            {
                return false;
            }
        }

        try
        {
            rawBytes = Convert.FromBase64String(trimmed);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
