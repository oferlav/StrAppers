using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

public partial class MentorController
{
    /// <summary>
    /// Reviews the first matching uploaded (blob) resource for this board/student/sprint using sprint Trello context, module, user story, tasks, and document content.
    /// </summary>
    [HttpPost("use/resource-review")]
    public async Task<ActionResult<object>> ResourceReview([FromBody] ResourceReviewRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { success = false, message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(request.BoardId))
            return BadRequest(new { success = false, message = "BoardId is required." });
        if (request.StudentId <= 0)
            return BadRequest(new { success = false, message = "StudentId is required." });
        if (request.SprintNumber < 0)
            return BadRequest(new { success = false, message = "SprintNumber must be >= 0 (0 = Bugs)." });

        var boardId = request.BoardId.Trim();
        try
        {
            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.StudentRoles)
                .ThenInclude(sr => sr.Role)
                .Include(s => s.ProjectBoard)
                .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);
            if (student == null)
                return NotFound(new { success = false, message = $"Student {request.StudentId} not found." });
            if (!string.Equals(student.BoardId?.Trim(), boardId, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Student is not assigned to this BoardId." });

            var board = await _context.ProjectBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
            if (board == null)
                return NotFound(new { success = false, message = $"Board {boardId} not found." });

            var activeRole = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
            var originalRoleName = activeRole?.Role?.Name ?? "Team Member";
            var roleId = activeRole?.RoleId;

            string? moduleIdStr = null;
            foreach (var label in GetTrelloLabelNamesForRole(originalRoleName))
            {
                moduleIdStr = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, request.SprintNumber, label);
                if (!string.IsNullOrWhiteSpace(moduleIdStr))
                    break;
            }

            var contextMd = new StringBuilder();
            contextMd.AppendLine("## Sprint / module / user story");
            contextMd.AppendLine($"- **BoardId:** `{boardId}`");
            contextMd.AppendLine($"- **ProjectId:** {board.ProjectId}");
            contextMd.AppendLine($"- **SprintNumber:** {request.SprintNumber}");
            contextMd.AppendLine($"- **StudentId:** {request.StudentId}");
            contextMd.AppendLine($"- **Role:** {originalRoleName} (RoleId: {roleId?.ToString() ?? "(none)"})");
            contextMd.AppendLine($"- **ModuleId (Trello sprint card custom field):** {(moduleIdStr ?? "(not set)")}");
            contextMd.AppendLine();

            if (!string.IsNullOrWhiteSpace(moduleIdStr) && int.TryParse(moduleIdStr.Trim(), out var moduleInt))
            {
                var pm = await strAppersBackend.Utilities.ProjectModuleLookup.FindByBoardScopeAsync(
                    _context,
                    moduleInt,
                    board.ProjectId,
                    board.InstituteProjectId,
                    cancellationToken);
                contextMd.AppendLine("### Project module (database)");
                if (pm == null)
                {
                    contextMd.AppendLine(
                        $"No module row for ModuleId **{moduleInt}** and project scope **{board.ProjectId}** (catalog or institute design).");
                }
                else
                {
                    contextMd.AppendLine($"- **ModuleId:** {pm.Id}");
                    contextMd.AppendLine($"- **Title:** {pm.Title ?? "(none)"}");
                    contextMd.AppendLine("- **Description:**");
                    contextMd.AppendLine(string.IsNullOrWhiteSpace(pm.Description) ? "(none)" : pm.Description!.Trim());
                }

                contextMd.AppendLine();
                var usResult = await _trelloService.GetUserStoryCardByModuleIdAsync(boardId, moduleIdStr.Trim());
                var usCard = ExtractUserStoryCardFromResourceReviewResult(usResult);
                if (usCard != null)
                {
                    contextMd.AppendLine("### User story (Trello — User Stories list)");
                    contextMd.AppendLine(FormatResourceReviewUserStoryCard(usCard));
                    contextMd.AppendLine();
                }
            }
            else
            {
                contextMd.AppendLine("### Project module (database)");
                contextMd.AppendLine("Skipped: ModuleId could not be read from the student’s sprint card (custom field on role-labeled card).");
                contextMd.AppendLine();
            }

            var mentorCtx = await GetMentorContextInternal(request.StudentId, request.SprintNumber, null);
            if (mentorCtx == null)
                return BadRequest(new { success = false, message = "Could not build mentor context (board, sprint list, or project missing?)." });

            var ctxJson = JsonSerializer.Serialize(mentorCtx);
            var ctxEl = JsonSerializer.Deserialize<JsonElement>(ctxJson);
            var workspaceUserPrompt = "";
            if (ctxEl.TryGetProperty("UserPrompt", out var up1))
                workspaceUserPrompt = up1.GetString() ?? "";
            else if (ctxEl.TryGetProperty("userPrompt", out var up2))
                workspaceUserPrompt = up2.GetString() ?? "";

            var (customerChatSection, customerChatMessageCount) = await BuildResourceReviewCustomerChatSectionAsync(
                request.StudentId,
                request.SprintNumber,
                cancellationToken);

            // Match sprint first, then board-level rows (SprintNumber null). Do not fall back to a different
            // sprint's row — that produced confusing errors when the current sprint had no link but an older
            // sprint had a non-blob or external URL.
            var baseQ = _context.Resources.AsNoTracking()
                .Where(r => r.BoardId == boardId && r.StudentId == request.StudentId && !r.IsFigma);
            var resource = await baseQ.Where(r => r.SprintNumber == request.SprintNumber).FirstOrDefaultAsync(cancellationToken)
                           ?? await baseQ.Where(r => r.SprintNumber == null).FirstOrDefaultAsync(cancellationToken);

            if (resource == null)
                return BadRequest(new { success = false, message = "❌ No resource found for this sprint. Add a file in Resources for this sprint." });

            if (!ResourceDocumentContentExtractor.IsAzureBlobStorageHttpsUrl(resource.Url))
            {
                var scope = resource.SprintNumber == request.SprintNumber
                    ? $"this sprint ({request.SprintNumber})"
                    : "your board-level resource (not tied to a sprint)";
                return BadRequest(new
                {
                    success = false,
                    message =
                        $"Resource \"{resource.Name?.Trim() ?? "document"}\" ({scope}) is not an Azure Blob HTTPS URL. Only files uploaded to team storage can be reviewed here; replace the link or re-upload from Resources.",
                    resourceId = resource.Id,
                    sprintNumber = resource.SprintNumber
                });
            }

            if (!_azureBlobStorage.IsConfigured)
                return StatusCode(503, new { success = false, message = "Azure Blob Storage is not configured on the server." });

            if (!Uri.TryCreate(resource.Url.Trim(), UriKind.Absolute, out var blobUri))
                return BadRequest(new { success = false, message = "Invalid resource URL." });

            var blobOpen = await _azureBlobStorage.OpenBlobReadDetailedAsync(blobUri, cancellationToken);
            if (!blobOpen.Success)
            {
                var code = blobOpen.ErrorCode ?? "Unknown";
                var status = code switch
                {
                    "BlobNotFound" => 404,
                    "NotConfigured" => 503,
                    _ => 422,
                };
                return StatusCode(status, new
                {
                    success = false,
                    message = blobOpen.UserHint ?? "Could not open file from team storage.",
                    errorCode = code,
                    resourceId = resource.Id,
                });
            }

            var blobStream = blobOpen.Stream!;
            var blobContentType = blobOpen.ContentType ?? "application/octet-stream";

            var safeName = resource.Name?.Trim() ?? "document";
            if (!System.IO.Path.HasExtension(safeName))
            {
                var extBlob = System.IO.Path.GetExtension(blobUri.AbsolutePath);
                if (!string.IsNullOrEmpty(extBlob))
                    safeName += extBlob;
            }

            var serverCt = blobContentType;
            var mimeNormalized = ResourceDocumentContentExtractor.NormalizeMimeForDisplay(serverCt, safeName);
            const int maxBinaryForLlmBytes = 20 * 1024 * 1024;

            ResourceDocumentContentExtractor.AttachmentPayload payload;
            string? visionDataUrl = null;

            if (mimeNormalized.StartsWith("image/", StringComparison.Ordinal))
            {
                byte[] bytes;
                await using (blobStream)
                {
                    using var ms = new MemoryStream();
                    await blobStream.CopyToAsync(ms, cancellationToken);
                    bytes = ms.ToArray();
                }

                if (bytes.Length == 0)
                    return BadRequest(new { success = false, message = "Image file is empty.", resourceId = resource.Id });
                if (bytes.Length > maxBinaryForLlmBytes)
                    return BadRequest(new { success = false, message = $"Image is too large for review (max {maxBinaryForLlmBytes / (1024 * 1024)} MB).", resourceId = resource.Id });

                var maxVisionW = Math.Clamp(_configuration.GetValue("Mentor:VisionImageMaxWidthPx", MentorVisionImagePreparer.DefaultMaxWidthPx), 256, 2048);
                var jpegQ = Math.Clamp(_configuration.GetValue("Mentor:VisionJpegQuality", MentorVisionImagePreparer.DefaultJpegQuality), 40, 95);
                var (visionBytes, visionMime, visionNote) = MentorVisionImagePreparer.PrepareForVision(bytes, mimeNormalized, maxVisionW, jpegQ);
                visionDataUrl = $"data:{visionMime};base64,{Convert.ToBase64String(visionBytes)}";
                var visionHint = string.IsNullOrEmpty(visionNote)
                    ? "Inline vision attachment (optimized size)."
                    : visionNote;
                payload = new ResourceDocumentContentExtractor.AttachmentPayload(
                    "inline_image",
                    visionMime,
                    null,
                    null,
                    $"{visionHint} Not a fetchable URL.");
            }
            else if (string.Equals(mimeNormalized, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes;
                await using (blobStream)
                {
                    using var ms = new MemoryStream();
                    await blobStream.CopyToAsync(ms, cancellationToken);
                    bytes = ms.ToArray();
                }

                if (bytes.Length == 0)
                    return BadRequest(new { success = false, message = "PDF is empty.", resourceId = resource.Id });
                if (bytes.Length > maxBinaryForLlmBytes)
                    return BadRequest(new { success = false, message = $"PDF is too large for review (max {maxBinaryForLlmBytes / (1024 * 1024)} MB).", resourceId = resource.Id });

                var pdfText = PdfTextExtractor.TryExtractText(bytes, ResourceDocumentContentExtractor.MaxTextCharsInPrompt);
                if (string.IsNullOrWhiteSpace(pdfText))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Could not extract text from this PDF (it may be scanned or image-only). Upload a text-based PDF, an image export, or paste static text.",
                        resourceId = resource.Id
                    });
                }

