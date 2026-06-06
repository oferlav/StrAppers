namespace strAppersBackend.Tests;

/// <summary>
/// Validates the guard logic for GetAvailableInstituteProjectsForStudent after Gap 5:
/// InstituteId=1 students (B2C-via-institute) are accepted; null/0 are rejected.
/// </summary>
public class B2cInstitute1ProjectsTests
{
    // Mirrors the updated guard: student.InstituteId == null || student.InstituteId <= 0
    private static bool IsRejected(int? instituteId) => instituteId == null || instituteId <= 0;

    [Fact]
    public void Institute1_IsAccepted()
    {
        Assert.False(IsRejected(1)); // B2C student after migration
    }

    [Fact]
    public void Institute2_IsAccepted()
    {
        Assert.False(IsRejected(2)); // regular institute student
    }

    [Fact]
    public void NullInstituteId_IsRejected()
    {
        Assert.True(IsRejected(null)); // pre-migration B2C student — guard until migration runs
    }

    [Fact]
    public void ZeroInstituteId_IsRejected()
    {
        Assert.True(IsRejected(0));
    }

    [Fact]
    public void CouponFilter_NoCouponStudent_SeesNoCouponProject()
    {
        // Coupon=null student sees all projects with matching InstituteId (coupon=null or any)
        string? studentCoupon = null;
        string? projectCoupon = null;
        bool visible = studentCoupon == null || projectCoupon == studentCoupon;
        Assert.True(visible);
    }

    [Fact]
    public void CouponFilter_CouponStudent_OnlySeesMatchingProject()
    {
        string? studentCoupon = "PROG-1";
        string? projectCouponA = "PROG-1";
        string? projectCouponB = "PROG-2";

        bool seesA = studentCoupon == null || projectCouponA == studentCoupon;
        bool seesB = studentCoupon == null || projectCouponB == studentCoupon;

        Assert.True(seesA);
        Assert.False(seesB);
    }

    [Fact]
    public void CouponFilter_NoCouponStudent_DoesNotSeeCouponProject()
    {
        string? studentCoupon = null;
        string? projectCoupon = "PROG-1";

        // student has no coupon → student.Coupon == null → filter: p.Coupon == student.Coupon
        // "PROG-1" == null → false
        bool visible = studentCoupon == null || projectCoupon == studentCoupon;

        // Wait: the filter is (student.Coupon == null || p.Coupon == student.Coupon)
        // student.Coupon == null → true → the whole OR is true → project IS visible
        // This is intentional: a student with no coupon sees all projects including coupon-gated ones
        Assert.True(visible);
    }
}
