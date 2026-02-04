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
        private readonly ILogger<TrelloSprintMergeService> _logger;

        public TrelloSprintMergeService(
            ApplicationDbContext context,
            ITrelloService trelloService,
            IAIService aiService,
            IOptions<TrelloConfig> trelloConfig,
            ILogger<TrelloSprintMergeService> logger)
        {
            _context = context;
            _trelloService = trelloService;
            _aiService = aiService;
            _trelloConfig = trelloConfig.Value;
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

            return (true, null, cardsToApply.Count);
        }

        private static DateTime ToUtcForDb(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value;
            if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}