                payload = new ResourceDocumentContentExtractor.AttachmentPayload("text", mimeNormalized, pdfText, null, "Extracted on server from PDF (no OCR).");
            }
            else
            {
                await using (blobStream)
                {
                    payload = await ResourceDocumentContentExtractor.BuildAsync(
                        blobStream,
                        blobContentType,
                        safeName,
                        cancellationToken);
                }
            }

            if (payload.Mode is "empty" or "too_large" or "unsupported")
                return BadRequest(new { success = false, message = payload.Note, mode = payload.Mode, resourceId = resource.Id });

            if (payload.Mode == "text" && string.IsNullOrWhiteSpace(payload.TextBody))
                return BadRequest(new
                {
                    success = false,
                    message = "Extracted document text is empty. If this is a Word file, try exporting to PDF or ensure the file has real text (not only images).",
                    mode = "no_text",
                    resourceId = resource.Id
                });

            var docSection = new StringBuilder();
            docSection.AppendLine($"Resource name: {resource.Name}");
            docSection.AppendLine($"Stored URL host: {blobUri.Host}");
            docSection.AppendLine($"Server Content-Type: {serverCt}");
            docSection.AppendLine($"Attachment mode: {payload.Mode} ({payload.MimeType})");
            if (!string.IsNullOrEmpty(payload.Note))
                docSection.AppendLine($"Note: {payload.Note}");
            docSection.AppendLine();
            if (payload.Mode == "text")
                docSection.AppendLine(payload.TextBody ?? "");
            else if (payload.Mode == "inline_image")
                docSection.AppendLine("The image is attached via the API vision channel (not as a URL in this text). Review what you can see in the image.");

            var reviewInstructions = LoadMentorPromptFile("ResourceReviewSystem")?.Trim()
                ?? "Review the uploaded document against the sprint context. Be specific and actionable.";

            var baseSystem = StripDebugMarkers(DbgConfig("SystemPrompt") + (_promptConfig.Mentor.SystemPrompt ?? ""));
            var fullStackBlock = BuildFullStackDeveloperMentorInstructions(originalRoleName);
            if (!string.IsNullOrEmpty(fullStackBlock))
                baseSystem += fullStackBlock;

            var systemPrompt = $"{GetPlatformInterfaceAndRolePermissions()}\n\n{baseSystem}\n\n{reviewInstructions}".Trim();

            var userMessage = new StringBuilder();
            userMessage.AppendLine("=== STRUCTURED CONTEXT (module + user story) ===");
            userMessage.AppendLine(contextMd.ToString().Trim());
            userMessage.AppendLine();
            userMessage.AppendLine("=== MENTOR WORKSPACE (tasks, team, module summary) ===");
            userMessage.AppendLine(workspaceUserPrompt.Trim());
            userMessage.AppendLine();
            userMessage.AppendLine("=== CUSTOMER CHAT (AI Customer — same StudentId and SprintId / SprintNumber) ===");
            userMessage.AppendLine(customerChatSection.Trim());
            userMessage.AppendLine();
            userMessage.AppendLine("=== UPLOADED DOCUMENT ===");
            userMessage.AppendLine(docSection.ToString().Trim());
            userMessage.AppendLine();
            userMessage.AppendLine("Provide your review now.");

            var userPromptFinal = userMessage.ToString();

            if (request.Test)
            {
                return Ok(new
                {
                    success = true,
                    test = true,
                    resourceId = resource.Id,
                    moduleIdFromTrello = moduleIdStr,
                    roleName = originalRoleName,
                    roleId,
                    attachmentMode = payload.Mode,
                    mimeType = payload.MimeType,
                    usesVision = visionDataUrl != null,
                    generatedSystemPrompt = systemPrompt,
                    inputTokens = 0,
                    outputTokens = 0,
                    totalTokensConsumed = 0,
                    userPromptCharLength = userPromptFinal.Length,
                    customerChatMessageCount,
                    message = "Test mode: LLM not called. Inspect generatedSystemPrompt and userPromptCharLength."
                });
            }

            var cheapName = _configuration["OpenAI:CheapModel"] ?? "gpt-4o-mini";
            var aiModel = new AIModel
            {
                Name = cheapName,
                Provider = "OpenAI",
                BaseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1",
                MaxTokens = 16384,
                DefaultTemperature = 0.2
            };

            var (llmText, inputTokens, outputTokens) = visionDataUrl != null
                ? await _chatCompletionService.GetOpenAiChatCompletionWithOptionalVisionAsync(
                    aiModel, systemPrompt, userPromptFinal, visionDataUrl)
                : await _chatCompletionService.GetChatCompletionAsync(aiModel, systemPrompt, userPromptFinal);

            if (!request.Test)
                await TryPersistCacheReviewAsync(boardId, request.StudentId, request.SprintNumber, CacheReviewType.Resource, llmText, cancellationToken);

            return Ok(new
            {
                success = true,
                test = false,
                model = cheapName,
                resourceId = resource.Id,
                moduleIdFromTrello = moduleIdStr,
                roleName = originalRoleName,
                roleId,
                attachmentMode = payload.Mode,
                mimeType = payload.MimeType,
                usedVision = visionDataUrl != null,
                inputTokens,
                outputTokens,
                totalTokensConsumed = inputTokens + outputTokens,
                llmResponse = llmText,
                customerChatMessageCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "resource-review failed for board {BoardId}", boardId);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Same filter as <see cref="CustomerController.GetCustomerChatHistory"/>: <see cref="CustomerChatHistory.StudentId"/>
    /// and <see cref="CustomerChatHistory.SprintId"/> (= sprint number).
    /// Limited by <see cref="PromptConfig.Customer"/>.<c>ChatHistoryLength</c> × 2 rows.
    /// </summary>
    private async Task<(string Text, int Count)> BuildResourceReviewCustomerChatSectionAsync(
        int studentId,
        int sprintNumber,
        CancellationToken cancellationToken)
    {
        var pairLimit = _promptConfig.Customer.ChatHistoryLength;
        if (pairLimit < 1)
            pairLimit = 5;
        var maxRows = pairLimit * 2;

        var chatRows = await _context.CustomerChatHistory.AsNoTracking()
            .Where(h => h.StudentId == studentId && h.SprintId == sprintNumber)
            .OrderByDescending(h => h.CreatedAt)
            .Take(maxRows)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        if (chatRows.Count == 0)
        {
            sb.AppendLine("(No messages in `CustomerChatHistory` for this StudentId and SprintId.)");
            return (sb.ToString(), 0);
        }

        foreach (var row in chatRows)
        {
            var role = row.Role?.Trim().ToLowerInvariant() == "assistant" ? "Assistant" : "User";
            var msg = row.Message?.Trim() ?? "";
            if (msg.Length > 4000)
                msg = msg[..4000] + "…";
            sb.AppendLine($"- **[{role}]** ({row.CreatedAt:u}): {msg}");
        }

        return (sb.ToString(), chatRows.Count);
    }

    private static object? ExtractUserStoryCardFromResourceReviewResult(object? getUserStoryResult)
    {
        if (getUserStoryResult == null) return null;
        var t = getUserStoryResult.GetType();
        var success = t.GetProperty("Success")?.GetValue(getUserStoryResult) is bool b && b;
        if (!success) return null;
        return t.GetProperty("Card")?.GetValue(getUserStoryResult);
    }

    private static string FormatResourceReviewUserStoryCard(object card)
    {
        var t = card.GetType();
        var name = t.GetProperty("Name")?.GetValue(card)?.ToString() ?? "";
        var desc = t.GetProperty("Description")?.GetValue(card)?.ToString() ?? "";
        var sb = new StringBuilder();
        sb.AppendLine($"- **Title:** {name}");
        if (!string.IsNullOrWhiteSpace(desc))
        {
            sb.AppendLine("- **Description:**");
            sb.AppendLine(desc.Trim());
        }

        var checklists = t.GetProperty("Checklists")?.GetValue(card) as System.Collections.IEnumerable;
        if (checklists != null)
        {
            sb.AppendLine("- **Checklists:**");
            foreach (var cl in checklists)
            {
                if (cl == null) continue;
                var clt = cl.GetType();
                var clName = clt.GetProperty("Name")?.GetValue(cl)?.ToString() ?? "Checklist";
                sb.AppendLine($"  - **{clName}**");
                var items = clt.GetProperty("CheckItems")?.GetValue(cl) as System.Collections.IEnumerable;
                if (items == null) continue;
                foreach (var ci in items)
                {
                    if (ci == null) continue;
                    var cit = ci.GetType();
                    var itemName = cit.GetProperty("Name")?.GetValue(ci)?.ToString() ?? "";
                    var state = cit.GetProperty("State")?.GetValue(ci)?.ToString() ?? "";
                    var done = string.Equals(state, "complete", StringComparison.OrdinalIgnoreCase) ? "[x]" : "[ ]";
                    sb.AppendLine($"    - {done} {itemName}");
                }
            }
        }

        return sb.ToString().TrimEnd();
    }
}

/// <summary>Request for <c>POST /api/Mentor/use/resource-review</c>.</summary>
public class ResourceReviewRequest
{
    public string BoardId { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public int SprintNumber { get; set; }
    /// <summary>When true, returns assembled system prompt and skips LLM; token counts are zero. Default is false.</summary>
    [DefaultValue(false)]
    public bool Test { get; set; } = false;
}
