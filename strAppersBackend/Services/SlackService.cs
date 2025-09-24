// SLACK INTEGRATION TEMPORARILY DISABLED
/*
using Microsoft.Extensions.Options;
using SlackNet;
using SlackNet.WebApi;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

public class SlackService
{
    private readonly ISlackApiClient _slackClient;
    private readonly ILogger<SlackService> _logger;
    private readonly SlackSettings _settings;

    public SlackService(IOptions<SlackSettings> settings, ILogger<SlackService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _slackClient = new SlackApiClient(_settings.BotToken);
    }

    public async Task<SlackTeamCreationResult> CreateProjectTeamAsync(Project project, List<Student> students)
    {
        try
        {
            var teamName = $"{project.Id}_{RemoveSpaces(project.Title)}_team";
            
            _logger.LogInformation("Creating Slack team: {TeamName} for project: {ProjectTitle}", teamName, project.Title);

            // Create the private channel
            var channelResponse = await _slackClient.Conversations.Create(teamName, isPrivate: true);
            
            if (channelResponse == null)
            {
                return new SlackTeamCreationResult
                {
                    Success = false,
                    ErrorMessage = "Failed to create Slack channel"
                };
            }

            var channelId = channelResponse.Id;
            var result = new SlackTeamCreationResult
            {
                Success = true,
                ChannelId = channelId,
                ChannelName = teamName,
                TeamName = teamName
            };

            // Invite students to the channel
            foreach (var student in students)
            {
                try
                {
                    var userResponse = await _slackClient.Users.LookupByEmail(student.Email);
                    
                    if (userResponse != null)
                    {
                        await _slackClient.Conversations.Invite(channelId, userResponse.Id);
                        
                        result.MemberResults.Add(new SlackMemberResult
                        {
                            StudentId = student.Id,
                            StudentEmail = student.Email,
                            Success = true,
                            SlackUserId = userResponse.Id
                        });
                    }
                    else
                    {
                        result.MemberResults.Add(new SlackMemberResult
                        {
                            StudentId = student.Id,
                            StudentEmail = student.Email,
                            Success = false,
                            ErrorMessage = "User not found in Slack workspace"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to invite student {Email} to Slack channel", student.Email);
                    result.MemberResults.Add(new SlackMemberResult
                    {
                        StudentId = student.Id,
                        StudentEmail = student.Email,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Slack team for project {ProjectId}", project.Id);
            return new SlackTeamCreationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> SendWelcomeMessageAsync(string channelId, Project project, List<Student> students)
    {
        try
        {
            var adminStudents = students.Where(s => s.IsAdmin).ToList();
            var regularStudents = students.Where(s => !s.IsAdmin).ToList();

            var message = $"🎉 Welcome to the *{project.Title}* project team!\n\n";
            message += $"📋 *Project Description:* {project.Description}\n\n";
            
            if (adminStudents.Any())
            {
                message += $"👑 *Project Admins:*\n";
                foreach (var admin in adminStudents)
                {
                    message += $"• {admin.FirstName} {admin.LastName} ({admin.Email})\n";
                }
                message += "\n";
            }

            if (regularStudents.Any())
            {
                message += $"👥 *Team Members:*\n";
                foreach (var student in regularStudents)
                {
                    message += $"• {student.FirstName} {student.LastName} ({student.Email})\n";
                }
            }

            message += "\n🚀 Let's build something amazing together!";

            await _slackClient.Chat.PostMessage(channelId, message);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending welcome message to channel {ChannelId}", channelId);
            return false;
        }
    }

    public async Task<SlackConnectionTestResult> TestConnectionAsync()
    {
        try
        {
            var authTest = await _slackClient.Auth.Test();
            
            if (authTest != null)
            {
                return new SlackConnectionTestResult
                {
                    Success = true,
                    Message = "Slack connection successful",
                    BotUserId = authTest.UserId,
                    TeamId = authTest.TeamId,
                    TeamName = authTest.Team
                };
            }
            else
            {
                return new SlackConnectionTestResult
                {
                    Success = false,
                    Message = "Failed to authenticate with Slack"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Slack connection");
            return new SlackConnectionTestResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    private static string RemoveSpaces(string input)
    {
        return input.Replace(" ", "").Replace("-", "").Replace("_", "");
    }
}

public class SlackSettings
{
    public string BotToken { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public string DefaultChannel { get; set; } = string.Empty;
}
*/
