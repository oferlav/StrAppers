using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using strAppersBackend.Models;
using System.Text;

namespace strAppersBackend.Services;

public interface IGmailService
{
    Task<bool> SendMeetingEmailAsync(string recipientEmail, string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string meetingDescription = "");
    Task<bool> SendBulkMeetingEmailsAsync(List<string> recipientEmails, string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string meetingDescription = "");
}

public class GmailService : IGmailService
{
    private readonly GoogleWorkspaceConfig _config;
    private readonly ILogger<GmailService> _logger;
    private readonly Google.Apis.Gmail.v1.GmailService _gmailService;

    public GmailService(IOptions<GoogleWorkspaceConfig> config, ILogger<GmailService> logger)
    {
        _config = config.Value;
        _logger = logger;
        _gmailService = InitializeGmailService();
    }

    private Google.Apis.Gmail.v1.GmailService InitializeGmailService()
    {
        try
        {
            var credential = GoogleCredential.FromFile(_config.ServiceAccountKeyPath)
                .CreateScoped(_config.Scopes.ToArray());

            return new Google.Apis.Gmail.v1.GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "StrAppers Backend"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Gmail service: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<bool> SendMeetingEmailAsync(string recipientEmail, string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string meetingDescription = "")
    {
        try
        {
            _logger.LogInformation("Sending meeting email to {Email} for meeting: {Title}", recipientEmail, meetingTitle);

            // Check if meeting link is available
            if (string.IsNullOrEmpty(meetingLink))
            {
                _logger.LogWarning("No meeting link provided for email to {Email}", recipientEmail);
                meetingLink = "Meeting link will be provided separately";
            }

            var emailBody = CreateMeetingEmailBody(meetingTitle, startTime, endTime, meetingLink, meetingDescription);
            var message = CreateEmailMessage(recipientEmail, meetingTitle, emailBody);

            _logger.LogInformation("Attempting to send email to {Email} using Gmail API", recipientEmail);
            var result = await _gmailService.Users.Messages.Send(message, "me").ExecuteAsync();

            _logger.LogInformation("Meeting email sent successfully to {Email}, Message ID: {MessageId}", recipientEmail, result.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending meeting email to {Email}: {Message}", recipientEmail, ex.Message);
            _logger.LogError("Full exception details: {Exception}", ex.ToString());
            return false;
        }
    }

    public async Task<bool> SendBulkMeetingEmailsAsync(List<string> recipientEmails, string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string meetingDescription = "")
    {
        try
        {
            _logger.LogInformation("Sending bulk meeting emails to {Count} recipients for meeting: {Title}", 
                recipientEmails.Count, meetingTitle);

            var tasks = recipientEmails.Select(email => 
                SendMeetingEmailAsync(email, meetingTitle, startTime, endTime, meetingLink, meetingDescription));

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Bulk email sending completed: {SuccessCount}/{TotalCount} successful", 
                successCount, recipientEmails.Count);

            return successCount == recipientEmails.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk meeting emails: {Message}", ex.Message);
            return false;
        }
    }

    private string CreateMeetingEmailBody(string meetingTitle, DateTime startTime, DateTime endTime, string meetingLink, string meetingDescription)
    {
        var startTimeFormatted = startTime.ToString("MMMM dd, yyyy 'at' h:mm tt");
        var endTimeFormatted = endTime.ToString("MMMM dd, yyyy 'at' h:mm tt");
        var duration = (endTime - startTime).TotalMinutes;

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
            <p style='margin: 10px 0;'><a href='{meetingLink}' style='background-color: #2196f3; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>Join Google Meet</a></p>
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

    private Message CreateEmailMessage(string recipientEmail, string subject, string htmlBody)
    {
        var plainTextBody = System.Text.RegularExpressions.Regex.Replace(htmlBody, "<[^>]*>", "");
        
        var emailMessage = $@"
To: {recipientEmail}
Subject: {subject}
Content-Type: text/html; charset=utf-8

{htmlBody}";

        var bytes = Encoding.UTF8.GetBytes(emailMessage);
        var base64String = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");

        return new Message
        {
            Raw = base64String
        };
    }
}
