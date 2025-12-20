using System.Text.Json;
using System.Text.Json.Serialization;

namespace strAppersBackend.Models;

// Request DTO
public class ParseResumeRequest
{
    [JsonPropertyName("fileBase64")]
    public string FileBase64 { get; set; } = string.Empty;
}

// Response DTOs
public class ParseResumeResponse
{
    [JsonPropertyName("candidateMetaData")]
    public CandidateMetaData? CandidateMetaData { get; set; }
    
    [JsonPropertyName("professionalData")]
    public ProfessionalData? ProfessionalData { get; set; }
    
    [JsonPropertyName("affindaStructuredData")]
    public JsonElement? AffindaStructuredData { get; set; }  // What Affinda parsed/structured
    
    [JsonPropertyName("rawAffindaResponse")]
    public JsonElement? RawAffindaResponse { get; set; }  // Complete raw response
}

public class CandidateMetaData
{
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }
    
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }
    
    [JsonPropertyName("location")]
    public string? Location { get; set; }
    
    [JsonPropertyName("linkedinUrl")]
    public string? LinkedInUrl { get; set; }
    
    // Additional fields extracted from Affinda
    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; set; }
    
    [JsonPropertyName("headshot")]
    public string? Headshot { get; set; }
    
    [JsonPropertyName("nationality")]
    public string? Nationality { get; set; }
    
    [JsonPropertyName("availability")]
    public string? Availability { get; set; }
    
    [JsonPropertyName("preferredWorkLocation")]
    public string? PreferredWorkLocation { get; set; }
    
    [JsonPropertyName("willingToRelocate")]
    public bool? WillingToRelocate { get; set; }
    
    [JsonPropertyName("rightToWork")]
    public string? RightToWork { get; set; }
}

public class ProfessionalData
{
    [JsonPropertyName("detectedJobTitle")]
    public string? DetectedJobTitle { get; set; }
    
    [JsonPropertyName("totalYearsExperience")]
    public int? TotalYearsExperience { get; set; }
    
    [JsonPropertyName("skills")]
    public List<string> Skills { get; set; } = new List<string>();
    
    [JsonPropertyName("educationLevel")]
    public string? EducationLevel { get; set; }
    
    // Additional fields extracted from Affinda
    [JsonPropertyName("objective")]
    public string? Objective { get; set; }
    
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
    
    [JsonPropertyName("expectedSalary")]
    public string? ExpectedSalary { get; set; }
    
    [JsonPropertyName("education")]
    public List<EducationDetail> Education { get; set; } = new List<EducationDetail>();
    
    [JsonPropertyName("workExperience")]
    public List<WorkExperienceDetail> WorkExperience { get; set; } = new List<WorkExperienceDetail>();
    
    [JsonPropertyName("projects")]
    public List<string> Projects { get; set; } = new List<string>();
    
    [JsonPropertyName("achievements")]
    public List<string> Achievements { get; set; } = new List<string>();
    
    [JsonPropertyName("associations")]
    public List<string> Associations { get; set; } = new List<string>();
    
    [JsonPropertyName("patents")]
    public List<string> Patents { get; set; } = new List<string>();
    
    [JsonPropertyName("publications")]
    public List<string> Publications { get; set; } = new List<string>();
    
    [JsonPropertyName("hobbies")]
    public List<string> Hobbies { get; set; } = new List<string>();
    
    [JsonPropertyName("referees")]
    public List<string> Referees { get; set; } = new List<string>();
    
    [JsonPropertyName("languages")]
    public List<LanguageDetail> Languages { get; set; } = new List<LanguageDetail>();
}

// Affinda API Response Models (for deserialization)
// Affinda API v3 can return different structures - handle both
public class AffindaResumeResponse
{
    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; } // Can be empty object or contain document data
    
    [JsonPropertyName("meta")]
    public AffindaMeta? Meta { get; set; }
    
    [JsonPropertyName("error")]
    public AffindaError? Error { get; set; }
    
    // Direct document structure (v3 API may return document directly)
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }
    
    [JsonPropertyName("document")]
    public AffindaDocument? Document { get; set; }
}

