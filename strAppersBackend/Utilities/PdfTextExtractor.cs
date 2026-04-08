using System.Text;
using UglyToad.PdfPig;

namespace strAppersBackend.Utilities;

/// <summary>Best-effort PDF text for mentor prompts (no OCR).</summary>
public static class PdfTextExtractor
{
    public static string? TryExtractText(ReadOnlyMemory<byte> pdfBytes, int maxChars)
    {
        if (pdfBytes.Length == 0 || maxChars <= 0) return null;
        try
        {
            using var ms = new MemoryStream(pdfBytes.ToArray(), writable: false);
            using var doc = PdfDocument.Open(ms);
            var sb = new StringBuilder();
            foreach (var page in doc.GetPages())
            {
                sb.AppendLine(page.Text);
                if (sb.Length >= maxChars)
                    break;
            }

            var t = sb.ToString().Trim();
            if (string.IsNullOrEmpty(t)) return null;
            if (t.Length > maxChars)
                return t[..maxChars] + "\n\n… [truncated]";
            return t;
        }
        catch
        {
            return null;
        }
    }
}
