using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

public interface ISmtpEmailService
{
    Task<bool> SendMeetingEmailAsync(string recipientEmail, string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string meetingDescription = "");
    Task<bool> SendBulkMeetingEmailsAsync(List<string> recipientEmails, string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string meetingDescription = "");
    Task<bool> SendWelcomeEmailAsync(string recipientEmail, string firstName, string projectName, int projectLengthWeeks);
    Task<bool> SendBulkWelcomeEmailsAsync(List<(string Email, string FirstName)> recipients, string projectName, int projectLengthWeeks);
    Task<bool> SendMeetingEmailWithSenderAsync(string recipientEmail, string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string senderEmail, string senderName, string? customMessage = null, string? organizationName = null);
}

public class SmtpEmailService : ISmtpEmailService
{
    private readonly SmtpConfig _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpConfig> config, ILogger<SmtpEmailService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public async Task<bool> SendMeetingEmailAsync(string recipientEmail, string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string meetingDescription = "")
    {
        try
        {
            _logger.LogInformation("Sending SMTP meeting email to {Email} for meeting: {Title}", recipientEmail, meetingTitle);
            _logger.LogInformation("SMTP Configuration - Host: {Host}, Port: {Port}, From: {FromEmail}, Security: {Security}", 
                _config.Host, _config.Port, _config.FromEmail, _config.Security);

            using var client = CreateSmtpClient();
            using var message = CreateEmailMessage(recipientEmail, meetingTitle, startTime, endTime, meetingLink, meetingDescription);

            _logger.LogInformation("SMTP email details - To: {To}, Subject: {Subject}, Meeting Link: {Link}", 
                recipientEmail, message.Subject, meetingLink);

            await client.SendMailAsync(message);

            _logger.LogInformation("SMTP meeting email sent successfully to {Email}", recipientEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMTP meeting email to {Email}: {Message}. Check: 1) SMTP server configuration, 2) Email credentials, 3) Network connectivity, 4) Email server status", recipientEmail, ex.Message);
            return false;
        }
    }

    public async Task<bool> SendBulkMeetingEmailsAsync(List<string> recipientEmails, string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string meetingDescription = "")
    {
        try
        {
            _logger.LogInformation("Sending bulk SMTP meeting emails to {Count} recipients for meeting: {Title}", 
                recipientEmails.Count, meetingTitle);

            var tasks = recipientEmails.Select(email => 
                SendMeetingEmailAsync(email, meetingTitle, startTime, endTime, meetingLink, meetingDescription));

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Bulk SMTP email sending completed: {SuccessCount}/{TotalCount} successful", 
                successCount, recipientEmails.Count);

            return successCount == recipientEmails.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk SMTP meeting emails: {Message}", ex.Message);
            return false;
        }
    }

    private SmtpClient CreateSmtpClient()
    {
        var client = new SmtpClient(_config.Host, _config.Port)
        {
            EnableSsl = _config.Security.Equals("StartTls", StringComparison.OrdinalIgnoreCase) || 
                        _config.Security.Equals("Ssl", StringComparison.OrdinalIgnoreCase),
            Credentials = new NetworkCredential(_config.User, _config.Pass)
        };

        return client;
    }

    private MailMessage CreateEmailMessage(string recipientEmail, string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string meetingDescription)
    {
        _logger.LogInformation("Creating email message with FromName: '{FromName}' and FromEmail: '{FromEmail}'", _config.FromName, _config.FromEmail);
        
        var message = new MailMessage
        {
            From = new MailAddress(_config.FromEmail, _config.FromName),
            Subject = $"Meeting Invitation: {meetingTitle}",
            IsBodyHtml = true,
            Body = CreateEmailBody(meetingTitle, startTime, endTime, meetingLink, meetingDescription)
        };
        
        // Try to ensure the display name is set correctly
        message.From = new MailAddress(_config.FromEmail, _config.FromName, System.Text.Encoding.UTF8);

        message.To.Add(recipientEmail);
        
        // Try to add a custom header to ensure the display name is preserved
        message.Headers.Add("X-Sender-Name", _config.FromName);
        
        // Add calendar invitation as attachment for other calendar apps
        var icsContent = CreateIcsContent(meetingTitle, startTime, endTime, meetingLink, meetingDescription);
        var icsAttachment = new Attachment(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(icsContent)), 
            "meeting-invitation.ics", "text/calendar");
        message.Attachments.Add(icsAttachment);
        
        _logger.LogInformation("Email message created - From: '{FromDisplayName}' <{FromAddress}>, To: {To}, Subject: {Subject}, Attachments: {AttachmentCount}", 
            message.From.DisplayName, message.From.Address, recipientEmail, message.Subject, message.Attachments.Count);

        return message;
    }

    private string BuildGoogleCalendarLink(string title, DateTime startLocal, DateTime endLocal, string description = "", string location = "", string timeZoneId = "Asia/Jerusalem", string[] guests = null)
    {
        // Convert to UTC and format as YYYYMMDDTHHMMSSZ
        string ToGCalUtc(DateTime dtLocal) =>
            dtLocal.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");

        string dates = $"{ToGCalUtc(startLocal)}/{ToGCalUtc(endLocal)}";

        string baseUrl = "https://calendar.google.com/calendar/render?action=TEMPLATE";
        string url = baseUrl
            + "&text=" + System.Net.WebUtility.UrlEncode(title ?? "")
            + "&dates=" + System.Net.WebUtility.UrlEncode(dates)
            + "&details=" + System.Net.WebUtility.UrlEncode(description ?? "")
            + "&location=" + System.Net.WebUtility.UrlEncode(location ?? "")
            + "&ctz=" + System.Net.WebUtility.UrlEncode(timeZoneId ?? "Asia/Jerusalem");

        if (guests != null && guests.Length > 0)
            url += "&add=" + System.Net.WebUtility.UrlEncode(string.Join(",", guests));

        _logger.LogInformation("Generated Google Calendar link for meeting: {Title}", title);
        return url;
    }

    private string CreateIcsContent(string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string meetingDescription)
    {
        var now = DateTime.UtcNow;
        var uid = Guid.NewGuid().ToString();
        
        // Format dates in UTC for iCalendar
        var startUtc = startTime.ToUniversalTime();
        var endUtc = endTime.ToUniversalTime();
        var nowUtc = now.ToUniversalTime();
        
        var startIso = startUtc.ToString("yyyyMMddTHHmmssZ");
        var endIso = endUtc.ToString("yyyyMMddTHHmmssZ");
        var nowIso = nowUtc.ToString("yyyyMMddTHHmmssZ");
        
        var icsContent = $@"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//StrAppers//Meeting Invitation//EN
METHOD:REQUEST
BEGIN:VEVENT
UID:{uid}
DTSTAMP:{nowIso}
DTSTART:{startIso}
DTEND:{endIso}
SUMMARY:{meetingTitle}
DESCRIPTION:{meetingDescription}\n\nMeeting Link: {meetingLink}
LOCATION:{meetingLink}
URL:{meetingLink}
ORGANIZER:MAILTO:{_config.FromEmail}
STATUS:CONFIRMED
SEQUENCE:0
BEGIN:VALARM
TRIGGER:-PT15M
ACTION:DISPLAY
DESCRIPTION:Reminder
END:VALARM
END:VEVENT
END:VCALENDAR";

        _logger.LogInformation("Created iCalendar content for meeting: {Title} at {StartTime}", meetingTitle, startTime);
        return icsContent;
    }

    private string CreateEmailBody(string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string meetingDescription)
    {
        var startTimeFormatted = startTime.ToString("MMMM dd, yyyy 'at' h:mm tt");
        var endTimeFormatted = endTime.ToString("MMMM dd, yyyy 'at' h:mm tt");
        var duration = (endTime - startTime).TotalMinutes;
        
        // Generate Google Calendar link
        var googleCalendarLink = BuildGoogleCalendarLink(
            title: meetingTitle,
            startLocal: startTime,
            endLocal: endTime,
            description: $"{meetingDescription}\n\nMeeting Link: {meetingLink}",
            location: meetingLink,
            timeZoneId: "Asia/Jerusalem"
        );

        return $@"
<html>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <h2 style='color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px;'>
            📅 Meeting Invitation: {meetingTitle}
        </h2>
        
        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
            <h3 style='color: #2c3e50; margin-top: 0;'>Meeting Details</h3>
            <p><strong>📅 Date & Time:</strong> {startTimeFormatted}</p>
            <p><strong>⏰ Duration:</strong> {duration} minutes</p>
            <p><strong>🔗 Meeting Link:</strong> <a href='{meetingLink}' style='color: #3498db; font-weight: bold; font-size: 16px;'>{meetingLink}</a></p>
        </div>

        <div style='background-color: #e3f2fd; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #2196f3;'>
            <h3 style='color: #1976d2; margin-top: 0;'>🚀 Quick Access</h3>
            <p style='margin: 10px 0;'><strong>Click here to join the meeting:</strong></p>
            <p style='margin: 10px 0;'><a href='{meetingLink}' style='background-color: #5558AF; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>Join Meeting</a></p>
        </div>

        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #28a745;'>
            <h3 style='color: #28a745; margin-top: 0;'>📅 Add to Calendar</h3>
            <p style='margin: 10px 0;'><strong>Choose your preferred calendar app:</strong></p>
            <p style='margin: 10px 0;'>
                <a href='{googleCalendarLink}' target='_blank' style='display:inline-block;padding:10px 16px;border-radius:6px;background:#1a73e8;color:#fff;text-decoration:none;font-weight:600;margin-right:10px;'>Add to Google Calendar</a>
                <span style='color: #666; font-size: 14px;'>or download the .ics file attachment for Outlook, Apple Calendar, etc.</span>
            </p>
        </div>

        {(string.IsNullOrEmpty(meetingDescription) ? "" : $@"
        <div style='margin: 20px 0;'>
            <h3 style='color: #2c3e50;'>Description</h3>
            <p>{meetingDescription}</p>
        </div>
        ")}

        <div style='background-color: #e8f5e8; padding: 15px; border-radius: 5px; margin: 20px 0;'>
            <h3 style='color: #27ae60; margin-top: 0;'>📋 What to Expect</h3>
            <ul>
                <li>Click the meeting link above to join</li>
                <li>Test your camera and microphone before the meeting</li>
                <li>Join a few minutes early to ensure everything works</li>
                <li>Have a stable internet connection</li>
            </ul>
        </div>

        <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee; color: #666; font-size: 14px;'>
            <p>This meeting was automatically scheduled by the StrAppers system.</p>
            <p>If you have any questions, please contact your project administrator.</p>
        </div>
    </div>
</body>
</html>";
    }

    public async Task<bool> SendWelcomeEmailAsync(string recipientEmail, string firstName, string projectName, int projectLengthWeeks)
    {
        try
        {
            if (_config.WelcomeEmailTemplate == null || string.IsNullOrEmpty(_config.WelcomeEmailTemplate.Subject) || string.IsNullOrEmpty(_config.WelcomeEmailTemplate.Body))
            {
                _logger.LogWarning("Welcome email template not configured, skipping welcome email to {Email}", recipientEmail);
                return false;
            }

            _logger.LogInformation("Sending welcome email to {Email} for project {ProjectName}", recipientEmail, projectName);

            // Replace placeholders in subject and body
            var subject = _config.WelcomeEmailTemplate.Subject
                .Replace("{{first_name}}", firstName)
                .Replace("{{PROJECT_NAME}}", projectName)
                .Replace("{{X}}", projectLengthWeeks.ToString());

            var body = _config.WelcomeEmailTemplate.Body
                .Replace("{{first_name}}", firstName)
                .Replace("{{PROJECT_NAME}}", projectName)
                .Replace("{{X}}", projectLengthWeeks.ToString());

            // Convert newlines to HTML breaks for email body
            var htmlBody = body.Replace("\n", "<br>");

            using var client = CreateSmtpClient();
            using var message = new MailMessage
            {
                From = new MailAddress(_config.FromEmail, _config.FromName, System.Text.Encoding.UTF8),
                Subject = subject,
                IsBodyHtml = true,
                Body = $@"
<html>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='white-space: pre-line;'>{htmlBody}</div>
    </div>
</body>
</html>"
            };

            message.To.Add(recipientEmail);
            message.Headers.Add("X-Sender-Name", _config.FromName);

            await client.SendMailAsync(message);

            _logger.LogInformation("Welcome email sent successfully to {Email}", recipientEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending welcome email to {Email}: {Message}", recipientEmail, ex.Message);
            return false;
        }
    }

    public async Task<bool> SendBulkWelcomeEmailsAsync(List<(string Email, string FirstName)> recipients, string projectName, int projectLengthWeeks)
    {
        try
        {
            _logger.LogInformation("Sending bulk welcome emails to {Count} recipients for project {ProjectName}", 
                recipients.Count, projectName);

            var tasks = recipients.Select(r => 
                SendWelcomeEmailAsync(r.Email, r.FirstName, projectName, projectLengthWeeks));

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Bulk welcome email sending completed: {SuccessCount}/{TotalCount} successful", 
                successCount, recipients.Count);

            return successCount == recipients.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk welcome emails: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<bool> SendMeetingEmailWithSenderAsync(string recipientEmail, string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string senderEmail, string senderName, string? customMessage = null, string? organizationName = null)
    {
        try
        {
            _logger.LogInformation("Sending SMTP meeting email to {Email} for meeting: {Title} from sender: {SenderName} <{SenderEmail}>", 
                recipientEmail, meetingTitle, senderName, senderEmail);
            _logger.LogInformation("SMTP Configuration - Host: {Host}, Port: {Port}, From: {FromEmail}, Security: {Security}", 
                _config.Host, _config.Port, _config.FromEmail, _config.Security);

            using var client = CreateSmtpClient();
            using var message = CreateEmailMessageWithSender(recipientEmail, meetingTitle, startTime, endTime, meetingLink, senderEmail, senderName, customMessage, organizationName);

            _logger.LogInformation("SMTP email details - To: {To}, Subject: {Subject}, Meeting Link: {Link}, From: {SenderName} <{SenderEmail}>", 
                recipientEmail, message.Subject, meetingLink, senderName, senderEmail);

            await client.SendMailAsync(message);

            _logger.LogInformation("SMTP meeting email sent successfully to {Email} from {SenderName}", recipientEmail, senderName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMTP meeting email to {Email} from {SenderName}: {Message}", recipientEmail, senderName, ex.Message);
            return false;
        }
    }

    private MailMessage CreateEmailMessageWithSender(string recipientEmail, string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string senderEmail, string senderName, string? customMessage, string? organizationName)
    {
        _logger.LogInformation("Creating email message with Sender: '{SenderName}' <{SenderEmail}>, Organization: {Organization}", senderName, senderEmail, organizationName ?? "N/A");
        
        // Use sender email/name for the "From" field, but reply-to will be set to sender
        // Note: SMTP servers may require the From address to match authenticated user, so we'll use config FromEmail
        // but set Reply-To to sender and use sender name in the display
        var message = new MailMessage
        {
            From = new MailAddress(_config.FromEmail, senderName, System.Text.Encoding.UTF8),
            Subject = $"Meeting Invitation: {meetingTitle}",
            IsBodyHtml = true,
            Body = CreateEmailBodyWithSender(meetingTitle, startTime, endTime, meetingLink, senderName, customMessage, organizationName)
        };
        
        // Set Reply-To to the sender so replies go to them
        message.ReplyToList.Add(new MailAddress(senderEmail, senderName));
        
        // Add custom header with sender info
        message.Headers.Add("X-Sender-Name", senderName);
        message.Headers.Add("X-Sender-Email", senderEmail);
        if (!string.IsNullOrEmpty(organizationName))
        {
            message.Headers.Add("X-Organization", organizationName);
        }

        message.To.Add(recipientEmail);
        
        // Add calendar invitation as attachment
        var icsContent = CreateIcsContentWithSender(meetingTitle, startTime, endTime, meetingLink, senderEmail, senderName, customMessage, organizationName);
        var icsAttachment = new Attachment(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(icsContent)), 
            "meeting-invitation.ics", "text/calendar");
        message.Attachments.Add(icsAttachment);
        
        _logger.LogInformation("Email message created - From: '{FromDisplayName}' <{FromAddress}>, Reply-To: {ReplyTo}, To: {To}, Subject: {Subject}, Attachments: {AttachmentCount}", 
            message.From.DisplayName, message.From.Address, senderEmail, recipientEmail, message.Subject, message.Attachments.Count);

        return message;
    }

    private string CreateEmailBodyWithSender(string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string senderName, string? customMessage, string? organizationName)
    {
        var startTimeFormatted = startTime.ToString("MMMM dd, yyyy 'at' h:mm tt");
        var endTimeFormatted = endTime.ToString("MMMM dd, yyyy 'at' h:mm tt");
        var duration = (endTime - startTime).TotalMinutes;
        
        // Generate Google Calendar link
        var googleCalendarLink = BuildGoogleCalendarLink(
            title: meetingTitle,
            startLocal: startTime,
            endLocal: endTime,
            description: customMessage ?? "",
            location: meetingLink,
            timeZoneId: "Asia/Jerusalem"
        );

        var encodedSenderName = System.Net.WebUtility.HtmlEncode(senderName);
        var encodedOrganizationName = string.IsNullOrEmpty(organizationName) ? "" : System.Net.WebUtility.HtmlEncode(organizationName);
        
        var organizationSection = string.IsNullOrEmpty(organizationName) ? "" : $@"
        <div style='background-color: #fff3cd; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #ffc107;'>
            <h3 style='color: #856404; margin-top: 0;'>🏢 Organization</h3>
            <p style='margin: 0; font-size: 16px; font-weight: bold;'>{encodedOrganizationName}</p>
        </div>";

        var customMessageSection = string.IsNullOrEmpty(customMessage) ? "" : $@"
        <div style='background-color: #e7f3ff; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #2196f3;'>
            <h3 style='color: #1976d2; margin-top: 0;'>💬 Message from {encodedSenderName}</h3>
            <p style='margin: 0; white-space: pre-line;'>{System.Net.WebUtility.HtmlEncode(customMessage)}</p>
        </div>";

        return $@"
<html>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <h2 style='color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px;'>
            📅 Meeting Invitation: {meetingTitle}
        </h2>
        
        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
            <p style='margin: 0 0 10px 0;'><strong>From:</strong> {encodedSenderName}</p>
            {organizationSection}
        </div>

        {customMessageSection}
        
        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
            <h3 style='color: #2c3e50; margin-top: 0;'>Meeting Details</h3>
            <p><strong>📅 Date & Time:</strong> {startTimeFormatted}</p>
            <p><strong>⏰ Duration:</strong> {duration} minutes</p>
            <p><strong>🔗 Meeting Link:</strong> <a href='{meetingLink}' style='color: #3498db; font-weight: bold; font-size: 16px;'>{meetingLink}</a></p>
        </div>

        <div style='background-color: #e3f2fd; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #2196f3;'>
            <h3 style='color: #1976d2; margin-top: 0;'>🚀 Quick Access</h3>
            <p style='margin: 10px 0;'><strong>Click here to join the meeting:</strong></p>
            <p style='margin: 10px 0;'><a href='{meetingLink}' style='background-color: #5558AF; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>Join Meeting</a></p>
        </div>

        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #28a745;'>
            <h3 style='color: #28a745; margin-top: 0;'>📅 Add to Calendar</h3>
            <p style='margin: 10px 0;'><strong>Choose your preferred calendar app:</strong></p>
            <p style='margin: 10px 0;'>
                <a href='{googleCalendarLink}' target='_blank' style='display:inline-block;padding:10px 16px;border-radius:6px;background:#1a73e8;color:#fff;text-decoration:none;font-weight:600;margin-right:10px;'>Add to Google Calendar</a>
                <span style='color: #666; font-size: 14px;'>or download the .ics file attachment for Outlook, Apple Calendar, etc.</span>
            </p>
        </div>

        <div style='background-color: #e8f5e8; padding: 15px; border-radius: 5px; margin: 20px 0;'>
            <h3 style='color: #27ae60; margin-top: 0;'>📋 What to Expect</h3>
            <ul>
                <li>Click the meeting link above to join</li>
                <li>Test your camera and microphone before the meeting</li>
                <li>Join a few minutes early to ensure everything works</li>
                <li>Have a stable internet connection</li>
            </ul>
        </div>

        <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee; color: #666; font-size: 14px;'>
            <p>This meeting invitation was sent by {encodedSenderName}.</p>
            <p>If you have any questions, please reply to this email.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string CreateIcsContentWithSender(string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string senderEmail, string senderName, string? customMessage, string? organizationName)
    {
        var now = DateTime.UtcNow;
        var uid = Guid.NewGuid().ToString();
        
        // Format dates in UTC for iCalendar
        var startUtc = startTime.ToUniversalTime();
        var endUtc = endTime.ToUniversalTime();
        var nowUtc = now.ToUniversalTime();
        
        var startIso = startUtc.ToString("yyyyMMddTHHmmssZ");
        var endIso = endUtc.ToString("yyyyMMddTHHmmssZ");
        var nowIso = nowUtc.ToString("yyyyMMddTHHmmssZ");
        
        var description = customMessage ?? "";
        if (!string.IsNullOrEmpty(organizationName))
        {
            description = string.IsNullOrEmpty(description) 
                ? $"Organization: {organizationName}\n\nMeeting Link: {meetingLink}"
                : $"{description}\n\nOrganization: {organizationName}\n\nMeeting Link: {meetingLink}";
        }
        else
        {
            description = string.IsNullOrEmpty(description) 
                ? $"Meeting Link: {meetingLink}"
                : $"{description}\n\nMeeting Link: {meetingLink}";
        }
        
        var icsContent = $@"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//StrAppers//Meeting Invitation//EN
METHOD:REQUEST
BEGIN:VEVENT
UID:{uid}
DTSTAMP:{nowIso}
DTSTART:{startIso}
DTEND:{endIso}
SUMMARY:{meetingTitle}
DESCRIPTION:{description.Replace("\n", "\\n")}
LOCATION:{meetingLink}
URL:{meetingLink}
ORGANIZER;CN={senderName}:MAILTO:{senderEmail}
STATUS:CONFIRMED
SEQUENCE:0
BEGIN:VALARM
TRIGGER:-PT15M
ACTION:DISPLAY
DESCRIPTION:Reminder
END:VALARM
END:VEVENT
END:VCALENDAR";

        _logger.LogInformation("Created iCalendar content for meeting: {Title} at {StartTime} from {SenderName}", meetingTitle, startTime, senderName);
        return icsContent;
    }
}

public class SmtpConfig
{
    public bool UseSmtp { get; set; } = false; // Default: use Teams API for emails, set to true to use SMTP
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Security { get; set; } = "StartTls"; // Options: None, StartTls, Ssl
    public string User { get; set; } = string.Empty;
    public string Pass { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "StrAppers Admin";
    public WelcomeEmailTemplateConfig? WelcomeEmailTemplate { get; set; }
}

public class WelcomeEmailTemplateConfig
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
