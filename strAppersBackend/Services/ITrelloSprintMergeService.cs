namespace strAppersBackend.Services
{
    /// <summary>
    /// Executes merge-sprint: override a live board sprint with SystemBoard (optionally AI-merged) and update ProjectBoardSprintMerge.
    /// </summary>
    public interface ITrelloSprintMergeService
    {
        /// <summary>
        /// Executes the merge-sprint flow: resolve SystemBoard, fetch sprints, optionally AI-merge, override on live board, upsert MergedAt.
        /// </summary>
        /// <param name="projectId">Project ID.</param>
        /// <param name="boardId">Live board Trello ID.</param>
        /// <param name="sprintNumber">Sprint number (e.g. 1 for Sprint1).</param>
        /// <param name="merge">When true, merge live and system sprint via AI; when false, use system sprint only.</param>
        /// <returns>Success, error message if failed, and cards count on success.</returns>
        Task<(bool Success, string? Error, int CardsCount)> ExecuteMergeSprintAsync(int projectId, string boardId, int sprintNumber, bool merge);
    }
}
