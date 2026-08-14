using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// get-students applicant filtering must compare roles by NAME, not row id — the Roles catalog
/// holds duplicate rows per conceptual role (global default, institute-1 copy, per-squad copies),
/// so id comparisons over/under-exclude depending on which duplicate a student happens to hold.
/// </summary>
public class ProjectApplicantFilterTests
{
    private static ProjectsController.ApplicantRoleView Applicant(int id, string? roleName, DateTime? updatedAt = null) =>
        new(id, roleName, updatedAt);

    // ── The reported bug: FS viewer must see the PM applicant ────────────────

    [Fact]
    public void FullStackViewer_SeesPmApplicant()
    {
        var students = new List<ProjectsController.ApplicantRoleView> { Applicant(1, "Product Manager") };

        var survivors = ProjectsController.FilterApplicantsForCandidate(students, "Full Stack Developer", currentStudentId: null);

        Assert.Contains(1, survivors);
    }

    [Fact]
    public void SameConceptualRole_DifferentDuplicateRow_IsStillExcluded()
    {
        // Viewer holds squad-duplicated PM row (id 122); applicant holds the default PM row (id 1).
        // Ids differ, names match → the applicant competes for the viewer's seat → excluded.
        var students = new List<ProjectsController.ApplicantRoleView> { Applicant(1, "Product Manager") };

        var survivors = ProjectsController.FilterApplicantsForCandidate(students, "Product Manager", currentStudentId: null);

        Assert.Empty(survivors);
    }

    [Fact]
    public void SameRoleName_DifferentCasingAndSpacing_IsExcluded()
    {
        var students = new List<ProjectsController.ApplicantRoleView> { Applicant(1, "  product manager ") };

        var survivors = ProjectsController.FilterApplicantsForCandidate(students, "Product Manager", currentStudentId: null);

        Assert.Empty(survivors);
    }

    [Fact]
    public void Self_AlwaysKept_EvenWithSameRole()
    {
        var students = new List<ProjectsController.ApplicantRoleView> { Applicant(7, "Product Manager") };

        var survivors = ProjectsController.FilterApplicantsForCandidate(students, "Product Manager", currentStudentId: 7);

        Assert.Contains(7, survivors);
    }

    // ── Dedupe by conceptual role, not row id ─────────────────────────────────

    [Fact]
    public void TwoPmApplicants_ViaDifferentDuplicateRows_OnlyOneSurvives()
    {
        var earlier = new DateTime(2026, 7, 1);
        var later = new DateTime(2026, 7, 10);
        var students = new List<ProjectsController.ApplicantRoleView>
        {
            Applicant(1, "Product Manager", later),   // default row holder
            Applicant(2, "Product Manager", earlier), // squad-duplicate row holder
        };

        var survivors = ProjectsController.FilterApplicantsForCandidate(students, "Full Stack Developer", currentStudentId: null);

        Assert.Single(survivors);
        Assert.Contains(2, survivors); // earliest UpdatedAt wins the slot
    }

    [Fact]
    public void DifferentRoles_BothSurvive()
    {
        var students = new List<ProjectsController.ApplicantRoleView>
        {
            Applicant(1, "Product Manager"),
            Applicant(2, "UI/UX Designer"),
        };

        var survivors = ProjectsController.FilterApplicantsForCandidate(students, "Full Stack Developer", currentStudentId: null);

        Assert.Equal(2, survivors.Count);
    }

    // ── Developer pairing rule preserved (already name-based) ────────────────

    [Fact]
    public void FullStackViewer_ExcludesSplitDeveloperApplicants()
    {
        var students = new List<ProjectsController.ApplicantRoleView>
        {
            Applicant(1, "Backend Developer"),
            Applicant(2, "Product Manager"),
        };

        var survivors = ProjectsController.FilterApplicantsForCandidate(students, "Full Stack Developer", currentStudentId: null);

        Assert.DoesNotContain(1, survivors);
        Assert.Contains(2, survivors);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void NoCandidateRole_NoSameRoleExclusion_DedupeStillRuns()
    {
        var students = new List<ProjectsController.ApplicantRoleView>
        {
            Applicant(1, "Product Manager", new DateTime(2026, 7, 1)),
            Applicant(2, "Product Manager", new DateTime(2026, 7, 2)),
        };

        var survivors = ProjectsController.FilterApplicantsForCandidate(students, candidateRoleName: null, currentStudentId: null);

        Assert.Single(survivors);
        Assert.Contains(1, survivors);
    }

    [Fact]
    public void NullRoleNameApplicant_NotExcluded_ByCandidateComparison()
    {
        var students = new List<ProjectsController.ApplicantRoleView> { Applicant(1, null) };

        var survivors = ProjectsController.FilterApplicantsForCandidate(students, "Product Manager", currentStudentId: null);

        Assert.Contains(1, survivors);
    }

    // ── Role-type courses: every team member shares one role ─────────────────
    // get-students passes candidateRoleName: null (no same-role exclusion) and
    // perRoleLimit: RoleCount instead of the Squad-mode dedupe-to-one.

    [Fact]
    public void RoleCourse_KeepsUpToRoleCountApplicantsOnTheSameRole()
    {
        var students = new List<ProjectsController.ApplicantRoleView>
        {
            Applicant(1, "Data Analyst", new DateTime(2026, 7, 1)),
            Applicant(2, "Data Analyst", new DateTime(2026, 7, 2)),
            Applicant(3, "Data Analyst", new DateTime(2026, 7, 3)),
            Applicant(4, "Data Analyst", new DateTime(2026, 7, 4)),
        };

        var survivors = ProjectsController.FilterApplicantsForCandidate(
            students, candidateRoleName: null, currentStudentId: 3, perRoleLimit: 3);

        Assert.Equal(3, survivors.Count);
        Assert.Contains(3, survivors);            // self always kept
        Assert.Contains(1, survivors);            // then earliest UpdatedAt
        Assert.Contains(2, survivors);
        Assert.DoesNotContain(4, survivors);      // beyond RoleCount
    }

    [Fact]
    public void RoleCourse_RosterSmallerThanRoleCount_KeepsEveryone()
    {
        var students = new List<ProjectsController.ApplicantRoleView>
        {
            Applicant(1, "Data Analyst", new DateTime(2026, 7, 1)),
            Applicant(2, "Data Analyst", new DateTime(2026, 7, 2)),
        };

        var survivors = ProjectsController.FilterApplicantsForCandidate(
            students, candidateRoleName: null, currentStudentId: 1, perRoleLimit: 4);

        Assert.Equal(2, survivors.Count);
    }

    [Fact]
    public void PerRoleLimitDefaultsToOne_SquadBehaviourUnchanged()
    {
        var students = new List<ProjectsController.ApplicantRoleView>
        {
            Applicant(1, "Data Analyst", new DateTime(2026, 7, 1)),
            Applicant(2, "Data Analyst", new DateTime(2026, 7, 2)),
        };

        var survivors = ProjectsController.FilterApplicantsForCandidate(
            students, candidateRoleName: null, currentStudentId: null);

        Assert.Single(survivors);
        Assert.Contains(1, survivors);
    }
}
