using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers;

public partial class ProjectsController
{
    private static readonly JsonSerializerOptions PostInstituteAssistantJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Institute project AI assistant. <c>General</c>: header fields; <c>Templates</c>: Task Builder (Trello template JSON + module + card context).
    /// Route: POST /api/Projects/use/by-institute/ai-assistant/{source}/{id}
    /// </summary>
    /// <remarks>
    /// Reads the raw JSON body here instead of <c>[FromBody] JsonElement</c> so the model binder never treats
    /// Templates payloads as <see cref="ProjectInstituteAssistantRequest"/> (which would fail <c>UserRequest</c> validation).
    /// </remarks>
    [HttpPost("use/by-institute/ai-assistant/{source}/{id:int}")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> PostInstituteProjectAssistant(
        string source,
        int id,
        CancellationToken cancellationToken = default)
    {
        Request.EnableBuffering();
        string bodyText;
        using (var reader = new StreamReader(
                   Request.Body,
                   Encoding.UTF8,
                   detectEncodingFromByteOrderMarks: false,
                   bufferSize: 64 * 1024,
                   leaveOpen: true))
        {
            bodyText = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(bodyText))
        {
            return BadRequest("Request body is required.");
        }

        var canonical = InstituteAssistantChatHelper.NormalizeToCanonicalSource(source);
        if (canonical == null)
        {
            return BadRequest("Invalid source. Use General, Templates, Brief, Modules, or Customer.");
        }

        if (string.Equals(
                canonical,
                InstituteAssistantChatHelper.SourceGeneral,
                StringComparison.Ordinal))
        {
            ProjectInstituteAssistantRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<ProjectInstituteAssistantRequest>(
                    bodyText,
                    PostInstituteAssistantJsonOptions);
            }
            catch (JsonException ex)
            {
                return BadRequest($"Invalid JSON: {ex.Message}");
            }

            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            return await PostInstituteProjectAssistantGeneralCoreAsync(
                id,
                canonical,
                request,
                cancellationToken);
        }

        if (string.Equals(
                canonical,
                InstituteAssistantChatHelper.SourceTemplates,
                StringComparison.Ordinal))
        {
            ProjectInstituteTemplatesAssistantRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<ProjectInstituteTemplatesAssistantRequest>(
                    bodyText,
                    PostInstituteAssistantJsonOptions);
            }
            catch (JsonException ex)
            {
                return BadRequest($"Invalid JSON: {ex.Message}");
            }

            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            return await PostInstituteProjectAssistantTemplatesCoreAsync(
                id,
                request,
                cancellationToken);
        }

