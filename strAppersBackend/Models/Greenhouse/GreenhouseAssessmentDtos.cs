using System.Text.Json.Serialization;

namespace strAppersBackend.Models.Greenhouse;

// ── GET list-tests ─────────────────────────────────────────────────────────────
// Greenhouse calls this to populate the test picker when setting up an interview stage.

public class ListTestsResponseItem
{
    [JsonPropertyName("partner_test_id")]
    public string PartnerTestId { get; set; } = string.Empty;

    [JsonPropertyName("partner_test_name")]
    public string PartnerTestName { get; set; } = string.Empty;
}

// ── POST send-test ─────────────────────────────────────────────────────────────
// Greenhouse calls this when a recruiter sends a test to a candidate.

public class SendTestRequest
{
    [JsonPropertyName("partner_test_id")]
    public string PartnerTestId { get; set; } = string.Empty;

    [JsonPropertyName("candidate")]
    public SendTestCandidate Candidate { get; set; } = new();

    [JsonPropertyName("sent_by")]
    public string? SentBy { get; set; }

    // Greenhouse callback URL — we PATCH this when the candidate finishes.
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class SendTestCandidate
{
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("preferred_name")]
    public string? PreferredName { get; set; }

    [JsonPropertyName("resume_url")]
    public string? ResumeUrl { get; set; }

    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("greenhouse_profile_url")]
    public string? GreenhouseProfileUrl { get; set; }
}

public class SendTestResponse
{
    [JsonPropertyName("partner_interview_id")]
    public string PartnerInterviewId { get; set; } = string.Empty;
}

// ── GET test-status ────────────────────────────────────────────────────────────
// Greenhouse polls this hourly (or after we PATCH the callback) to retrieve results.

public class TestStatusResponse
{
    [JsonPropertyName("partner_status")]
    public string PartnerStatus { get; set; } = string.Empty;  // not_started | started | complete

    // Required by Greenhouse when status is "complete".
    [JsonPropertyName("partner_profile_url")]
    public string? PartnerProfileUrl { get; set; }

    [JsonPropertyName("partner_score")]
    public decimal? PartnerScore { get; set; }

    [JsonPropertyName("metadata")]
    public object? Metadata { get; set; }
}

// ── POST response-error ────────────────────────────────────────────────────────
// Greenhouse calls this to report errors it encountered when calling our endpoints.

public class ResponseErrorRequest
{
    [JsonPropertyName("api_call")]
    public string ApiCall { get; set; } = string.Empty;

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    [JsonPropertyName("partner_test_id")]
    public string? PartnerTestId { get; set; }

    [JsonPropertyName("partner_test_name")]
    public string? PartnerTestName { get; set; }

    [JsonPropertyName("partner_interview_id")]
    public string? PartnerInterviewId { get; set; }

    [JsonPropertyName("candidate_email")]
    public string? CandidateEmail { get; set; }
}
