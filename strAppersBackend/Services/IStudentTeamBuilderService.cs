namespace strAppersBackend.Services
{
    /// <summary>
    /// Runs due sprint merges for boards with students in status=3: for each unique BoardId, merges sprints where DueDate has passed and MergedAt is null.
    /// </summary>
    public interface IStudentTeamBuilderService
    {
        /// <summary>
        /// Iterates unique BoardIds from students with Status=3 and BoardId not null; for each board, finds ProjectBoardSprintMerge rows where MergedAt is null and DueDate &lt; UtcNow; triggers merge-sprint (merge=true) and updates MergedAt on success.
        /// </summary>
        /// <returns>Merged count, error count, and list of error messages.</returns>
        Task<(int MergedCount, int ErrorCount, IReadOnlyList<string> Errors)> RunDueSprintMergesAsync();
    }
}
