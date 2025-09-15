using SlackNet;
using SlackNet.WebApi;
using strAppersBackend.Models;
using strAppersBackend.Data;

namespace strAppersBackend.Services
{
    public class SlackService
    {
        private readonly ISlackApiClient _botClient;
        private readonly ISlackApiClient _userClient;
        private readonly ILogger<SlackService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public SlackService(ILogger<SlackService> logger, IConfiguration configuration, ApplicationDbContext context)
        {
            _logger = logger;
            _configuration = configuration;
            _context = context;
            
            var botToken = _configuration["Slack:BotToken"];
            var userToken = _configuration["Slack:UserToken"];
            
            // Use appropriate tokens for different operations
            _botClient = new SlackApiClient(botToken);
            _userClient = new SlackApiClient(userToken); // Use actual user token for user operations
        }

        /// <summary>
        /// Creates a Slack private channel for a project team
        /// </summary>
        public async Task<SlackTeamCreationResult> CreateProjectTeamAsync(Project project, List<Student> students)
        {
            try
            {
                // Generate team name: ProjectId_ProjectTitleNoSpaces_Team
                var teamName = GenerateTeamName(project);
                
                _logger.LogInformation("Creating Slack team: {TeamName} for project: {ProjectTitle}", 
                    teamName, project.Title);

                // Create private channel (requires user token)
                // Note: This will automatically add the creator, but we'll remove them later
                var channelResponse = await _userClient.Conversations.Create(teamName, isPrivate: true);

                if (channelResponse == null)
                {
                    _logger.LogError("Failed to create Slack channel: {TeamName}", teamName);
                    return new SlackTeamCreationResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to create channel"
                    };
                }

                var channelId = channelResponse.Id;
                _logger.LogInformation("Successfully created Slack channel: {ChannelId} with name: {ChannelName}", 
                    channelId, channelResponse.Name);

                // First, invite the bot to the channel so it can send messages
                await InviteBotToChannelAsync(channelId);

                // Remove the creator from the channel to keep them invisible
                await RemoveCreatorFromChannelAsync(channelId);

                // Add students to the channel
                var memberResults = new List<SlackMemberResult>();
                
                foreach (var student in students)
                {
                    var memberResult = await AddStudentToChannelAsync(channelId, channelResponse.Name, student, project);
                    memberResults.Add(memberResult);
                }

                return new SlackTeamCreationResult
                {
                    Success = true,
                    ChannelId = channelId,
                    ChannelName = channelResponse.Name,
                    TeamName = teamName,
                    MemberResults = memberResults
                };
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

        /// <summary>
        /// Adds a student to a Slack channel
        /// </summary>
        private async Task<SlackMemberResult> AddStudentToChannelAsync(string channelId, string channelName, Student student, Project project)
        {
            try
            {
                // First, try to find the user by email (requires user token)
                var userResponse = await _userClient.Users.LookupByEmail(student.Email);
                
                if (userResponse == null)
                {
                    _logger.LogWarning("Could not find Slack user with email: {Email}. User must be manually invited to workspace first.", student.Email);
                    
                    // Create a join request record
                    await CreateJoinRequestAsync(channelId, channelName, student, project);
                    
                    return new SlackMemberResult
                    {
                        StudentId = student.Id,
                        StudentEmail = student.Email,
                        Success = false,
                        ErrorMessage = "User not found in Slack workspace. Please invite them to the workspace manually first.",
                        Message = "User needs to be invited to workspace manually (Enterprise feature required for automatic invitations)"
                    };
                }

                // Add user to channel (requires user token)
                var inviteResponse = await _userClient.Conversations.Invite(channelId, new[] { userResponse.Id });
                
                if (inviteResponse == null)
                {
                    _logger.LogWarning("Failed to add user {Email} to channel {ChannelId}", 
                        student.Email, channelId);
                    return new SlackMemberResult
                    {
                        StudentId = student.Id,
                        StudentEmail = student.Email,
                        Success = false,
                        ErrorMessage = "Failed to add user to channel"
                    };
                }

                _logger.LogInformation("Successfully added student {Email} to Slack channel {ChannelId}", 
                    student.Email, channelId);

                return new SlackMemberResult
                {
                    StudentId = student.Id,
                    StudentEmail = student.Email,
                    Success = true,
                    SlackUserId = userResponse.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding student {Email} to Slack channel {ChannelId}", 
                    student.Email, channelId);
                return new SlackMemberResult
                {
                    StudentId = student.Id,
                    StudentEmail = student.Email,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Generates team name: ProjectId_ProjectTitleNoSpaces_Team
        /// </summary>
        private string GenerateTeamName(Project project)
        {
            // Remove spaces and special characters from project title
            var cleanTitle = System.Text.RegularExpressions.Regex.Replace(project.Title, @"[^a-zA-Z0-9]", "");
            
            // Generate team name: ProjectId_ProjectTitleNoSpaces_Team
            var teamName = $"{project.Id}_{cleanTitle}_Team";
            
            // Slack channel names must be lowercase and max 80 characters
            teamName = teamName.ToLower();
            if (teamName.Length > 80)
            {
                teamName = teamName.Substring(0, 80);
            }
            
            return teamName;
        }

        /// <summary>
        /// Sends a welcome message to the project team channel
        /// </summary>
        public async Task<bool> SendWelcomeMessageAsync(string channelId, Project project, List<Student> students)
        {
            try
            {
                var adminStudents = students.Where(s => s.IsAdmin).ToList();
                var regularStudents = students.Where(s => !s.IsAdmin).ToList();

                var message = $"🎉 *Welcome to the {project.Title} project team!*\n\n";
                message += $"📋 *Project Description:* {project.Description}\n";
                message += $"🏢 *Organization:* {project.Organization?.Name}\n\n";
                
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
                    foreach (var member in regularStudents)
                    {
                        message += $"• {member.FirstName} {member.LastName} ({member.Email})\n";
                    }
                }
                
                message += "\n🚀 *Let's collaborate and make this project amazing!*";

                var response = await _botClient.Chat.PostMessage(new Message
                {
                    Channel = channelId,
                    Text = message
                });

                return response != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending welcome message to channel {ChannelId}", channelId);
                return false;
            }
        }

        public Task<bool> JoinChannelAsync(string channelId)
        {
            try
            {
                _logger.LogInformation("Joining Slack channel: {ChannelId}", channelId);
                
                // For private channels, we need to use conversations.connect:write scope
                // The bot should automatically be added to channels it creates
                _logger.LogInformation("Bot should already be in the channel it created: {ChannelId}", channelId);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining Slack channel {ChannelId}", channelId);
                return Task.FromResult(false);
            }
        }


        /// <summary>
        /// Creates a join request record in the database
        /// </summary>
        private async Task CreateJoinRequestAsync(string channelId, string channelName, Student student, Project project)
        {
            try
            {
                var joinRequest = new JoinRequest
                {
                    ChannelId = channelId,
                    ChannelName = channelName,
                    StudentId = student.Id,
                    StudentEmail = student.Email,
                    StudentFirstName = student.FirstName,
                    StudentLastName = student.LastName,
                    ProjectId = project.Id,
                    ProjectTitle = project.Title,
                    JoinDate = DateTime.UtcNow,
                    Added = false,
                    Notes = "User not found in Slack workspace - manual invitation required",
                    ErrorMessage = "User not found in Slack workspace. Please invite them to the workspace manually first."
                };

                _context.JoinRequests.Add(joinRequest);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created join request for student {Email} to channel {ChannelName}", 
                    student.Email, channelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating join request for student {Email} to channel {ChannelName}", 
                    student.Email, channelName);
            }
        }

        /// <summary>
        /// Removes the channel creator from the channel to keep them invisible
        /// </summary>
        private async Task RemoveCreatorFromChannelAsync(string channelId)
        {
            try
            {
                // Get user token user ID
                var userInfo = await _userClient.Auth.Test();
                if (userInfo?.UserId != null)
                {
                    // Remove the creator from the channel
                    await _userClient.Conversations.Leave(channelId);
                    _logger.LogInformation("Successfully removed channel creator {UserId} from channel {ChannelId}", 
                        userInfo.UserId, channelId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not remove channel creator from channel {ChannelId}", channelId);
            }
        }

        /// <summary>
        /// Invites the bot to a channel so it can send messages
        /// </summary>
        private async Task InviteBotToChannelAsync(string channelId)
        {
            try
            {
                // Get bot user ID
                var botInfo = await _botClient.Auth.Test();
                if (botInfo?.UserId != null)
                {
                    // Invite bot to channel using user token
                    await _userClient.Conversations.Invite(channelId, new[] { botInfo.UserId });
                    _logger.LogInformation("Successfully invited bot {BotId} to channel {ChannelId}", 
                        botInfo.UserId, channelId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not invite bot to channel {ChannelId}", channelId);
            }
        }


        /// <summary>
        /// Archives a Slack channel (requires user token with channels:write scope)
        /// Note: Slack doesn't have a true "delete" - channels are archived instead
        /// </summary>
        public async Task<bool> DeleteChannelAsync(string channelId)
        {
            try
            {
                _logger.LogInformation("Archiving Slack channel: {ChannelId}", channelId);
                
                // Archive channel (requires user token with channels:write scope)
                // Archive method returns void, so we assume success if no exception is thrown
                await _userClient.Conversations.Archive(channelId);
                
                _logger.LogInformation("Successfully archived Slack channel: {ChannelId}", channelId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving Slack channel {ChannelId}", channelId);
                return false;
            }
        }

        public async Task<object> TestConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Testing Slack API connection");
                
                // Use Auth.Test instead of Team.Info since it works with our current permissions
                var botInfo = await _botClient.Auth.Test();
                
                if (botInfo != null)
                {
                    _logger.LogInformation("Slack API connection successful");
                    return new
                    {
                        connected = true,
                        teamName = botInfo.Team,
                        teamId = botInfo.TeamId,
                        userId = botInfo.UserId,
                        url = botInfo.Url
                    };
                }
                else
                {
                    _logger.LogWarning("Slack API connection failed - no bot info returned");
                    return new
                    {
                        connected = false,
                        error = "No bot info returned"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Slack connection");
                return new
                {
                    connected = false,
                    error = ex.Message,
                    errorType = ex.GetType().Name,
                    stackTrace = ex.StackTrace
                };
            }
        }

        public async Task<object> TestBotInfoAsync()
        {
            try
            {
                _logger.LogInformation("Testing bot info");
                
                // Test bot info
                var botInfo = await _botClient.Auth.Test();
                
                if (botInfo != null)
                {
                    _logger.LogInformation("Bot info retrieved successfully");
                    return new
                    {
                        success = true,
                        userId = botInfo.UserId,
                        teamId = botInfo.TeamId,
                        team = botInfo.Team,
                        url = botInfo.Url
                    };
                }
                else
                {
                    _logger.LogWarning("Bot info test failed - no info returned");
                    return new
                    {
                        success = false,
                        error = "No bot info returned"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing bot info");
                return new
                {
                    success = false,
                    error = ex.Message,
                    errorType = ex.GetType().Name
                };
            }
        }

        public async Task<object> TestUserLookupAsync(string email)
        {
            try
            {
                _logger.LogInformation("Testing user lookup for email: {Email}", email);
                
                // Test user lookup (requires user token)
                var userResponse = await _userClient.Users.LookupByEmail(email);
                
                if (userResponse != null)
                {
                    _logger.LogInformation("User lookup successful for email: {Email}", email);
                    return new
                    {
                        success = true,
                        found = true,
                        userId = userResponse.Id,
                        userName = userResponse.Name,
                        realName = userResponse.RealName,
                        email = email
                    };
                }
                else
                {
                    _logger.LogWarning("User not found for email: {Email}", email);
                    return new
                    {
                        success = true,
                        found = false,
                        email = email,
                        message = "User not found in Slack workspace"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing user lookup for email: {Email}", email);
                return new
                {
                    success = false,
                    found = false,
                    email = email,
                    error = ex.Message,
                    errorType = ex.GetType().Name
                };
            }
        }

        public async Task<object> TestUsersListAsync()
        {
            try
            {
                _logger.LogInformation("Testing users list API");
                
                // Test users list (different API call) - requires user token
                var usersResponse = await _userClient.Users.List();
                
                if (usersResponse != null)
                {
                    _logger.LogInformation("Users list API successful");
                    return new
                    {
                        success = true,
                        userCount = usersResponse.Members?.Count ?? 0,
                        message = "Users list API is working"
                    };
                }
                else
                {
                    _logger.LogWarning("Users list API failed - no response");
                    return new
                    {
                        success = false,
                        error = "No response from users list API"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing users list API");
                return new
                {
                    success = false,
                    error = ex.Message,
                    errorType = ex.GetType().Name
                };
            }
        }

        public async Task<object> TestBotPermissionsAsync()
        {
            try
            {
                _logger.LogInformation("Testing bot permissions");
                
                // Test what the bot can actually do
                var botInfo = await _botClient.Auth.Test();
                
                if (botInfo != null)
                {
                    _logger.LogInformation("Bot permissions test successful");
                    return new
                    {
                        success = true,
                        botId = botInfo.UserId,
                        teamId = botInfo.TeamId,
                        team = botInfo.Team,
                        url = botInfo.Url,
                        message = "Bot is authenticated and working"
                    };
                }
                else
                {
                    _logger.LogWarning("Bot permissions test failed - no info returned");
                    return new
                    {
                        success = false,
                        error = "No bot info returned"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing bot permissions");
                return new
                {
                    success = false,
                    error = ex.Message,
                    errorType = ex.GetType().Name
                };
            }
        }

        public async Task<object> TestChannelVisibilityAsync(string channelId)
        {
            try
            {
                _logger.LogInformation("Testing channel visibility for channel: {ChannelId}", channelId);
                
                // Test if bot can see the channel
                var channelInfo = await _botClient.Conversations.Info(channelId);
                
                if (channelInfo != null)
                {
                    _logger.LogInformation("Channel visibility test successful for channel: {ChannelId}", channelId);
                    return new
                    {
                        success = true,
                        channelId = channelInfo.Id,
                        channelName = channelInfo.Name,
                        isPrivate = channelInfo.IsPrivate,
                        isMember = channelInfo.IsMember,
                        memberCount = channelInfo.NumMembers,
                        created = channelInfo.Created,
                        creator = channelInfo.Creator,
                        message = "Channel is visible to bot"
                    };
                }
                else
                {
                    _logger.LogWarning("Channel visibility test failed - no channel info returned for channel: {ChannelId}", channelId);
                    return new
                    {
                        success = false,
                        error = "Channel not found or not accessible",
                        channelId = channelId
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing channel visibility for channel: {ChannelId}", channelId);
                return new
                {
                    success = false,
                    error = ex.Message,
                    errorType = ex.GetType().Name,
                    channelId = channelId
                };
            }
        }

        public async Task<object> TestChannelMembershipAsync(string channelId)
        {
            try
            {
                _logger.LogInformation("Testing channel membership for channel: {ChannelId}", channelId);
                
                // Test if bot can see channel members
                var membersResponse = await _botClient.Conversations.Members(channelId);
                
                if (membersResponse != null)
                {
                    _logger.LogInformation("Channel membership test successful for channel: {ChannelId}", channelId);
                    return new
                    {
                        success = true,
                        channelId = channelId,
                        memberCount = membersResponse.Members?.Count ?? 0,
                        members = membersResponse.Members?.Take(10), // Show first 10 members
                        message = "Channel membership accessible"
                    };
                }
                else
                {
                    _logger.LogWarning("Channel membership test failed - no members response for channel: {ChannelId}", channelId);
                    return new
                    {
                        success = false,
                        error = "Cannot access channel members",
                        channelId = channelId
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing channel membership for channel: {ChannelId}", channelId);
                return new
                {
                    success = false,
                    error = ex.Message,
                    errorType = ex.GetType().Name,
                    channelId = channelId
                };
            }
        }

        public async Task<object> TestChannelsListAsync()
        {
            try
            {
                _logger.LogInformation("Testing channels list API");
                
                // Test if bot can list channels
                var channelsResponse = await _botClient.Conversations.List();
                
                if (channelsResponse != null)
                {
                    _logger.LogInformation("Channels list test successful");
                    return new
                    {
                        success = true,
                        channelCount = channelsResponse.Channels?.Count ?? 0,
                        channels = channelsResponse.Channels?.Take(5).Select(c => new
                        {
                            id = c.Id,
                            name = c.Name,
                            isPrivate = c.IsPrivate,
                            isMember = c.IsMember
                        }),
                        message = "Channels list accessible"
                    };
                }
                else
                {
                    _logger.LogWarning("Channels list test failed - no response");
                    return new
                    {
                        success = false,
                        error = "No response from channels list API"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing channels list API");
                return new
                {
                    success = false,
                    error = ex.Message,
                    errorType = ex.GetType().Name
                };
            }
        }

        public async Task<object> TestChannelCreationWithDetailsAsync(string teamName)
        {
            try
            {
                _logger.LogInformation("Testing detailed channel creation for team: {TeamName}", teamName);
                
                // Create channel with detailed logging (requires user token)
                var channelResponse = await _userClient.Conversations.Create(teamName, isPrivate: true);
                
                if (channelResponse != null)
                {
                    _logger.LogInformation("Detailed channel creation successful for team: {TeamName}", teamName);
                    
                    // Immediately test if we can see the channel we just created
                    var channelInfo = await _botClient.Conversations.Info(channelResponse.Id);
                    
                    return new
                    {
                        success = true,
                        channelId = channelResponse.Id,
                        channelName = channelResponse.Name,
                        teamName = teamName,
                        isPrivate = true,
                        creationResponse = new
                        {
                            id = channelResponse.Id,
                            name = channelResponse.Name,
                            created = channelResponse.Created
                        },
                        visibilityTest = channelInfo != null ? new
                        {
                            visible = true,
                            isMember = channelInfo.IsMember,
                            memberCount = channelInfo.NumMembers,
                            error = (string?)null
                        } : new
                        {
                            visible = false,
                            isMember = false,
                            memberCount = 0,
                            error = (string?)"Channel not visible immediately after creation"
                        },
                        message = "Channel created and visibility tested"
                    };
                }
                else
                {
                    _logger.LogWarning("Detailed channel creation failed for team: {TeamName}", teamName);
                    return new
                    {
                        success = false,
                        error = "Failed to create channel",
                        teamName = teamName
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in detailed channel creation for team: {TeamName}", teamName);
                return new
                {
                    success = false,
                    error = ex.Message,
                    errorType = ex.GetType().Name,
                    teamName = teamName
                };
            }
        }
    }
}