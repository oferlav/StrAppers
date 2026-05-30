namespace strAppersBackend.Services;

/// <summary>
/// Runs all catalogued AI assessment metrics for every active student on a board after a sprint transition.
/// Only triggered when a new Trello sprint list was actually created (ListCreated = true from ExecuteMergeSprintAsync).
/// </summary>
public interface ISprintAssessmentService
{
    /// <summary>
    /// Runs all <see cref="strAppersBackend.Models.Metric"/> rows that have a non-null Endpoint
    /// for every student with Status=3 on <paramref name="boardId"/> for <paramref name="completedSprintNumber"/>.
    /// Errors per student/metric are logged and swallowed so one failure never blocks the rest.
    /// </summary>
    Task RunForBoardSprintAsync(string boardId, int completedSprintNumber, CancellationToken cancellationToken = default);
}
