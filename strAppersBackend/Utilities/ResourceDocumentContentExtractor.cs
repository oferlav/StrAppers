using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;

namespace strAppersBackend.Utilities;

/// <summary>
/// Builds mentor-prompt text or base64 payloads from uploaded resource streams (Office via Open XML, plain text, images/PDF as base64).
/// </summary>
public static class ResourceDocumentContentExtractor
{
    public const int MaxTextCharsInPrompt = 200_000;

    /// <summary>True for image/* and PDF — resource-review loads these on the server (vision data URL or PdfPig text), not as client-fetch SAS in the prompt.</summary>
    public static bool ShouldDeliverBinaryViaSasUrl(string contentType, string fileName)
    {
        var mime = (contentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        var ext = System.IO.Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (mime == "application/octet-stream" || string.IsNullOrEmpty(mime))
            mime = GuessMimeFromExtension(ext);
        return mime.StartsWith("image/", StringComparison.Ordinal) || mime == "application/pdf";
    }

    public static string NormalizeMimeForDisplay(string contentType, string fileName)
    {
        var mime = (contentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        var ext = System.IO.Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (mime == "application/octet-stream" || string.IsNullOrEmpty(mime))
            mime = GuessMimeFromExtension(ext);
        return string.IsNullOrEmpty(mime) ? "application/octet-stream" : mime;
    }

    public sealed record AttachmentPayload(
        string Mode,
        string MimeType,
        string? TextBody,
        string? Base64Body,
        string Note);

    public static async Task<AttachmentPayload> BuildAsync(
        Stream input,
        string contentType,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var mime = (contentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        var ext = System.IO.Path.GetExtension(fileName ?? "").ToLowerInvariant();

        await using var ms = new MemoryStream();
        await input.CopyToAsync(ms, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = ms.ToArray();

        if (bytes.Length == 0)
            return new AttachmentPayload("empty", mime, null, null, "File is empty.");

        if (mime == "application/octet-stream" || string.IsNullOrEmpty(mime))
            mime = GuessMimeFromExtension(ext);

        if (IsPlainTextMime(mime) || IsPlainTextExtension(ext))
            return BuildTextFromUtf8(bytes, mime);

        if (mime.StartsWith("image/", StringComparison.Ordinal) || mime == "application/pdf")
            return new AttachmentPayload("unsupported", mime, null, null,
                "Images and PDF are not embedded as base64; the API supplies a short-lived read-only SAS URL instead.");

        if (ext == ".docx" || mime == "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
            return ExtractDocx(bytes);

        if (ext == ".xlsx" || mime == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            return ExtractXlsx(bytes);

        if (ext == ".pptx" || mime == "application/vnd.openxmlformats-officedocument.presentationml.presentation")
            return ExtractPptx(bytes);

        if (ext == ".doc")
            return new AttachmentPayload("unsupported", mime, null, null,
                "Legacy .doc (binary) is not automatically converted. Ask the student to upload .docx or PDF.");

        var asTextTry = TryDecodeAsPrintableText(bytes, mime);
        if (asTextTry != null)
            return asTextTry;

        return new AttachmentPayload("unsupported", mime, null, null,
            $"No extractor for type '{mime}' / '{ext}'. Supported: images, PDF (base64), .txt/.md/.csv/.json/.xml, .docx, .xlsx, .pptx.");
    }

    private static AttachmentPayload BuildTextFromUtf8(byte[] bytes, string mime)
    {
        string text;
        try
        {
            text = Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            text = Encoding.Default.GetString(bytes);
        }

        if (text.Length > MaxTextCharsInPrompt)
        {
            text = text[..MaxTextCharsInPrompt] +
                   $"\n\n… [truncated: document had more than {MaxTextCharsInPrompt} characters]";
        }

        return new AttachmentPayload("text", mime, text, null, "");
    }

    private static AttachmentPayload? TryDecodeAsPrintableText(byte[] bytes, string mime)
    {
        if (bytes.Length > MaxTextCharsInPrompt * 4)
            return null;
        string s;
        try
        {
            s = Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }

        var printable = 0;
        foreach (var c in s)
        {
            if (!char.IsControl(c) || c is '\r' or '\n' or '\t')
                printable++;
        }

        if (s.Length > 0 && printable * 10 < s.Length * 7)
            return null;

        if (s.Length > MaxTextCharsInPrompt)
            s = s[..MaxTextCharsInPrompt] + "\n\n… [truncated]";
        return new AttachmentPayload("text", mime, s, null, "Decoded as UTF-8 text (heuristic).");
    }

    private static AttachmentPayload ExtractDocx(byte[] bytes)
    {
        const string mime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            using var doc = WordprocessingDocument.Open(ms, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null)
                return new AttachmentPayload("empty", mime, null, null,
                    "The Word file has no document body (corrupt or empty package). Try re-saving as .docx or export PDF.");

            var sb = new StringBuilder();
            // Includes paragraphs inside tables (TableCell → Paragraph).
            foreach (var p in body.Descendants<Paragraph>())
            {
                var t = p.InnerText;
                if (!string.IsNullOrWhiteSpace(t))
                    sb.AppendLine(t.TrimEnd());
            }

            var text = sb.ToString().Trim();
            // Text boxes / some layouts: all run text in body
            if (text.Length == 0)
            {
                var runs = new StringBuilder();
                foreach (var tx in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                {
                    if (!string.IsNullOrEmpty(tx.Text))
                        runs.Append(tx.Text);
                }

                text = runs.ToString().Trim();
                if (text.Length > 0)
                {
                    // Roughly separate words that were concatenated without spaces
                    text = System.Text.RegularExpressions.Regex.Replace(text, @"(\p{Ll})(\p{Lu})", "$1 $2");
                }
            }

            if (string.IsNullOrWhiteSpace(text))
                return new AttachmentPayload("empty", mime, null, null,
                    "No extractable text in this Word file (image-only pages, embedded pictures, or scanned content). Export as PDF with text, or paste the text.");

            if (text.Length > MaxTextCharsInPrompt)
                text = text[..MaxTextCharsInPrompt] + $"\n\n… [truncated after {MaxTextCharsInPrompt} chars]";
            return new AttachmentPayload("text", mime, text, null, "Extracted on server from .docx (paragraphs, tables, and run text).");
        }
        catch (Exception ex)
        {
            return new AttachmentPayload("unsupported", mime, null, null,
                $"Could not read .docx: {ex.Message}");
        }
    }

    private static AttachmentPayload ExtractXlsx(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            using var doc = SpreadsheetDocument.Open(ms, false);
            var sb = new StringBuilder();
            var workbookPart = doc.WorkbookPart;
            if (workbookPart == null)
                return new AttachmentPayload("text", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "(empty workbook)", null, "");

            var sst = workbookPart.SharedStringTablePart?.SharedStringTable;
            string ResolveShared(string? raw)
            {
                if (string.IsNullOrEmpty(raw) || sst == null) return raw ?? "";
                if (!int.TryParse(raw, out var idx) || idx < 0) return raw;
                var items = sst.Elements<SharedStringItem>().ToList();
                if (idx >= items.Count) return raw;
                return items[idx].InnerText ?? raw;
            }

            foreach (var sheet in workbookPart.Workbook.Descendants<Sheet>())
            {
                var name = sheet.Name?.Value ?? "Sheet";
                var id = sheet.Id?.Value;
                if (string.IsNullOrEmpty(id)) continue;
                if (workbookPart.GetPartById(id) is not WorksheetPart wsp) continue;
                var sheetData = wsp.Worksheet?.GetFirstChild<SheetData>();
                if (sheetData == null) continue;
                sb.AppendLine($"## {name}");
                foreach (var row in sheetData.Elements<Row>())
                {
                    var parts = new List<string>();
                    foreach (var cell in row.Elements<Cell>())
                    {
                        var v = cell.CellValue?.Text ?? cell.InnerText;
                        if (cell.DataType?.Value == CellValues.SharedString)
                            v = ResolveShared(v);
                        parts.Add(v?.Trim() ?? "");
                    }

                    if (parts.Any(static p => !string.IsNullOrWhiteSpace(p)))
                        sb.AppendLine(string.Join("\t", parts));
                }

                sb.AppendLine();
            }

            var text = sb.ToString().Trim();
            if (text.Length > MaxTextCharsInPrompt)
                text = text[..MaxTextCharsInPrompt] + $"\n\n… [truncated after {MaxTextCharsInPrompt} chars]";
            return new AttachmentPayload("text", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                text, null, "");
        }
        catch (Exception ex)
        {
            return new AttachmentPayload("unsupported",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", null, null,
                $"Could not read .xlsx: {ex.Message}");
        }
    }

    private static AttachmentPayload ExtractPptx(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            using var doc = PresentationDocument.Open(ms, false);
            var sb = new StringBuilder();
            var presPart = doc.PresentationPart;
            if (presPart?.Presentation?.SlideIdList == null)
                return new AttachmentPayload("text", "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                    "(empty presentation)", null, "");

            var slideIdx = 0;
            foreach (var slideId in presPart.Presentation.SlideIdList.Elements<SlideId>())
            {
                var rid = slideId.RelationshipId?.Value;
                if (string.IsNullOrEmpty(rid)) continue;
                if (presPart.GetPartById(rid) is not SlidePart slidePart) continue;
                slideIdx++;
                sb.AppendLine($"## Slide {slideIdx}");
                foreach (var t in slidePart.Slide?.Descendants<DocumentFormat.OpenXml.Drawing.Text>() ?? Enumerable.Empty<DocumentFormat.OpenXml.Drawing.Text>())
                {
                    var line = t.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(line))
                        sb.AppendLine(line);
                }

                sb.AppendLine();
            }

            var text = sb.ToString().Trim();
            if (text.Length > MaxTextCharsInPrompt)
                text = text[..MaxTextCharsInPrompt] + $"\n\n… [truncated after {MaxTextCharsInPrompt} chars]";
            return new AttachmentPayload("text", "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                text, null, "");
        }
        catch (Exception ex)
        {
            return new AttachmentPayload("unsupported",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation", null, null,
                $"Could not read .pptx: {ex.Message}");
        }
    }

    private static string GuessMimeFromExtension(string ext) => ext switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".txt" or ".log" => "text/plain",
        ".md" => "text/markdown",
        ".csv" => "text/csv",
        ".json" => "application/json",
        ".xml" => "application/xml",
        ".html" or ".htm" => "text/html",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        _ => "application/octet-stream",
    };

    private static bool IsPlainTextMime(string mime) =>
        mime is "text/plain" or "text/markdown" or "text/csv" or "text/html" or "application/json" or "application/xml"
            or "text/xml";

    private static bool IsPlainTextExtension(string ext) =>
        ext is ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".html" or ".htm" or ".log" or ".rtf";

    public static bool IsAzureBlobStorageHttpsUrl(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return false;
        if (!Uri.TryCreate(href.Trim(), UriKind.Absolute, out var u)) return false;
        if (u.Scheme != Uri.UriSchemeHttps) return false;
        var host = u.Host.ToLowerInvariant();
        return host.EndsWith(".blob.core.windows.net", StringComparison.Ordinal)
               || host.EndsWith(".blob.storage.azure.net", StringComparison.Ordinal);
    }
}
