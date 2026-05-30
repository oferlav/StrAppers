using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Controllers;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

public class SprintAssessmentService : ISprintAssessmentService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SprintAssessmentService> _logger;

    public SprintAssessmentService(IServiceScopeFactory scopeFactory, ILogger<SprintAssessmentService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RunForBoardSprintAsync(string boardId, int completedSprintNumber, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var metrics = await context.Metrics.AsNoTracking()
            .Where(m => m.Endpoint != null && m.Endpoint != "")
            .ToListAsync(cancellationToken);

        if (metrics.Count == 0)
        {
            _logger.LogInformation("[SPRINT-ASSESSMENT] No metrics with endpoints configured; skipping board {BoardId}.", boardId);
            return;
        }

        var students = await context.Students.AsNoTracking()
            .Where(s => s.BoardId == boardId && s.Status == 3)
            .ToListAsync(cancellationToken);

        if (students.Count == 0)
        {
            _logger.LogInformation("[SPRINT-ASSESSMENT] No active students on board {BoardId}; skipping.", boardId);
            return;
        }

        _logger.LogInformation("[SPRINT-ASSESSMENT] Running {MetricCount} metric(s) for {StudentCount} student(s) on board {BoardId}, completed sprint {Sprint}.",
            metrics.Count, students.Count, boardId, completedSprintNumber);

        foreach (var student in students)
        {
            using var studentScope = _scopeFactory.CreateScope();
            var controller = CreateController(studentScope);

            foreach (var metric in metrics)
            {
                try
                {
                    await DispatchAsync(controller, boardId, student.Id, completedSprintNumber, metric.Id, cancellationToken);
                    _logger.LogInformation("[SPRINT-ASSESSMENT] Metric {MetricId} ({MetricName}) OK for student {StudentId}.", metric.Id, metric.Name, student.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[SPRINT-ASSESSMENT] Metric {MetricId} failed for student {StudentId} on board {BoardId}: {Error}",
                        metric.Id, student.Id, boardId, ex.Message);
                }
            }
        }

        _logger.LogInformation("[SPRINT-ASSESSMENT] Completed board {BoardId}, sprint {Sprint}.", boardId, completedSprintNumber);
    }

    private static MetricsController CreateController(IServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
        scope.ServiceProvider.GetRequiredService<ITrelloService>(),
        scope.ServiceProvider.GetRequiredService<IGitHubService>(),
        scope.ServiceProvider.GetRequiredService<IConfiguration>(),
        scope.ServiceProvider.GetRequiredService<ILogger<MetricsController>>(),
        scope.ServiceProvider.GetRequiredService<IChatCompletionService>(),
        scope.ServiceProvider.GetRequiredService<IHttpClientFactory>(),
        scope.ServiceProvider.GetRequiredService<IOptions<PromptConfig>>(),
        scope.ServiceProvider.GetRequiredService<IMicrosoftGraphService>(),
        scope.ServiceProvider.GetRequiredService<ISmtpEmailService>(),
        scope.ServiceProvider.GetRequiredService<IAzureBlobStorageService>());

    private static async Task DispatchAsync(MetricsController controller, string boardId, int studentId, int sprintNumber, int metricId, CancellationToken ct)
    {
        switch (metricId)
        {
            case 1:
                await controller.Adherence(new MetricsController.AdherenceRequest { BoardId = boardId, StudentId = studentId, SprintNumber = sprintNumber }, ct);
                break;
            case 2:
                await controller.GapAnalysis(new MetricsController.GapAnalysisRequest { BoardId = boardId, StudentId = studentId, SprintNumber = sprintNumber }, ct);
                break;
            case 5:
                await controller.Attendance(boardId, sprintNumber, studentId, ct);
                break;
            case 7:
                await controller.CustomerEngagement(new MetricsController.CustomerEngagementRequest { BoardId = boardId, StudentId = studentId, SprintNumber = sprintNumber }, ct);
                break;
            case 8:
                await controller.MeetingsCommunication(new MetricsController.MeetingsCommunicationRequest { BoardId = boardId, StudentId = studentId, SprintNumber = sprintNumber }, ct);
                break;
        }
    }
}