        if (string.Equals(
                canonical,
                InstituteAssistantChatHelper.SourceBrief,
                StringComparison.Ordinal))
        {
            ProjectInstituteBriefAssistantRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<ProjectInstituteBriefAssistantRequest>(
                    bodyText,
                    PostInstituteAssistantJsonOptions);
            }
            catch (JsonException ex)
            {
                return BadRequest($"Invalid JSON: {ex.Message}");
            }

            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            return await PostInstituteProjectAssistantBriefCoreAsync(
                id,
                canonical,
                request,
                cancellationToken);
        }

        if (string.Equals(
                canonical,
                InstituteAssistantChatHelper.SourceModules,
                StringComparison.Ordinal))
        {
            ProjectInstituteModulesAssistantRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<ProjectInstituteModulesAssistantRequest>(
                    bodyText,
                    PostInstituteAssistantJsonOptions);
            }
            catch (JsonException ex)
            {
                return BadRequest($"Invalid JSON: {ex.Message}");
            }

            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            return await PostInstituteProjectAssistantModulesCoreAsync(
                id,
                canonical,
                request,
                cancellationToken);
        }

        if (string.Equals(
                canonical,
                InstituteAssistantChatHelper.SourceCustomer,
                StringComparison.Ordinal))
        {
            ProjectInstituteCustomerAssistantRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<ProjectInstituteCustomerAssistantRequest>(
                    bodyText,
                    PostInstituteAssistantJsonOptions);
            }
            catch (JsonException ex)
            {
                return BadRequest($"Invalid JSON: {ex.Message}");
            }

            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            return await PostInstituteProjectAssistantCustomerCoreAsync(
                id,
                canonical,
                request,
                cancellationToken);
        }

        return StatusCode(
            501,
            "This source is not implemented yet. Use 'General' for the header, 'Templates' for Task Builder, or a future tab-scoped source.");
    }

    private async Task<IActionResult> PostInstituteProjectAssistantModulesCoreAsync(
        int id,
        string canonicalSource,
        ProjectInstituteModulesAssistantRequest request,
        CancellationToken cancellationToken)
    {
        const int maxUserRequestLength = 12_000;
        if (string.IsNullOrWhiteSpace(request.UserRequest))
        {
            return BadRequest("userRequest in body is required (non-empty).");
        }

        if (request.UserRequest.Length > maxUserRequestLength)
        {
            return BadRequest($"userRequest is too long (max {maxUserRequestLength} characters).");
        }

        var trimmedRequest = request.UserRequest.Trim();

        try
        {
            _logger.LogInformation(
                "MODULES_ASSISTANT_DEPLOY_MARKER_v2 start: ProjectId={ProjectId}, Source={Source}, RequestLen={RequestLen}",
                id,
                canonicalSource,
                trimmedRequest.Length);

            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            var teacher = await InstituteStaffAuthHelper.ResolveActiveInstituteTeacherAsync(
                _context, Request, cancellationToken);
            if (teacher == null || teacher.InstituteId != instituteId.Value)
            {
                return Unauthorized("Institute staff context is missing or invalid.");
            }

            var project = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value))
                .Select(p => new
                {
                    p.Mission,
                    p.ShortBrief,
                    p.Description,
                    p.SystemDesign,
                    p.SystemDesignFormatted,
                    HasSystemDesignDoc = p.SystemDesignDoc != null && p.SystemDesignDoc.Length > 0,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (project == null)
            {
                return NotFound($"Project with ID {id} not found for this institute context.");
            }

            var modulesFromDb = await _context.ProjectModules
                .AsNoTracking()
                .Where(pm => pm.ProjectId == id && pm.ModuleType == 2)
                .OrderBy(pm => pm.Sequence ?? int.MaxValue)
                .ThenBy(pm => pm.Id)
                .Select((pm) => new ProjectModuleAssistantItem
                {
                    ModuleId = pm.Id,
                    Sequence = pm.Sequence,
                    Title = pm.Title,
                    Body = pm.Description,
                })
                .ToListAsync(cancellationToken);

            static List<ProjectModuleAssistantItem> NormalizeModules(List<ProjectModuleAssistantItem>? items)
            {
                var source = items ?? new List<ProjectModuleAssistantItem>();
                var normalized = new List<ProjectModuleAssistantItem>();
                for (var i = 0; i < source.Count; i++)
                {
                    var it = source[i];
                    var seq = (it?.Sequence ?? 0) > 0 ? it!.Sequence : (i + 1);
                    normalized.Add(new ProjectModuleAssistantItem
                    {
                        ModuleId = it?.ModuleId,
                        Sequence = seq,
                        Title = (it?.Title ?? string.Empty).Trim(),
                        Body = (it?.Body ?? string.Empty).Trim(),
                    });
                }
                return normalized.OrderBy(x => x.Sequence ?? int.MaxValue).ThenBy(x => x.ModuleId ?? int.MaxValue).ToList();
            }

            var currentModules = request.CurrentModules != null && request.CurrentModules.Count > 0
                ? NormalizeModules(request.CurrentModules)
                : NormalizeModules(modulesFromDb);
            var originalModules = request.OriginalModules != null && request.OriginalModules.Count > 0
                ? NormalizeModules(request.OriginalModules)
                : NormalizeModules(modulesFromDb);

            var missionForPrompt = !string.IsNullOrWhiteSpace(request.CurrentMission)
                ? request.CurrentMission!.Trim()
                : project.Mission;
            var shortBriefForPrompt = !string.IsNullOrWhiteSpace(request.CurrentShortBrief)
                ? request.CurrentShortBrief!.Trim()
                : project.ShortBrief;
            var descriptionForPrompt = !string.IsNullOrWhiteSpace(request.CurrentDescription)
                ? request.CurrentDescription!.Trim()
                : project.Description;
            var hasUploadedDesignMarker =
                !string.IsNullOrWhiteSpace(project.SystemDesignFormatted)
                && project.SystemDesignFormatted.StartsWith("[UploadedDesignParsed]", StringComparison.Ordinal);
            var uploadedDesignContext = !string.IsNullOrWhiteSpace(project.SystemDesign)
                ? project.SystemDesign
                : null;
            var designContextUsed = !string.IsNullOrWhiteSpace(uploadedDesignContext);
            var designContextChars = designContextUsed ? uploadedDesignContext!.Length : 0;
            var designContextSource = hasUploadedDesignMarker
                ? "UploadedDesignParsedMarker"
                : (project.HasSystemDesignDoc ? "SystemDesignDoc" : (designContextUsed ? "SystemDesignTextOnly" : "None"));
            _logger.LogInformation(
                "MODULES_ASSISTANT_DEPLOY_MARKER_v2 context-check: ProjectId={ProjectId}, HasMarker={HasMarker}, HasDoc={HasDoc}, UsingContext={UsingContext}",
                id,
                hasUploadedDesignMarker,
                project.HasSystemDesignDoc,
                designContextUsed);

            var historyBlock = await InstituteAssistantChatHelper.BuildRecentContextBlockAsync(
                _context,
                teacher.InstituteId,
                teacher.Id,
                id,
                canonicalSource,
                _logger,
                cancellationToken);

            var systemBlock = BuildProjectModulesAiSystemInstructions();
            var userBlock = BuildProjectModulesAiUserBlock(
                missionForPrompt,
                shortBriefForPrompt,
                descriptionForPrompt,
                uploadedDesignContext,
                originalModules,
                currentModules,
                trimmedRequest);

            var fullPrompt = systemBlock
                + (string.IsNullOrWhiteSpace(historyBlock) ? "" : "\n\n" + historyBlock.Trim())
                + "\n\n" + userBlock;

            var raw = await _aiService.GenerateTextResponseAsync(fullPrompt);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return StatusCode(502, "The AI assistant did not return a response. Please try again.");
            }

            var parsed = TryParseProjectModulesAiJsonResponse(raw, out var message, out var suggestedModules);
            if (parsed)
            {
                if (designContextUsed
                    && message.Contains("design document context is missing", StringComparison.OrdinalIgnoreCase))
                {
                    message = "The uploaded design document is available in context. Please specify how many modules you want (for example, 7), and I will generate module titles and module bodies accordingly.";
                }
                _logger.LogInformation(
                    "MODULES_ASSISTANT_DEPLOY_MARKER_v2 parsed: ProjectId={ProjectId}, SuggestedModulesCount={SuggestedModulesCount}",
                    id,
                    suggestedModules?.Count ?? 0);
                var ai = new ProjectModulesAiAssistantResponse
                {
                    AssistantMessage = message,
                    SuggestedModules = suggestedModules,
                    DesignContextUsed = designContextUsed,
                    DesignContextChars = designContextChars,
                    DesignContextSource = designContextSource,
                };
                var assistantToStore = !string.IsNullOrWhiteSpace(ai.AssistantMessage) ? ai.AssistantMessage : null;
                await InstituteAssistantChatHelper.SaveTurnAsync(
                    _context,
                    teacher.InstituteId,
                    teacher.Id,
                    id,
                    canonicalSource,
                    trimmedRequest,
                    assistantToStore,
                    _logger,
                    cancellationToken);
                return Ok(ai);
            }

            var fallback = raw.Trim();
            _logger.LogInformation(
                "MODULES_ASSISTANT_DEPLOY_MARKER_v2 fallback: ProjectId={ProjectId}, FallbackLen={FallbackLen}",
                id,
                fallback.Length);
            await InstituteAssistantChatHelper.SaveTurnAsync(
                _context,
                teacher.InstituteId,
                teacher.Id,
                id,
                canonicalSource,
                trimmedRequest,
                fallback,
                _logger,
                cancellationToken);
            return Ok(new ProjectModulesAiAssistantResponse
            {
                AssistantMessage = fallback,
                SuggestedModules = null,
                DesignContextUsed = designContextUsed,
                DesignContextChars = designContextChars,
                DesignContextSource = designContextSource,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in project modules AI assistant for project {ProjectId}", id);
            return StatusCode(500, "An error occurred while running the modules assistant.");
        }
    }

    private async Task<IActionResult> PostInstituteProjectAssistantBriefCoreAsync(
        int id,
        string canonicalSource,
        ProjectInstituteBriefAssistantRequest request,
        CancellationToken cancellationToken)
    {
        const int maxUserRequestLength = 12_000;
        if (string.IsNullOrWhiteSpace(request.UserRequest))
        {
            return BadRequest("userRequest in body is required (non-empty).");
        }

        if (request.UserRequest.Length > maxUserRequestLength)
        {
            return BadRequest($"userRequest is too long (max {maxUserRequestLength} characters).");
        }

        var trimmedRequest = request.UserRequest.Trim();

        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            var teacher = await InstituteStaffAuthHelper.ResolveActiveInstituteTeacherAsync(
                _context, Request, cancellationToken);
            if (teacher == null || teacher.InstituteId != instituteId.Value)
            {
                return Unauthorized("Institute staff context is missing or invalid.");
            }

            var project = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value))
                .Select(p => new
                {
                    p.Description,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (project == null)
            {
                return NotFound($"Project with ID {id} not found for this institute context.");
            }

            var descriptionForPrompt = !string.IsNullOrWhiteSpace(request.CurrentDescription)
                ? request.CurrentDescription.Trim()
                : project.Description;
            var originalDescriptionForPrompt = !string.IsNullOrWhiteSpace(request.OriginalDescription)
                ? request.OriginalDescription.Trim()
                : project.Description;

            if (ShouldAnswerFromOriginalBrief(trimmedRequest))
            {
                var originalBriefText = RenderOriginalBriefForAssistantReply(originalDescriptionForPrompt);
                var directReply = string.IsNullOrWhiteSpace(originalBriefText)
                    ? "I do not have an original brief payload available for this project yet."
                    : $"Original brief:\n{originalBriefText}";

                await InstituteAssistantChatHelper.SaveTurnAsync(
                    _context,
                    teacher.InstituteId,
                    teacher.Id,
                    id,
                    canonicalSource,
                    trimmedRequest,
                    directReply,
                    _logger,
                    cancellationToken);

                return Ok(new ProjectBriefAiAssistantResponse
                {
                    AssistantMessage = directReply,
                    SuggestedDescription = null,
                });
            }

            var historyBlock = await InstituteAssistantChatHelper.BuildRecentContextBlockAsync(
                _context,
                teacher.InstituteId,
                teacher.Id,
                id,
                canonicalSource,
                _logger,
                cancellationToken);

            var systemBlock = BuildProjectBriefAiSystemInstructions();
            var userBlock = BuildProjectBriefAiUserBlock(
                originalDescription: originalDescriptionForPrompt,
                currentDescription: descriptionForPrompt,
                userRequest: trimmedRequest);

            var fullPrompt = systemBlock
                + (string.IsNullOrWhiteSpace(historyBlock) ? "" : "\n\n" + historyBlock.Trim())
                + "\n\n" + userBlock;

            var raw = await _aiService.GenerateTextResponseAsync(fullPrompt);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return StatusCode(502, "The AI assistant did not return a response. Please try again.");
            }

            var parsed = TryParseProjectBriefAiJsonResponse(raw, out var message, out var suggestedDescription);
            if (parsed)
            {
                var ai = new ProjectBriefAiAssistantResponse
                {
                    AssistantMessage = message,
                    SuggestedDescription = suggestedDescription,
                };
                var assistantToStore = !string.IsNullOrWhiteSpace(ai.AssistantMessage) ? ai.AssistantMessage : null;
                await InstituteAssistantChatHelper.SaveTurnAsync(
                    _context,
                    teacher.InstituteId,
                    teacher.Id,
                    id,
                    canonicalSource,
                    trimmedRequest,
                    assistantToStore,
                    _logger,
                    cancellationToken);
                return Ok(ai);
            }

            var fallback = raw.Trim();
            await InstituteAssistantChatHelper.SaveTurnAsync(
                _context,
                teacher.InstituteId,
                teacher.Id,
                id,
                canonicalSource,
                trimmedRequest,
                fallback,
                _logger,
                cancellationToken);
            return Ok(new ProjectBriefAiAssistantResponse
            {
                AssistantMessage = fallback,
                SuggestedDescription = null,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in project brief AI assistant for project {ProjectId}", id);
            return StatusCode(500, "An error occurred while running the brief assistant.");
        }
    }

    private static bool ShouldAnswerFromOriginalBrief(string userRequest)
    {
        if (string.IsNullOrWhiteSpace(userRequest))
        {
            return false;
        }

        var q = userRequest.Trim();
        return q.Contains("original brief", StringComparison.OrdinalIgnoreCase)
               || q.Contains("original description", StringComparison.OrdinalIgnoreCase)
               || q.Contains("before changes", StringComparison.OrdinalIgnoreCase)
               || (q.Contains("remember", StringComparison.OrdinalIgnoreCase)
                   && q.Contains("brief", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IActionResult> PostInstituteProjectAssistantCustomerCoreAsync(
        int id,
        string canonicalSource,
        ProjectInstituteCustomerAssistantRequest request,
        CancellationToken cancellationToken)
    {
        const int maxUserRequestLength = 12_000;
        if (string.IsNullOrWhiteSpace(request.UserRequest))
        {
            return BadRequest("userRequest in body is required (non-empty).");
        }

        if (request.UserRequest.Length > maxUserRequestLength)
        {
            return BadRequest($"userRequest is too long (max {maxUserRequestLength} characters).");
        }

        var trimmedRequest = request.UserRequest.Trim();

        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            var teacher = await InstituteStaffAuthHelper.ResolveActiveInstituteTeacherAsync(
                _context, Request, cancellationToken);
            if (teacher == null || teacher.InstituteId != instituteId.Value)
            {
                return Unauthorized("Institute staff context is missing or invalid.");
            }

            var project = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value))
                .Select(p => new
                {
                    p.CustomerPastStory,
                    p.Mission,
                    p.ShortBrief,
                    p.Description,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (project == null)
            {
                return NotFound($"Project with ID {id} not found for this institute context.");
            }

            var modulesFromDb = await _context.ProjectModules
                .AsNoTracking()
                .Where(pm => pm.ProjectId == id && pm.ModuleType == 2)
                .OrderBy(pm => pm.Sequence ?? int.MaxValue)
                .ThenBy(pm => pm.Id)
                .Select(pm => new ProjectModuleAssistantItem
                {
                    ModuleId = pm.Id,
                    Sequence = pm.Sequence,
                    Title = pm.Title,
                    Body = pm.Description,
                })
                .ToListAsync(cancellationToken);

            static List<ProjectModuleAssistantItem> NormalizeModules(List<ProjectModuleAssistantItem>? items)
            {
                var source = items ?? new List<ProjectModuleAssistantItem>();
                var normalized = new List<ProjectModuleAssistantItem>();
                for (var i = 0; i < source.Count; i++)
                {
                    var it = source[i];
                    var seq = (it?.Sequence ?? 0) > 0 ? it!.Sequence : (i + 1);
                    normalized.Add(new ProjectModuleAssistantItem
                    {
                        ModuleId = it?.ModuleId,
                        Sequence = seq,
                        Title = (it?.Title ?? string.Empty).Trim(),
                        Body = (it?.Body ?? string.Empty).Trim(),
                    });
                }
                return normalized.OrderBy(x => x.Sequence ?? int.MaxValue).ThenBy(x => x.ModuleId ?? int.MaxValue).ToList();
            }

            var currentModules = request.CurrentModules != null && request.CurrentModules.Count > 0
                ? NormalizeModules(request.CurrentModules)
                : NormalizeModules(modulesFromDb);
            var originalModules = request.OriginalModules != null && request.OriginalModules.Count > 0
                ? NormalizeModules(request.OriginalModules)
                : NormalizeModules(modulesFromDb);

            var missionForPrompt = !string.IsNullOrWhiteSpace(request.CurrentMission)
                ? request.CurrentMission!.Trim()
                : project.Mission;
            var shortBriefForPrompt = !string.IsNullOrWhiteSpace(request.CurrentShortBrief)
                ? request.CurrentShortBrief!.Trim()
                : project.ShortBrief;
            var descriptionForPrompt = !string.IsNullOrWhiteSpace(request.CurrentDescription)
                ? request.CurrentDescription!.Trim()
                : project.Description;
            var currentCustomerPastStory = !string.IsNullOrWhiteSpace(request.CurrentCustomerPastStory)
                ? request.CurrentCustomerPastStory!.Trim()
                : project.CustomerPastStory;
            var originalCustomerPastStory = !string.IsNullOrWhiteSpace(request.OriginalCustomerPastStory)
                ? request.OriginalCustomerPastStory!.Trim()
                : project.CustomerPastStory;

            var historyBlock = await InstituteAssistantChatHelper.BuildRecentContextBlockAsync(
                _context,
                teacher.InstituteId,
                teacher.Id,
                id,
                canonicalSource,
                _logger,
                cancellationToken);

            var systemBlock = BuildProjectCustomerAiSystemInstructions();
            var userBlock = BuildProjectCustomerAiUserBlock(
                missionForPrompt,
                shortBriefForPrompt,
                descriptionForPrompt,
                originalCustomerPastStory,
                currentCustomerPastStory,
                originalModules,
                currentModules,
                trimmedRequest);

            var fullPrompt = systemBlock
                + (string.IsNullOrWhiteSpace(historyBlock) ? "" : "\n\n" + historyBlock.Trim())
                + "\n\n" + userBlock;

            var raw = await _aiService.GenerateTextResponseAsync(fullPrompt);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return StatusCode(502, "The AI assistant did not return a response. Please try again.");
            }

            var parsed = TryParseProjectCustomerAiJsonResponse(raw, out var message, out var suggestedCustomerPastStory);
            if (parsed)
            {
                ApplyProjectCustomerAssistantFieldNormalization(ref message, ref suggestedCustomerPastStory);
                var ai = new ProjectCustomerAiAssistantResponse
                {
                    AssistantMessage = message,
                    SuggestedCustomerPastStory = suggestedCustomerPastStory,
                };
                var assistantToStore = !string.IsNullOrWhiteSpace(ai.AssistantMessage) ? ai.AssistantMessage : null;
                await InstituteAssistantChatHelper.SaveTurnAsync(
                    _context,
                    teacher.InstituteId,
                    teacher.Id,
                    id,
                    canonicalSource,
                    trimmedRequest,
                    assistantToStore,
                    _logger,
                    cancellationToken);
                return Ok(ai);
            }

            // Prose-only output (no JSON object) — still populate the form field
            if (string.IsNullOrEmpty(TryExtractJsonObjectString(raw)) && raw.Trim().Length >= 200)
            {
                const string plainAck =
                    "I've added the text to the customer past story field. You can review and edit it there before saving.";
                var plain = raw.Trim();
                var ai = new ProjectCustomerAiAssistantResponse
                {
                    AssistantMessage = plainAck,
                    SuggestedCustomerPastStory = plain,
                };
                await InstituteAssistantChatHelper.SaveTurnAsync(
                    _context,
                    teacher.InstituteId,
                    teacher.Id,
                    id,
                    canonicalSource,
                    trimmedRequest,
                    plainAck,
                    _logger,
                    cancellationToken);
                return Ok(ai);
            }

            var fallback = raw.Trim();
            await InstituteAssistantChatHelper.SaveTurnAsync(
                _context,
                teacher.InstituteId,
                teacher.Id,
                id,
                canonicalSource,
                trimmedRequest,
                fallback,
                _logger,
                cancellationToken);
            return Ok(new ProjectCustomerAiAssistantResponse
            {
                AssistantMessage = fallback,
                SuggestedCustomerPastStory = null,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in project customer AI assistant for project {ProjectId}", id);
            return StatusCode(500, "An error occurred while running the customer assistant.");
        }
    }

    private static string RenderOriginalBriefForAssistantReply(string? originalDescription)
    {
        if (string.IsNullOrWhiteSpace(originalDescription))
        {
            return string.Empty;
        }

        var raw = originalDescription.Trim();
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.Array)
            {
                return raw;
            }

            var lines = new List<string>();
            foreach (var item in content.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
                var text = item.TryGetProperty("text", out var x) ? x.GetString() : null;
                var clean = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
                if (string.IsNullOrWhiteSpace(clean))
                {
                    continue;
                }

                if (string.Equals(type, "heading", StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add($"- Heading: {clean}");
                }
                else if (string.Equals(type, "paragraph", StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add($"- Paragraph: {clean}");
                }
                else
                {
                    lines.Add($"- {clean}");
                }
            }

            return lines.Count > 0 ? string.Join("\n", lines) : raw;
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    private async Task<IActionResult> PostInstituteProjectAssistantGeneralCoreAsync(
        int id,
        string canonicalSource,
        ProjectInstituteAssistantRequest request,
        CancellationToken cancellationToken)
    {
        const int maxUserRequestLength = 12_000;
        if (string.IsNullOrWhiteSpace(request.UserRequest))
        {
            return BadRequest("userRequest in body is required (non-empty).");
        }

        if (request.UserRequest.Length > maxUserRequestLength)
        {
            return BadRequest($"userRequest is too long (max {maxUserRequestLength} characters).");
        }

        var trimmedRequest = request.UserRequest.Trim();

        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            var teacher = await InstituteStaffAuthHelper.ResolveActiveInstituteTeacherAsync(
                _context, Request, cancellationToken);
            if (teacher == null || teacher.InstituteId != instituteId.Value)
            {
                return Unauthorized("Institute staff context is missing or invalid.");
            }

            var project = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value))
                .Select(p => new
                {
                    p.Title,
                    p.Mission,
                    p.OneLiner,
                    p.ShortBrief,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (project == null)
            {
                return NotFound($"Project with ID {id} not found for this institute context.");
            }

            static string? CoalesceField(string? fromBody, string? fromDb) =>
                !string.IsNullOrWhiteSpace(fromBody) ? fromBody.Trim() : fromDb;

            var nameForPrompt = CoalesceField(request.CurrentTitle, project.Title);
            var missionForPrompt = CoalesceField(request.CurrentMission, project.Mission);
            var oneLinerForPrompt = CoalesceField(request.CurrentOneLiner, project.OneLiner);
            var shortBriefForPrompt = CoalesceField(request.CurrentShortBrief, project.ShortBrief);

            var historyBlock = await InstituteAssistantChatHelper.BuildRecentContextBlockAsync(
                _context,
                teacher.InstituteId,
                teacher.Id,
                id,
                canonicalSource,
                _logger,
                cancellationToken);

            var systemBlock = BuildProjectHeaderAiSystemInstructions();
            var userBlock = BuildProjectHeaderAiUserBlock(
                projectName: nameForPrompt,
                mission: missionForPrompt,
                oneLiner: oneLinerForPrompt,
                shortBrief: shortBriefForPrompt,
                userRequest: trimmedRequest);

            var fullPrompt = systemBlock
                + (string.IsNullOrWhiteSpace(historyBlock) ? "" : "\n\n" + historyBlock.Trim())
                + "\n\n" + userBlock;

            var raw = await _aiService.GenerateTextResponseAsync(fullPrompt);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return StatusCode(502, "The AI assistant did not return a response. Please try again.");
            }

            var parsed = TryParseProjectHeaderAiJsonResponse(raw, out var message, out var suggestions);
            if (parsed)
            {
                var ai = new ProjectHeaderAiAssistantResponse
                {
                    AssistantMessage = message,
                    SuggestedTitle = suggestions.Title,
                    SuggestedMission = suggestions.Mission,
                    SuggestedOneLiner = suggestions.OneLiner,
                    SuggestedShortBrief = suggestions.ShortBrief,
                };
                ApplyHeaderFieldWordLimitsToDto(ai, enforceTitleOneWord: true, enforceTextLimits: true);
                var assistantToStore = !string.IsNullOrWhiteSpace(ai.AssistantMessage) ? ai.AssistantMessage : null;
                await InstituteAssistantChatHelper.SaveTurnAsync(
                    _context,
                    teacher.InstituteId,
                    teacher.Id,
                    id,
                    canonicalSource,
                    trimmedRequest,
                    assistantToStore,
                    _logger,
                    cancellationToken);
                return Ok(ai);
            }

            var fallback = raw.Trim();
            await InstituteAssistantChatHelper.SaveTurnAsync(
                _context,
                teacher.InstituteId,
                teacher.Id,
                id,
                canonicalSource,
                trimmedRequest,
                fallback,
                _logger,
                cancellationToken);
            return Ok(new ProjectHeaderAiAssistantResponse
            {
                AssistantMessage = fallback,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in project header AI assistant for project {ProjectId}", id);
            return StatusCode(500, "An error occurred while running the header assistant.");
        }
    }

    private async Task<IActionResult> PostInstituteProjectAssistantTemplatesCoreAsync(
        int id,
        ProjectInstituteTemplatesAssistantRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserMessage))
        {
            return BadRequest(new { success = false, message = "userMessage in body is required (non-empty)." });
        }

        var instituteId = await ResolveInstituteIdFromAuthContextAsync();
        if (!instituteId.HasValue || instituteId.Value <= 0)
        {
            return Unauthorized(new { success = false, message = "Institute authentication context is missing or invalid." });
        }

        var teacher = await InstituteStaffAuthHelper.ResolveActiveInstituteTeacherAsync(
            _context, Request, cancellationToken);
        if (teacher == null || teacher.InstituteId != instituteId.Value)
        {
            return Unauthorized(new { success = false, message = "Institute staff context is missing or invalid." });
        }

        var project = await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value),
                cancellationToken);
        if (project == null)
        {
            return NotFound(new { success = false, message = $"Project {id} not found for this institute context." });
        }

        string trelloResolved;
        if (request.InstituteTemplateId is int itid && itid > 0)
        {
            var row = await _context.InstituteTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.Id == itid
                         && t.ProjectId == id
                         && t.InstituteId == teacher.InstituteId,
                    cancellationToken);
            if (row == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Institute template {itid} was not found for this project and institute, or is not allowed.",
                });
            }

            trelloResolved = string.IsNullOrWhiteSpace(row.TrelloBoardJson) ? "{}" : row.TrelloBoardJson.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(request.TrelloBoardJson))
        {
            trelloResolved = request.TrelloBoardJson.Trim();
        }
        else
        {
            return BadRequest(new
            {
                success = false,
                message = "Send trelloBoardJson (full template JSON) or set instituteTemplateId to load a saved institute template (recommended; smaller request).",
            });
        }

        var requestForPrompt = new ProjectInstituteTemplatesAssistantRequest
        {
            ModuleId = request.ModuleId,
            InstituteTemplateId = request.InstituteTemplateId,
            TrelloBoardJson = trelloResolved,
            SprintName = request.SprintName,
            RoleName = request.RoleName,
            UserMessage = request.UserMessage,
            CurrentDescription = request.CurrentDescription,
            CurrentChecklistItems = request.CurrentChecklistItems,
            Test = request.Test,
        };

        ProjectModule? module = null;
        if (requestForPrompt.ModuleId > 0)
        {
            module = await _context.ProjectModules
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    m => m.Id == requestForPrompt.ModuleId && m.ProjectId == id,
                    cancellationToken);
        }

        string? historyBlock = null;
        var hb = await InstituteAssistantChatHelper.BuildRecentContextBlockAsync(
            _context,
            teacher.InstituteId,
            teacher.Id,
            id,
            InstituteAssistantChatHelper.SourceTemplates,
            _logger,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(hb))
        {
            historyBlock = hb;
        }

        var systemPrompt = InstituteTaskBuilderAssistantHelper.LoadTaskBuilderSystemPrompt()
            ?? "You are a helpful mentor for task templates. Output JSON only with keys aiReply, description, checklistItems.";

        var userPrompt = InstituteTaskBuilderAssistantHelper.BuildUserPrompt(
            project, module, requestForPrompt, historyBlock);

        if (request.Test)
        {
            return Ok(new
            {
                success = true,
                test = true,
                systemPrompt = InstituteTaskBuilderAssistantHelper.StripDebugMarkers(systemPrompt.Trim()),
                userPrompt,
            });
        }

        try
        {
            var cheapName = _configuration["OpenAI:CheapModel"] ?? "gpt-4o-mini";
            var aiModel = new AIModel
            {
                Name = cheapName,
                Provider = "OpenAI",
                BaseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1",
                MaxTokens = 8192,
                DefaultTemperature = 0.25,
            };

            var (llmText, inputTokens, outputTokens) = await _chatCompletionService.GetChatCompletionAsync(
                aiModel,
                InstituteTaskBuilderAssistantHelper.StripDebugMarkers(systemPrompt.Trim()),
                userPrompt,
                null);

            if (!InstituteTaskBuilderAssistantHelper.TryParseTaskBuilderMentorJson(
                    llmText, out var parsed) || parsed == null)
            {
                return UnprocessableEntity(new
                {
                    success = false,
                    message = "Assistant did not return valid JSON. Try a shorter question or rephrase.",
                    preview = InstituteTaskBuilderAssistantHelper.TruncateForPreview(llmText.Trim(), 4000),
                    inputTokens,
                    outputTokens,
                });
            }

            var aiReply = string.IsNullOrWhiteSpace(parsed.AiReply)
                ? "(No aiReply in model output.)"
                : parsed.AiReply!.Trim();

            List<string>? checklist = null;
            if (parsed.ChecklistItems is { Count: > 0 })
            {
                checklist = parsed.ChecklistItems
                    .Select(s => (s ?? "").Trim())
                    .Where(s => s.Length > 0)
                    .ToList();
                if (checklist.Count == 0)
                {
                    checklist = null;
                }
            }

            var description = string.IsNullOrWhiteSpace(parsed.Description) ? null : parsed.Description!.Trim();

            await InstituteAssistantChatHelper.SaveTurnAsync(
                _context,
                teacher.InstituteId,
                teacher.Id,
                id,
                InstituteAssistantChatHelper.SourceTemplates,
                request.UserMessage.Trim(),
                aiReply,
                _logger,
                cancellationToken);

            return Ok(new
            {
                success = true,
                aiReply,
                description,
                checklistItems = checklist,
                inputTokens,
                outputTokens,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task builder assistant failed for project {ProjectId}", id);
            return StatusCode(500, new { success = false, message = "Task builder assistant failed.", detail = ex.Message });
        }
    }
}
