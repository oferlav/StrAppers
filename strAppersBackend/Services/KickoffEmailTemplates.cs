namespace strAppersBackend.Services;

/// <summary>
/// Shared HTML templates for kickoff-meeting-dispute emails (suggest/approve notifications,
/// reset notice), so every trigger point — BoardsController's suggest/approve endpoints,
/// StudentsController's login-triggered reset check, and StudentTeamBuilderService's periodic
/// reset sweep (via the kickoff-reset-notice endpoint) — sends the same look and tone.
/// </summary>
public static class KickoffEmailTemplates
{
    /// <summary>
    /// Wraps kickoff-flow email body HTML in the same styling/signature used by the welcome email
    /// (see WelcomeEmailTemplate_paste.json), with a clickable www.skill-in.com link so recipients
    /// can get straight back to logging in.
    /// </summary>
    public static string BuildKickoffEmailHtml(string bodyHtml)
    {
        return $@"
<html>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
  <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
    {bodyHtml}
    <p style='margin-top: 24px;'>
      Best regards,<br>
      The Skill-in Team<br>
      <a href='https://www.skill-in.com' style='color: #3D76FF; text-decoration: none;'>www.skill-in.com</a>
    </p>
  </div>
</body>
</html>";
    }

    /// <summary>
    /// Sent to every squad member once their board is reset for missing the kickoff-agreement
    /// deadline. Polite/professional/encouraging by design — this isn't anyone's fault, and the
    /// goal is to get them back into the platform picking a new project, not to scold them.
    /// </summary>
    public static string BuildResetNoticeEmailHtml(string firstName)
    {
        var safeFirstName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(firstName) ? "there" : firstName);
        return BuildKickoffEmailHtml($@"
            <p>Hi {safeFirstName},</p>
            <p>Unfortunately, your squad wasn't able to agree on a kickoff meeting time within the deadline, so this project has been closed out for your squad.</p>
            <p>This happens sometimes — no hard feelings! There are plenty of other great projects waiting for you. Log in to Skill-in whenever you're ready to pick a new one and get matched with a new squad.</p>
            <p><a href='https://www.skill-in.com' style='background-color: #3D76FF; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>Log In to Skill-in</a></p>");
    }
}
