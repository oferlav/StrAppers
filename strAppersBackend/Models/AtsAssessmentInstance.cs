using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

// One row per candidate per job posting — created either by an ATS webhook (send_test)
// or manually when an employer generates a token for a specific candidate.
//
// Token / coupon flow:
//   ExternalInterviewId == the token (coupon) the candidate uses to register on Skill-in.
//   Students.Coupon = ExternalInterviewId
//   Students.InstituteId = the EmployerId on the linked AtsJobPosting
//   When the student registers, StudentId is backfilled here.
public class AtsAssessmentInstance
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    // The job this candidate is being assessed for
    [Column("JobPostingId")]
    public int? JobPostingId { get; set; }

    // 'greenhouse' | 'manual' | etc.
    [Required]
    [Column("Provider")]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    // For Greenhouse: the partner_interview_id we return on send_test.
    // For manual: a generated token the employer hands to the candidate.
    // This value IS the registration coupon (Students.Coupon).
    [Required]
    [Column("ExternalInterviewId")]
    [MaxLength(100)]
    public string ExternalInterviewId { get; set; } = string.Empty;

    // For Greenhouse: partner_test_id. For manual: assessment type slug.
    [Required]
    [Column("ExternalTestId")]
    [MaxLength(100)]
    public string ExternalTestId { get; set; } = string.Empty;

    // Candidate details provided by the ATS or entered manually by the employer
    [Required]
    [Column("CandidateEmail")]
    [MaxLength(255)]
    public string CandidateEmail { get; set; } = string.Empty;

    [Column("CandidateFirstName")]
    [MaxLength(100)]
    public string? CandidateFirstName { get; set; }

    [Column("CandidateLastName")]
    [MaxLength(100)]
    public string? CandidateLastName { get; set; }

    // Greenhouse: greenhouse_profile_url; other ATS: equivalent profile link
    [Column("ExternalProfileUrl")]
    [MaxLength(1000)]
    public string? ExternalProfileUrl { get; set; }

    // Greenhouse: the PATCH URL from send_test payload; null for manual mode
    [Column("CallbackUrl")]
    [MaxLength(1000)]
    public string? CallbackUrl { get; set; }

    // not_started | started | complete
    [Required]
    [Column("Status")]
    [MaxLength(50)]
    public string Status { get; set; } = "not_started";

    [Column("Score")]
    public decimal? Score { get; set; }

    // Our results page URL — returned to the ATS when status = complete
    [Column("ProfileUrl")]
    [MaxLength(1000)]
    public string? ProfileUrl { get; set; }

    // Who triggered the test (recruiter email from Greenhouse, or employer user)
    [Column("SentBy")]
    [MaxLength(255)]
    public string? SentBy { get; set; }

    // Timestamp of the invitation email we sent to the candidate
    [Column("InvitationSentAt")]
    public DateTime? InvitationSentAt { get; set; }

    // Backfilled when the candidate registers on Skill-in using the token
    [Column("StudentId")]
    public int? StudentId { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("UpdatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey(nameof(JobPostingId))]
    public virtual AtsJobPosting? JobPosting { get; set; }
}