public class AffindaMeta
{
    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }
    
    [JsonPropertyName("ready")]
    public bool Ready { get; set; }
    
    [JsonPropertyName("failed")]
    public bool Failed { get; set; }
}

public class AffindaResumeData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }
    
    [JsonPropertyName("document")]
    public AffindaDocument? Document { get; set; }
}

public class AffindaDocument
{
    [JsonPropertyName("data")]
    public AffindaResumeDataContent? Data { get; set; }
}

public class AffindaResumeDataContent
{
    [JsonPropertyName("name")]
    public AffindaName? Name { get; set; }
    
    [JsonPropertyName("phoneNumbers")]
    public List<string>? PhoneNumbers { get; set; }
    
    [JsonPropertyName("emails")]
    public List<string>? Emails { get; set; }
    
    [JsonPropertyName("location")]
    public AffindaLocation? Location { get; set; }
    
    [JsonPropertyName("websites")]
    public List<string>? Websites { get; set; }
    
    [JsonPropertyName("profession")]
    public string? Profession { get; set; }
    
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
    
    [JsonPropertyName("totalYearsExperience")]
    public int? TotalYearsExperience { get; set; }
    
    [JsonPropertyName("skills")]
    public List<AffindaSkill>? Skills { get; set; }
    
    [JsonPropertyName("education")]
    public List<AffindaEducation>? Education { get; set; }
    
    [JsonPropertyName("workExperience")]
    public List<AffindaWorkExperience>? WorkExperience { get; set; }
}

public class AffindaName
{
    [JsonPropertyName("first")]
    public string? First { get; set; }
    
    [JsonPropertyName("last")]
    public string? Last { get; set; }
    
    [JsonPropertyName("raw")]
    public string? Raw { get; set; }
}

public class AffindaLocation
{
    [JsonPropertyName("rawInput")]
    public string? RawInput { get; set; }
    
    [JsonPropertyName("city")]
    public string? City { get; set; }
    
    [JsonPropertyName("state")]
    public string? State { get; set; }
    
    [JsonPropertyName("country")]
    public string? Country { get; set; }
    
    [JsonPropertyName("formatted")]
    public string? Formatted { get; set; }
}

public class AffindaSkill
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class AffindaEducation
{
    [JsonPropertyName("accreditation")]
    public AffindaAccreditation? Accreditation { get; set; }
}

public class AffindaAccreditation
{
    [JsonPropertyName("education")]
    public string? Education { get; set; }
    
    [JsonPropertyName("level")]
    public string? Level { get; set; }
}

public class AffindaWorkExperience
{
    [JsonPropertyName("jobTitle")]
    public string? JobTitle { get; set; }
    
    [JsonPropertyName("dates")]
    public AffindaDates? Dates { get; set; }
}

public class AffindaDates
{
    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }
    
    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }
}

public class AffindaError
{
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }
    
    [JsonPropertyName("errorDetail")]
    public string? ErrorDetail { get; set; }
}

public class AffindaWorkspace
{
    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("organization")]
    public string? Organization { get; set; }
}

// DTOs for extracted structured data
public class EducationDetail
{
    [JsonPropertyName("organization")]
    public string? Organization { get; set; }
    
    [JsonPropertyName("level")]
    public string? Level { get; set; }
    
    [JsonPropertyName("major")]
    public string? Major { get; set; }
    
    [JsonPropertyName("minor")]
    public string? Minor { get; set; }
    
    [JsonPropertyName("location")]
    public string? Location { get; set; }
    
    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }
    
    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }
    
    [JsonPropertyName("grade")]
    public string? Grade { get; set; }
    
    [JsonPropertyName("accreditation")]
    public string? Accreditation { get; set; }
}

public class WorkExperienceDetail
{
    [JsonPropertyName("jobTitle")]
    public string? JobTitle { get; set; }
    
    [JsonPropertyName("organization")]
    public string? Organization { get; set; }
    
    [JsonPropertyName("location")]
    public string? Location { get; set; }
    
    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }
    
    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class LanguageDetail
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("proficiency")]
    public string? Proficiency { get; set; }
}

