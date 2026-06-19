using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using System.Text;

namespace strAppersBackend.Services;

// Handles outbound communication back to Greenhouse when a candidate completes an assessment.
// Inject wherever you mark an assessment complete (e.g. your CacheMetrics completion handler).
//
// Flow:
//   1. CacheMetrics is generated for a student
//   2. Look up student's Coupon → matches AtsAssessmentInstances.ExternalInterviewId
//   3. Call CompleteAssessmentAsync(token, score, profileUrl)
//   4. This updates the instance row and PATCHes Greenhouse's callback URL
//   5. Greenhouse polls /test-status → reads the score
public class GreenhouseAssessmentService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GreenhouseAssessmentService> _logger;

    public GreenhouseAssessmentService(
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GreenhouseAssessmentService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    // Call this when a candidate finishes their assessment (CacheMetrics generated).
    // token = the student's Coupon value = AtsAssessmentInstance.ExternalInterviewId
    public async Task<bool> CompleteAssessmentAsync(string token, decimal score, string profileUrl)
    {
        var instance = await _db.AtsAssessmentInstances
            .FirstOrDefaultAsync(i => i.ExternalInterviewId == token && i.Provider == "greenhouse");

        if (instance is null)
        {
            _logger.LogWarning("CompleteAssessment: no greenhouse instance found for token {Token}", token);
            return false;
        }

        instance.Status    = "complete";
        instance.Score     = score;
        instance.ProfileUrl = profileUrl;
        instance.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await NotifyGreenhouseAsync(instance);
    }

    // Backfills StudentId on the instance when a candidate registers using the token.
    // Call this from the student registration flow after the student row is created.
    public async Task LinkStudentAsync(string token, int studentId)
    {
        var instance = await _db.AtsAssessmentInstances
            .FirstOrDefaultAsync(i => i.ExternalInterviewId == token);

        if (instance is null) return;

        instance.StudentId = studentId;
        instance.Status    = "started";
        instance.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task<bool> NotifyGreenhouseAsync(AtsAssessmentInstance instance)
    {
        if (string.IsNullOrEmpty(instance.CallbackUrl))
        {
            _logger.LogWarning("No CallbackUrl for instance {Id} — cannot notify Greenhouse", instance.Id);
            return false;
        }

        var apiKey = _configuration["Greenhouse:AssessmentApiKey"] ?? string.Empty;
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("Greenhouse:AssessmentApiKey is not configured");
            return false;
        }

        try
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey + ":"));
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");

            var response = await client.PatchAsync(instance.CallbackUrl, null);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Greenhouse PATCH callback returned {Status} for token {Token}",
                    response.StatusCode, instance.ExternalInterviewId);
                return false;
            }

            _logger.LogInformation(
                "Greenhouse notified of completion for token {Token}", instance.ExternalInterviewId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying Greenhouse for token {Token}", instance.ExternalInterviewId);
            return false;
        }
    }
}
