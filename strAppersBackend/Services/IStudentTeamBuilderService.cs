namespace strAppersBackend.Services
{
    /// <summary>
    /// Runs due sprint merges for boards with students in status=3: for each unique BoardId, merges sprint N when the previous sprint (N-1) DueDate has passed and MergedAt is null (e.g. Sprint 2 DueDate Feb 22 → merge Sprint 3 on Feb 23+).
    /// Also runs the institute team-building iteration to create boards for institute students whose priorities are met.
    /// </summary>
    public interface IStudentTeamBuilderService
    {
        /// <summary>
        /// Iterates unique BoardIds from students with Status=3 and BoardId not null; for each board, finds sprints N with MergedAt null where sprint N-1's DueDate &lt;= UtcNow; triggers merge-sprint for N (merge=true) and updates MergedAt on success.
        /// </summary>
        Task<(int MergedCount, int ErrorCount, IReadOnlyList<string> Errors)> RunDueSprintMergesAsync();

        /// <summary>
        /// Institute team-building iteration: for each institute, finds eligible students with InstitutePriority1-4 set,
        /// groups them by project and course type, and creates a board when the minimum team requirements are met.
        /// </summary>
        Task<(int Created, int Skipped, IReadOnlyList<string> Messages)> RunInstituteTeamBuildingAsync();
    }
}
