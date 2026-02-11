namespace strAppersBackend.Services
{
    /// <summary>
    /// Runs due sprint merges for boards with students in status=3: for each unique BoardId, merges sprint N when the previous sprint (N-1) DueDate has passed and MergedAt is null (e.g. Sprint 2 DueDate Feb 22 → merge Sprint 3 on Feb 23+).
    /// </summary>
    public interface IStudentTeamBuilderService
    {
        /// <summary>
        /// Iterates unique BoardIds from students with Status=3 and BoardId not null; for each board, finds sprints N with MergedAt null where sprint N-1's DueDate &lt;= UtcNow; triggers merge-sprint for N (merge=true) and updates MergedAt on success.
        /// </summary>
        /// <returns>Merged count, error count, and list of error messages.</returns>
        Task<(int MergedCount, int ErrorCount, IReadOnlyList<string> Errors)> RunDueSprintMergesAsync();
    }
}
