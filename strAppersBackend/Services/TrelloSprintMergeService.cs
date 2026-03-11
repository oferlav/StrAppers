using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Services
{
    /// <summary>
    /// Executes merge-sprint: override a live board sprint with SystemBoard (optionally AI-merged) and update ProjectBoardSprintMerge.
    /// </summary>
    public class TrelloSprintMergeService : ITrelloSprintMergeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITrelloService _trelloService;
        private readonly IAIService _aiService;
        private readonly TrelloConfig _trelloConfig;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TrelloSprintMergeService> _logger;

        public TrelloSprintMergeService(
            ApplicationDbContext context,
            ITrelloService trelloService,
            IAIService aiService,
            IOptions<TrelloConfig> trelloConfig,
            IConfiguration configuration,
            ILogger<TrelloSprintMergeService> logger)
        {
            _context = context;
            _trelloService = trelloService;
            _aiService = aiService;
            _trelloConfig = trelloConfig.Value;
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? Error, int CardsCount)> ExecuteMergeSprintAsync(int projectId, string boardId, int sprintNumber, bool merge)
        {
            if (projectId <= 0)
                return (false, "ProjectId is required and must be greater than 0.", 0);
            if (string.IsNullOrWhiteSpace(boardId))
                return (false, "BoardId is required.", 0);
            if (sprintNumber <= 0)
                return (false, "SprintNumber is required and must be greater than 0.", 0);

            var mergeType = string.Equals(_trelloConfig.MergeType, "Add", StringComparison.OrdinalIgnoreCase) ? "Add" : "Merge";

            // MergeType=Add: just add a new sprint list to the board (after the previous sprint list) and upsert ProjectBoardSprintMerge (no SystemBoard, no AI merge).
            // Stops when the template (TrelloBoardJson) has no list/cards for this sprint — no empty sprints.
            if (mergeType == "Add")
            {
                var listName = $"Sprint {sprintNumber}";

                // Load template first: do not add a sprint that isn't in the template (avoids empty sprints).
                var project = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
                if (project == null || string.IsNullOrWhiteSpace(project.TrelloBoardJson))
                {
                    _logger.LogInformation("[MERGE-SPRINT] Add mode: project {ProjectId} has no TrelloBoardJson; skipping sprint {SprintNumber}.", projectId, sprintNumber);
                    return (false, "Project has no TrelloBoardJson; cannot add sprint.", 0);
                }
                TrelloProjectCreationRequest? trelloRequest = null;
                try
                {
                    trelloRequest = JsonSerializer.Deserialize<TrelloProjectCreationRequest>(project.TrelloBoardJson);
                }
                catch (Exception exJson)
                {
                    _logger.LogWarning(exJson, "[MERGE-SPRINT] Add mode: could not deserialize TrelloBoardJson for project {ProjectId}.", projectId);
                    return (false, "Invalid TrelloBoardJson; cannot add sprint.", 0);
                }
                if (!TemplateHasSprint(trelloRequest, listName))
                {
                    _logger.LogInformation("[MERGE-SPRINT] Add mode: template has no list/cards for '{ListName}'; stopping (no empty sprints).", listName);
                    return (false, $"Sprint {sprintNumber} not in template; no more sprints to add.", 0);
                }

                var listsWithPos = await _trelloService.GetBoardListsWithPositionsAsync(boardId);
                var existingList = listsWithPos.FirstOrDefault(l => string.Equals(l.Name, listName, StringComparison.OrdinalIgnoreCase));
                var sprintLengthWeeks = _configuration.GetValue<int>("BusinessLogicConfig:SprintLengthInWeeks", 1);
                DateTime? addModeDueDateUtc = null;
                if (sprintNumber > 1)
                {
                    var prevRow = await _context.ProjectBoardSprintMerges
                        .AsNoTracking()
                        .FirstOrDefaultAsync(m => m.ProjectBoardId == boardId && m.SprintNumber == sprintNumber - 1);
                    if (prevRow?.DueDate != null)
                        addModeDueDateUtc = prevRow.DueDate.Value.AddDays(sprintLengthWeeks * 7);
                }
                if (!addModeDueDateUtc.HasValue)
                    addModeDueDateUtc = DateTime.UtcNow.Date.AddDays((sprintNumber * sprintLengthWeeks * 7) - 1);

                string? newListId;
                int cardsCreated;
                if (!string.IsNullOrEmpty(existingList.Id))
                {
                    // Board already has a list with this name: skip creating list and cards, but still update ProjectBoardSprintMerge.
                    newListId = existingList.Id;
                    cardsCreated = 0;
                    _logger.LogInformation("[MERGE-SPRINT] Add mode: list '{ListName}' already exists on board {BoardId}; skipping list/card creation, updating ProjectBoardSprintMerge only.", listName, boardId);
                }
                else
                {
                    double? posAfterPrevSprint = null;
                    if (sprintNumber > 1)
                    {
                        var prevListName = $"Sprint {sprintNumber - 1}";
                        var idx = -1;
                        for (var i = 0; i < listsWithPos.Count; i++)
                        {
                            if (string.Equals(listsWithPos[i].Name, prevListName, StringComparison.OrdinalIgnoreCase))
                            { idx = i; break; }
                        }
                        if (idx >= 0)
                        {
                            if (idx + 1 < listsWithPos.Count)
                                posAfterPrevSprint = (listsWithPos[idx].Pos + listsWithPos[idx + 1].Pos) / 2.0;
                            else
                                posAfterPrevSprint = listsWithPos[idx].Pos + 65535.0;
                        }
                    }
                    newListId = await _trelloService.AddListToBoardAsync(boardId, listName, posAfterPrevSprint);
                    if (string.IsNullOrEmpty(newListId))
                        return (false, $"Failed to add list '{listName}' to board.", 0);

                    cardsCreated = 0;
                    if (trelloRequest != null)
                    {
                        var (created, createError) = await _trelloService.CreateSprintCardsOnListAsync(boardId, newListId, trelloRequest, listName, addModeDueDateUtc);
                        cardsCreated = created;
                        if (!string.IsNullOrEmpty(createError))
                            _logger.LogWarning("[MERGE-SPRINT] Add mode: created {Count} cards on list '{ListName}', some errors: {Error}", created, listName, createError);
                    }
                }

                try
                {
                    var mergeRecord = await _context.ProjectBoardSprintMerges
                        .FirstOrDefaultAsync(m => m.ProjectBoardId == boardId && m.SprintNumber == sprintNumber);
                    if (mergeRecord == null)
                    {
                        mergeRecord = new ProjectBoardSprintMerge
                        {
                            ProjectBoardId = boardId,
                            SprintNumber = sprintNumber,
                            MergedAt = DateTime.UtcNow,
                            ListId = newListId,
                            DueDate = addModeDueDateUtc
                        };
                        _context.ProjectBoardSprintMerges.Add(mergeRecord);
                    }
                    else
                    {
                        mergeRecord.MergedAt = DateTime.UtcNow;
                        mergeRecord.ListId = newListId;
                        mergeRecord.DueDate = addModeDueDateUtc ?? mergeRecord.DueDate;
                    }

                    var nextSprintNum = sprintNumber + 1;
                    var nextListName = $"Sprint {nextSprintNum}";
                    var nextDueDateUtc = addModeDueDateUtc?.AddDays(sprintLengthWeeks * 7);
                    // Only add a row for the next sprint if the template has that sprint — otherwise we stop (no more "due" merges).
                    var addNextRow = trelloRequest != null && TemplateHasSprint(trelloRequest, nextListName);
                    if (addNextRow)
                    {
                        var nextMergeRecord = await _context.ProjectBoardSprintMerges
                            .FirstOrDefaultAsync(m => m.ProjectBoardId == boardId && m.SprintNumber == nextSprintNum);
                        if (nextMergeRecord == null)
                        {
                            _context.ProjectBoardSprintMerges.Add(new ProjectBoardSprintMerge
                            {
                                ProjectBoardId = boardId,
                                SprintNumber = nextSprintNum,
                                ListId = null,
                                DueDate = nextDueDateUtc,
                                MergedAt = null
                            });
                        }
                        else
                        {
                            nextMergeRecord.DueDate = nextDueDateUtc ?? nextMergeRecord.DueDate;
                        }
                    }
                    else
                    {
                        _logger.LogInformation("[MERGE-SPRINT] Add mode: template has no sprint '{NextListName}'; not adding next row (chain stops).", nextListName);
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("[MERGE-SPRINT] Add mode: added list '{ListName}' for BoardId={BoardId}, SprintNumber={SprintNumber}, next sprint row {NextSprint} created/updated={AddNext}, {CardsCreated} cards.", listName, boardId, sprintNumber, nextSprintNum, addNextRow, cardsCreated);
                    return (true, null, cardsCreated);
                }
                catch (Exception exDb)
                {
                    _logger.LogError(exDb, "[MERGE-SPRINT] Add mode: failed to upsert ProjectBoardSprintMerge for BoardId={BoardId}, SprintNumber={SprintNumber}", boardId, sprintNumber);
                    return (false, exDb.Message, 0);
                }
            }

            // Merge mode: require SystemBoard and merge/override
            var projectBoard = await _context.ProjectBoards
                .FirstOrDefaultAsync(pb => pb.Id == boardId && pb.ProjectId == projectId);
            if (projectBoard == null)
                return (false, $"Board {boardId} not found for project {projectId}.", 0);
            var systemBoardId = projectBoard.SystemBoardId;
            if (string.IsNullOrWhiteSpace(systemBoardId))
                return (false, "This board has no linked SystemBoard. Merge-sprint requires a SystemBoard (CreatePMEmptyBoard).", 0);

            var sprintNameNoSpace = $"Sprint{sprintNumber}";
            var sprintNameWithSpace = $"Sprint {sprintNumber}";

            var systemSprint = await _trelloService.GetSprintFromBoardAsync(systemBoardId, sprintNameNoSpace)
                ?? await _trelloService.GetSprintFromBoardAsync(systemBoardId, sprintNameWithSpace);
            if (systemSprint == null || systemSprint.Cards == null || systemSprint.Cards.Count == 0)
                return (false, $"Sprint {sprintNumber} not found on SystemBoard or has no cards.", 0);

            var liveSprint = await _trelloService.GetSprintFromBoardAsync(boardId, sprintNameNoSpace)
                ?? await _trelloService.GetSprintFromBoardAsync(boardId, sprintNameWithSpace);
            if (liveSprint == null)
                return (false, $"Sprint list {sprintNumber} not found on live board.", 0);

            List<SprintSnapshotCard> cardsToApply;
            if (!merge)
            {
                cardsToApply = systemSprint.Cards;
            }
            else
            {
                var liveCardsJson = JsonSerializer.Serialize(liveSprint.Cards);
                var systemCardsJson = JsonSerializer.Serialize(systemSprint.Cards);
                // PromptType: SprintPlanning — Keep (config; template with placeholders {{LiveSprintJson}}, {{SystemSprintJson}})
                var promptTemplate = _trelloConfig.SprintMergePrompt;
                if (string.IsNullOrWhiteSpace(promptTemplate))
                {
                    promptTemplate = "You are given two representations of the same sprint: the LIVE sprint (current state on the board) and the SYSTEM sprint (template from SystemBoard).\nMerge them according to these rules:\n- Keep live card customizations (descriptions, checklist changes) when they correspond to the same task (match by role and similar name).\n- Include all template cards from the system sprint; add any that are missing on the live board.\n- Preserve RoleName and CardId when present. Use system sprint as the structure reference.\nOutput ONLY a single JSON array of cards, each object with exactly these keys: Name (string), Description (string), DueDate (ISO date string or null), RoleName (string), ChecklistItems (array of strings), CardId (string or null). No markdown, no explanation.\n\nLIVE sprint (current board):\n{{LiveSprintJson}}\n\nSYSTEM sprint (template):\n{{SystemSprintJson}}\n\nOutput the merged JSON array only:";
                }
                var prompt = promptTemplate
                    .Replace("{{LiveSprintJson}}", liveCardsJson, StringComparison.Ordinal)
                    .Replace("{{SystemSprintJson}}", systemCardsJson, StringComparison.Ordinal);

                var mergedJson = await _aiService.GenerateTextResponseAsync(prompt);
                if (string.IsNullOrWhiteSpace(mergedJson))
                    return (false, "AI did not return a merged sprint.", 0);

                var cleaned = mergedJson.Trim();
                if (cleaned.StartsWith("```"))
                {
                    var start = cleaned.IndexOf('\n') + 1;
                    var end = cleaned.IndexOf("```", start, StringComparison.Ordinal);
                    cleaned = end > start ? cleaned.Substring(start, end - start).Trim() : cleaned.Substring(start).Trim();
                }
                List<SprintSnapshotCard>? mergedCards;
                try
                {
                    mergedCards = JsonSerializer.Deserialize<List<SprintSnapshotCard>>(cleaned);
                }
                catch
                {
                    return (false, "AI response could not be parsed as sprint cards JSON.", 0);
                }
                if (mergedCards == null || mergedCards.Count == 0)
                    return (false, "Merged sprint had no cards.", 0);
                cardsToApply = mergedCards;
            }

            var (overrideSuccess, overrideError) = await _trelloService.OverrideSprintOnBoardAsync(boardId, liveSprint.ListId, cardsToApply);
            if (!overrideSuccess)
                return (false, overrideError ?? "Failed to override sprint on board.", 0);

            // DueDate for row N = this sprint's own first card DueDate (trigger for merging N+1 is row N.DueDate has passed).
            var dueDateRaw = cardsToApply.Count > 0 ? cardsToApply[0].DueDate : null;
            var dueDateUtc = dueDateRaw.HasValue ? ToUtcForDb(dueDateRaw.Value) : (DateTime?)null;
            _logger.LogInformation("[MERGE-SPRINT] Upserting ProjectBoardSprintMerge: BoardId={BoardId}, SprintNumber={SprintNumber}, DueDate={DueDate}", boardId, sprintNumber, dueDateUtc);
            try
            {
                var mergeRecord = await _context.ProjectBoardSprintMerges
                    .FirstOrDefaultAsync(m => m.ProjectBoardId == boardId && m.SprintNumber == sprintNumber);
                if (mergeRecord == null)
                {
                    mergeRecord = new ProjectBoardSprintMerge
                    {
                        ProjectBoardId = boardId,
                        SprintNumber = sprintNumber,
                        MergedAt = DateTime.UtcNow,
                        ListId = liveSprint.ListId,
                        DueDate = dueDateUtc
                    };
                    _context.ProjectBoardSprintMerges.Add(mergeRecord);
                    _logger.LogInformation("[MERGE-SPRINT] ProjectBoardSprintMerge added for BoardId={BoardId}, SprintNumber={SprintNumber}", boardId, sprintNumber);
                }
                else
                {
                    mergeRecord.MergedAt = DateTime.UtcNow;
                    mergeRecord.ListId = liveSprint.ListId;
                    mergeRecord.DueDate = dueDateUtc ?? mergeRecord.DueDate;
                    _logger.LogInformation("[MERGE-SPRINT] ProjectBoardSprintMerge updated for BoardId={BoardId}, SprintNumber={SprintNumber}", boardId, sprintNumber);
                }
                await _context.SaveChangesAsync();
                _logger.LogInformation("[MERGE-SPRINT] ProjectBoardSprintMerge saved successfully for BoardId={BoardId}.", boardId);
            }
            catch (Exception exDb)
            {
                _logger.LogError(exDb, "[MERGE-SPRINT] Failed to upsert ProjectBoardSprintMerge for BoardId={BoardId}, SprintNumber={SprintNumber}: {Message}", boardId, sprintNumber, exDb.Message);
            }

            // When NextSprintOnlyVisability is true, ensure the next empty sprint exists on the live board (from Projects.TrelloBoardJson)
            // and insert ProjectBoardSprintMerge for that sprint so "run due sprint merges" can merge it after this sprint's DueDate passes.
            if (_trelloConfig.NextSprintOnlyVisability)
            {
                try
                {
                    var project = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
                    if (project != null && !string.IsNullOrWhiteSpace(project.TrelloBoardJson))
                    {
                        var trelloRequest = JsonSerializer.Deserialize<TrelloProjectCreationRequest>(project.TrelloBoardJson);
                        if (trelloRequest != null)
                        {
                            // Compute next sprint DueDate = this sprint's due + one sprint length (do not use template dates from DB).
                            var sprintLengthWeeks = _configuration.GetValue<int>("BusinessLogicConfig:SprintLengthInWeeks", 1);
                            DateTime? nextDueDateUtc = null;
                            if (dueDateUtc.HasValue)
                                nextDueDateUtc = dueDateUtc.Value.AddDays(7 * sprintLengthWeeks);

                            var nextListId = await _trelloService.EnsureNextEmptySprintOnBoardAsync(boardId, trelloRequest, sprintNumber + 1, nextDueDateUtc);
                            if (!string.IsNullOrEmpty(nextListId))
                            {
                                var nextSprintNum = sprintNumber + 1;
                                var nextMergeRecord = await _context.ProjectBoardSprintMerges
                                    .FirstOrDefaultAsync(m => m.ProjectBoardId == boardId && m.SprintNumber == nextSprintNum);
                                if (nextMergeRecord == null)
                                {
                                    nextMergeRecord = new ProjectBoardSprintMerge
                                    {
                                        ProjectBoardId = boardId,
                                        SprintNumber = nextSprintNum,
                                        ListId = nextListId,
                                        DueDate = nextDueDateUtc,
                                        MergedAt = null
                                    };
                                    _context.ProjectBoardSprintMerges.Add(nextMergeRecord);
                                    _logger.LogInformation("[MERGE-SPRINT] ProjectBoardSprintMerge added for next sprint BoardId={BoardId}, SprintNumber={SprintNumber}, ListId={ListId}, DueDate={DueDate}", boardId, nextSprintNum, nextListId, nextDueDateUtc);
                                }
                                else
                                {
                                    nextMergeRecord.ListId = nextListId;
                                    nextMergeRecord.DueDate = nextDueDateUtc ?? nextMergeRecord.DueDate;
                                    _logger.LogInformation("[MERGE-SPRINT] ProjectBoardSprintMerge updated for next sprint BoardId={BoardId}, SprintNumber={SprintNumber}", boardId, nextSprintNum);
                                }
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                }
                catch (Exception exNext)
                {
                    _logger.LogWarning(exNext, "[MERGE-SPRINT] Failed to ensure next empty sprint on board {BoardId}: {Message}", boardId, exNext.Message);
                }
            }

            return (true, null, cardsToApply.Count);
        }

        /// <summary>Returns true if the template has a list or any cards for the given sprint list name (e.g. "Sprint 2").</summary>
        private static bool TemplateHasSprint(TrelloProjectCreationRequest? request, string listName)
        {
            if (request?.SprintPlan == null)
                return false;
            if (request.SprintPlan.Lists != null && request.SprintPlan.Lists.Any(l => string.Equals(l.Name, listName, StringComparison.OrdinalIgnoreCase)))
                return true;
            if (request.SprintPlan.Cards != null && request.SprintPlan.Cards.Any(c => string.Equals(c.ListName, listName, StringComparison.OrdinalIgnoreCase)))
                return true;
            return false;
        }

        private static DateTime ToUtcForDb(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value;
            if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}
