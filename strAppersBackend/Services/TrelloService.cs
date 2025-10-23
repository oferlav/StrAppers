using Microsoft.Extensions.Options;
using strAppersBackend.Models;
using System.Text;
using System.Text.Json;

namespace strAppersBackend.Services
{
    public interface ITrelloService
    {
        Task<TrelloUserRegistrationResponse> InviteUserToTrelloAsync(string email, string? fullName = null);
        Task<TrelloUserCheckResponse> CheckUserRegistrationAsync(string email);
        Task<TrelloProjectCreationResponse> CreateProjectWithSprintsAsync(TrelloProjectCreationRequest request, string projectTitle);
        Task<object> GetProjectStatsAsync(string trelloBoardId);
        Task<object> ListAllBoardsAsync();
        Task<object> GetBoardMembersWithEmailResolutionAsync(string trelloBoardId);
    }

    public class TrelloService : ITrelloService
    {
        private readonly HttpClient _httpClient;
        private readonly TrelloConfig _trelloConfig;
        private readonly ILogger<TrelloService> _logger;

        /// <summary>
        /// Generates a consistent board ID from project ID and title
        /// Format: ProjectId_ProjectTitle (with underscores, matching existing Trello format)
        /// </summary>
        public static string GenerateBoardId(int projectId, string projectTitle)
        {
            if (string.IsNullOrWhiteSpace(projectTitle))
            {
                throw new ArgumentException("Project title cannot be null or empty", nameof(projectTitle));
            }

            // Remove spaces and special characters, keep only alphanumeric
            var cleanTitle = System.Text.RegularExpressions.Regex.Replace(projectTitle, @"[^a-zA-Z0-9]", "");
            
            // Combine project ID with clean title using underscore (matching existing format)
            // Add timestamp to ensure uniqueness
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmm");
            var boardId = $"{projectId}_{cleanTitle}_{timestamp}";
            
            return boardId;
        }

        public TrelloService(HttpClient httpClient, IOptions<TrelloConfig> trelloConfig, ILogger<TrelloService> logger)
        {
            _httpClient = httpClient;
            _trelloConfig = trelloConfig.Value;
            _logger = logger;
        }

        public async Task<TrelloUserRegistrationResponse> InviteUserToTrelloAsync(string email, string? fullName = null)
        {
            try
            {
                _logger.LogInformation("Inviting user {Email} to Trello", email);

                // Create a temporary board to invite the user
                var tempBoardName = $"Temp_Invite_{Guid.NewGuid().ToString("N")[..8]}";
                var createBoardUrl = $"https://api.trello.com/1/boards?name={Uri.EscapeDataString(tempBoardName)}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                
                var createBoardResponse = await _httpClient.PostAsync(createBoardUrl, null);
                
                if (!createBoardResponse.IsSuccessStatusCode)
                {
                    return new TrelloUserRegistrationResponse
                    {
                        Success = false,
                        Message = "Failed to create temporary board for invitation"
                    };
                }

                var boardJson = await createBoardResponse.Content.ReadAsStringAsync();
                var boardData = JsonSerializer.Deserialize<JsonElement>(boardJson);
                var boardId = boardData.GetProperty("id").GetString();

                // Invite user to the board
                var inviteUrl = $"https://api.trello.com/1/boards/{boardId}/members?email={Uri.EscapeDataString(email)}&type=normal&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                
                var inviteResponse = await _httpClient.PutAsync(inviteUrl, null);
                
                if (inviteResponse.IsSuccessStatusCode)
                {
                    // Delete the temporary board
                    var deleteUrl = $"https://api.trello.com/1/boards/{boardId}?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                    await _httpClient.DeleteAsync(deleteUrl);

                    return new TrelloUserRegistrationResponse
                    {
                        Success = true,
                        Message = "User invitation sent successfully"
                    };
                }
                else
                {
                    // Delete the temporary board
                    var deleteUrl = $"https://api.trello.com/1/boards/{boardId}?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                    await _httpClient.DeleteAsync(deleteUrl);

                    return new TrelloUserRegistrationResponse
                    {
                        Success = false,
                        Message = "Failed to send invitation"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inviting user {Email} to Trello", email);
                return new TrelloUserRegistrationResponse
                {
                    Success = false,
                    Message = $"Error sending invitation: {ex.Message}"
                };
            }
        }

        public async Task<TrelloUserCheckResponse> CheckUserRegistrationAsync(string email)
        {
            try
            {
                _logger.LogInformation("Checking if user {Email} is registered in Trello", email);

                // Search for user by email
                var searchUrl = $"https://api.trello.com/1/search?query={Uri.EscapeDataString(email)}&modelTypes=members&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                
                var response = await _httpClient.GetAsync(searchUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    return new TrelloUserCheckResponse
                    {
                        Success = false,
                        Message = "Failed to check user registration",
                        IsRegistered = false
                    };
                }

                var content = await response.Content.ReadAsStringAsync();
                var searchData = JsonSerializer.Deserialize<JsonElement>(content);
                
                var members = searchData.GetProperty("members").EnumerateArray();
                var user = members.FirstOrDefault(m => 
                {
                    var userEmail = m.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : "";
                    return string.Equals(userEmail, email, StringComparison.OrdinalIgnoreCase);
                });

                if (user.ValueKind != JsonValueKind.Undefined)
                {
                    return new TrelloUserCheckResponse
                    {
                        Success = true,
                        Message = "User is registered in Trello",
                        IsRegistered = true,
                        UserId = user.GetProperty("id").GetString(),
                        Username = user.GetProperty("username").GetString(),
                        FullName = user.TryGetProperty("fullName", out var nameProp) ? nameProp.GetString() : ""
                    };
                }
                else
                {
                    return new TrelloUserCheckResponse
                    {
                        Success = true,
                        Message = "User is not registered in Trello",
                        IsRegistered = false
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user registration for {Email}", email);
                return new TrelloUserCheckResponse
                {
                    Success = false,
                    Message = $"Error checking registration: {ex.Message}",
                    IsRegistered = false
                };
            }
        }

        public async Task<TrelloProjectCreationResponse> CreateProjectWithSprintsAsync(TrelloProjectCreationRequest request, string projectTitle)
        {
            var response = new TrelloProjectCreationResponse();
            var errors = new List<string>();

            try
            {
                // Generate board name from project ID and title
                var boardName = GenerateBoardId(request.ProjectId, projectTitle);
                _logger.LogInformation("Creating Trello project with BoardName {BoardName} for ProjectId {ProjectId}", boardName, request.ProjectId);

                // Get user's organizations to use with Standard plan
                var organizationId = await GetUserOrganizationIdAsync();
                
                // Step 1: Create the main board using the generated board name
                // Add defaultLists=false to avoid creating default lists initially
                // Use organization ID for Standard plan
                // Set prefs_permissionLevel=public to make the board public (not private)
                var createBoardUrl = $"https://api.trello.com/1/boards?name={Uri.EscapeDataString(boardName)}&desc={Uri.EscapeDataString(request.ProjectDescription ?? "")}&defaultLists=false&prefs_permissionLevel=public&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                
                if (!string.IsNullOrEmpty(organizationId))
                {
                    createBoardUrl += $"&idOrganization={organizationId}";
                }
                
                if (request.DueDate.HasValue)
                {
                    createBoardUrl += $"&due={request.DueDate.Value:yyyy-MM-dd}";
                }

                _logger.LogInformation("Creating Trello board with URL: {CreateBoardUrl}", createBoardUrl);
                _logger.LogInformation("Board name: {BoardName}, Description: {Description}", boardName, request.ProjectDescription);

                var createBoardResponse = await _httpClient.PostAsync(createBoardUrl, null);
                
                if (!createBoardResponse.IsSuccessStatusCode)
                {
                    var errorContent = await createBoardResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Trello board creation failed with status {StatusCode}: {ErrorContent}", 
                        createBoardResponse.StatusCode, errorContent);
                    response.Success = false;
                    response.Message = $"Failed to create Trello board: {createBoardResponse.StatusCode} - {errorContent}";
                    return response;
                }

                var boardJson = await createBoardResponse.Content.ReadAsStringAsync();
                var boardData = JsonSerializer.Deserialize<JsonElement>(boardJson);
                var trelloBoardId = boardData.GetProperty("id").GetString();
                var boardUrl = boardData.GetProperty("url").GetString();
                var trelloBoardName = boardData.GetProperty("name").GetString();

                response.BoardId = trelloBoardId;  // Store the actual Trello board ID
                response.BoardName = trelloBoardName;    // Store the board name
                response.BoardUrl = boardUrl;

                // Step 2: Invite team members to the board
                _logger.LogInformation("Starting member invitation process for {MemberCount} members", request.TeamMembers.Count);
                
                foreach (var member in request.TeamMembers)
                {
                    try
                    {
                        _logger.LogInformation("=== TRELLO MEMBER INVITATION DEBUG ===");
                        _logger.LogInformation("Attempting to invite Trello member: {Email} ({Name}) to board: {BoardId}", member.Email, $"{member.FirstName} {member.LastName}", trelloBoardId);
                        _logger.LogInformation("Trello API Key (first 10 chars): {ApiKey}", _trelloConfig.ApiKey.Substring(0, 10));
                        _logger.LogInformation("Trello Token (first 10 chars): {Token}", _trelloConfig.ApiToken.Substring(0, 10));
                        
                        // Check if user exists in Trello first
                        _logger.LogInformation("🔍 Checking if user {Email} exists in Trello...", member.Email);
                        var userCheckUrl = $"https://api.trello.com/1/members/{Uri.EscapeDataString(member.Email)}?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                        var userCheckResponse = await _httpClient.GetAsync(userCheckUrl);
                        var userCheckContent = await userCheckResponse.Content.ReadAsStringAsync();
                        
                        _logger.LogInformation("User check response - Status: {Status}, Content: {Content}", userCheckResponse.StatusCode, userCheckContent);
                        
                        if (userCheckResponse.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("✅ User {Email} exists in Trello", member.Email);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ User {Email} does not exist in Trello (Status: {Status}). This might be why no email is sent.", member.Email, userCheckResponse.StatusCode);
                            _logger.LogInformation("💡 SOLUTION: User needs to create a Trello account with email {Email} first, then they can be added to the board", member.Email);
                        }
                        
                        var inviteUrl = $"https://api.trello.com/1/boards/{trelloBoardId}/members?email={Uri.EscapeDataString(member.Email)}&type=normal&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                        _logger.LogInformation("Trello invitation URL: {Url}", inviteUrl);
                        
                        var inviteResponse = await _httpClient.PutAsync(inviteUrl, null);
                        var responseContent = await inviteResponse.Content.ReadAsStringAsync();
                        
                        _logger.LogInformation("Trello API Response - Status: {Status}, Content: {Content}", inviteResponse.StatusCode, responseContent);
                        
                        if (inviteResponse.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("✅ Successfully invited {Email} to Trello board. Response: {Response}", member.Email, responseContent);
                            response.InvitedUsers.Add(new TrelloInvitedUser
                            {
                                Email = member.Email,
                                Name = $"{member.FirstName} {member.LastName}",
                                Status = "Invited"
                            });
                            
                            // Additional logging for successful invitations
                            _logger.LogInformation("📧 Email invitation should have been sent to {Email}. If not received, check: 1) Spam folder, 2) Email address is correct, 3) User has Trello account, 4) Email notifications enabled", member.Email);
                        }
                        else
                        {
                            _logger.LogError("❌ Failed to invite {Email} to Trello board. Status: {Status}, Error: {Error}", member.Email, inviteResponse.StatusCode, responseContent);
                            
                            // Specific error analysis
                            if (inviteResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                            {
                                _logger.LogError("🔒 FORBIDDEN (403) - Possible causes: 1) API token lacks permissions, 2) Board permissions don't allow member addition, 3) User has blocked invitations");
                            }
                            else if (inviteResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                            {
                                _logger.LogError("🔍 NOT FOUND (404) - Possible causes: 1) Board ID is invalid, 2) API endpoint changed, 3) Board was deleted");
                            }
                            else if (inviteResponse.StatusCode == System.Net.HttpStatusCode.BadRequest)
                            {
                                _logger.LogError("⚠️ BAD REQUEST (400) - Possible causes: 1) Invalid email format, 2) Missing required parameters, 3) Invalid member type");
                            }
                            
                            response.InvitedUsers.Add(new TrelloInvitedUser
                            {
                                Email = member.Email,
                                Name = $"{member.FirstName} {member.LastName}",
                                Status = "Failed"
                            });
                            errors.Add($"Failed to invite {member.Email}: {responseContent}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inviting member {Email}", member.Email);
                        errors.Add($"Error inviting {member.Email}: {ex.Message}");
                    }
                }

                // Step 3: Create role labels
                var roleLabelIds = new Dictionary<string, string>();
                var uniqueRoles = request.TeamMembers.Select(m => m.RoleName).Distinct().ToList();
                foreach (var roleName in uniqueRoles)
                {
                    try
                    {
                        var createLabelUrl = $"https://api.trello.com/1/boards/{trelloBoardId}/labels?name={Uri.EscapeDataString(roleName)}&color=blue&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                        
                        var createLabelResponse = await _httpClient.PostAsync(createLabelUrl, null);
                        
                        if (createLabelResponse.IsSuccessStatusCode)
                        {
                            var labelJson = await createLabelResponse.Content.ReadAsStringAsync();
                            var labelData = JsonSerializer.Deserialize<JsonElement>(labelJson);
                            var labelId = labelData.GetProperty("id").GetString();
                            roleLabelIds[roleName] = labelId;
                            
                            _logger.LogInformation("Created role label '{RoleName}' with ID '{LabelId}'", roleName, labelId);
                        }
                        else
                        {
                            errors.Add($"Failed to create role label: {roleName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating role label {RoleName}", roleName);
                        errors.Add($"Error creating role label {roleName}: {ex.Message}");
                    }
                }

                // Step 4: Create lists (sprints)
                var listIds = new Dictionary<string, string>();
                foreach (var list in request.SprintPlan.Lists)
                {
                    try
                    {
                        var createListUrl = $"https://api.trello.com/1/boards/{trelloBoardId}/lists?name={Uri.EscapeDataString(list.Name)}&pos={list.Position}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                        
                        var createListResponse = await _httpClient.PostAsync(createListUrl, null);
                        
                        if (createListResponse.IsSuccessStatusCode)
                        {
                            var listJson = await createListResponse.Content.ReadAsStringAsync();
                            var listData = JsonSerializer.Deserialize<JsonElement>(listJson);
                            var listId = listData.GetProperty("id").GetString();
                            listIds[list.Name] = listId;
                        }
                        else
                        {
                            errors.Add($"Failed to create list: {list.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating list {ListName}", list.Name);
                        errors.Add($"Error creating list {list.Name}: {ex.Message}");
                    }
                }

                // Step 5: Create cards (tasks)
                foreach (var card in request.SprintPlan.Cards)
                {
                    try
                    {
                        if (!listIds.ContainsKey(card.ListName))
                        {
                            errors.Add($"List not found for card: {card.Name}");
                            continue;
                        }

                        var listId = listIds[card.ListName];
                        var createCardUrl = $"https://api.trello.com/1/cards?name={Uri.EscapeDataString(card.Name)}&desc={Uri.EscapeDataString(card.Description)}&idList={listId}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";

                        if (card.DueDate.HasValue)
                        {
                            createCardUrl += $"&due={card.DueDate.Value:yyyy-MM-dd}";
                        }

                        // Add role label to card
                        if (!string.IsNullOrEmpty(card.RoleName) && roleLabelIds.ContainsKey(card.RoleName))
                        {
                            createCardUrl += $"&idLabels={roleLabelIds[card.RoleName]}";
                        }

                        var createCardResponse = await _httpClient.PostAsync(createCardUrl, null);
                        
                        if (createCardResponse.IsSuccessStatusCode)
                        {
                            var cardJson = await createCardResponse.Content.ReadAsStringAsync();
                            var cardData = JsonSerializer.Deserialize<JsonElement>(cardJson);
                            var cardId = cardData.GetProperty("id").GetString();
                            var cardUrl = cardData.GetProperty("url").GetString();

                            response.CreatedCards.Add(new TrelloCreatedCard
                            {
                                CardId = cardId,
                                Name = card.Name,
                                AssignedToEmail = card.AssignedToEmail,
                                ListName = card.ListName,
                                CardUrl = cardUrl
                            });
                        }
                        else
                        {
                            errors.Add($"Failed to create card: {card.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating card {CardName}", card.Name);
                        errors.Add($"Error creating card {card.Name}: {ex.Message}");
                    }
                }

                response.Success = true;
                response.Message = "Trello project created successfully";
                response.Errors = errors;

                _logger.LogInformation("Successfully created Trello project with BoardName {BoardName} and Trello BoardId {TrelloBoardId} with {CardCount} cards and {LabelCount} role labels", 
                    boardName, response.BoardId, response.CreatedCards.Count, roleLabelIds.Count);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Trello project for ProjectId {ProjectId}", request.ProjectId);
                response.Success = false;
                response.Message = $"Error creating Trello project: {ex.Message}";
                response.Errors = errors;
                return response;
            }
        }

        public async Task<object> GetProjectStatsAsync(string trelloBoardId)
        {
            try
            {
                _logger.LogInformation("Getting Trello stats for Trello BoardId {TrelloBoardId}", trelloBoardId);

                // Get board directly by Trello board ID
                var boardUrl = $"https://api.trello.com/1/boards/{trelloBoardId}?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var boardResponse = await _httpClient.GetAsync(boardUrl);
                
                if (!boardResponse.IsSuccessStatusCode)
                {
                    return new
                    {
                        Success = false,
                        Message = $"Board with ID '{trelloBoardId}' not found or access denied",
                        BoardFound = false
                    };
                }

                var boardContent = await boardResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("Trello board response: {BoardContent}", boardContent);
                
                var board = JsonSerializer.Deserialize<JsonElement>(boardContent);

                // Safely get board properties
                var boardId = board.TryGetProperty("id", out var idProp) ? idProp.GetString() : trelloBoardId;
                var boardUrl2 = board.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : "";
                var boardDesc = board.TryGetProperty("desc", out var descProp) ? descProp.GetString() : "";
                var boardClosed = board.TryGetProperty("closed", out var closedProp) ? closedProp.GetBoolean() : false;
                var boardDateLastActivity = board.TryGetProperty("dateLastActivity", out var dateProp) ? dateProp.GetString() : null;

                // Get board members with enhanced information
                var membersUrl = $"https://api.trello.com/1/boards/{boardId}/members?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var membersResponse = await _httpClient.GetAsync(membersUrl);
                var members = new List<object>();
                var membersLookup = new Dictionary<string, object>();
                
                if (membersResponse.IsSuccessStatusCode)
                {
                    var membersContent = await membersResponse.Content.ReadAsStringAsync();
                    var membersData = JsonSerializer.Deserialize<JsonElement[]>(membersContent);
                    
                    // Process members without async in lambda
                    members = membersData.Select(m => 
                    {
                        var memberId = m.TryGetProperty("id", out var idProp) ? idProp.GetString() : "";
                        var email = m.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : "";
                        var fullName = m.TryGetProperty("fullName", out var nameProp) ? nameProp.GetString() : "";
                        var username = m.TryGetProperty("username", out var userProp) ? userProp.GetString() : "";
                        
                        var member = new
                        {
                            Id = memberId,
                            Username = username,
                            FullName = fullName,
                            Email = email,
                            AvatarUrl = m.TryGetProperty("avatarUrl", out var avatarProp) ? avatarProp.GetString() : "",
                            MemberType = m.TryGetProperty("memberType", out var typeProp) ? typeProp.GetString() : "",
                            EmailResolved = !string.IsNullOrEmpty(email),
                            EmailSource = "board_members",
                            // Additional identification fields for fallback matching
                            DisplayName = !string.IsNullOrEmpty(fullName) ? fullName : username,
                            IdentificationMethod = !string.IsNullOrEmpty(email) ? "email" : 
                                                (!string.IsNullOrEmpty(fullName) ? "fullname" : "username")
                        };
                        
                        // Add to lookup for quick access by ID
                        if (!string.IsNullOrEmpty(memberId))
                        {
                            membersLookup[memberId] = member;
                        }
                        
                        return member;
                    }).Cast<object>().ToList();
                }

                // Get lists (sprints)
                var listsUrl = $"https://api.trello.com/1/boards/{boardId}/lists?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var listsResponse = await _httpClient.GetAsync(listsUrl);
                var lists = new List<object>();
                
                if (listsResponse.IsSuccessStatusCode)
                {
                    var listsContent = await listsResponse.Content.ReadAsStringAsync();
                    var listsData = JsonSerializer.Deserialize<JsonElement[]>(listsContent);
                    
                    lists = listsData.Select(l => new
                    {
                        Id = l.TryGetProperty("id", out var idProp) ? idProp.GetString() : "",
                        Name = l.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "",
                        Closed = l.TryGetProperty("closed", out var closedProp) ? closedProp.GetBoolean() : false,
                        Position = l.TryGetProperty("pos", out var posProp) ? posProp.GetDouble() : 0.0,
                        Subscribed = l.TryGetProperty("subscribed", out var subProp) ? subProp.GetBoolean() : false
                    }).Cast<object>().ToList();
                }

                // Get cards (tasks)
                var cardsUrl = $"https://api.trello.com/1/boards/{boardId}/cards?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var cardsResponse = await _httpClient.GetAsync(cardsUrl);
                var cards = new List<object>();
                var cardStats = new
                {
                    Total = 0,
                    Completed = 0,
                    InProgress = 0,
                    NotStarted = 0,
                    Overdue = 0,
                    Assigned = 0,
                    Unassigned = 0,
                    DueComplete = 0,
                    DueIncomplete = 0
                };

                if (cardsResponse.IsSuccessStatusCode)
                {
                    var cardsContent = await cardsResponse.Content.ReadAsStringAsync();
                    var cardsData = JsonSerializer.Deserialize<JsonElement[]>(cardsContent);
                    
                    var now = DateTime.UtcNow;
                    var completedCount = 0;
                    var inProgressCount = 0;
                    var notStartedCount = 0;
                    var overdueCount = 0;
                    var assignedCount = 0;
                    var unassignedCount = 0;
                    var dueCompleteCount = 0;
                    var dueIncompleteCount = 0;

                    cards = cardsData.Select(c => 
                    {
                        var dueDate = c.TryGetProperty("due", out var dueProp) ? dueProp.GetString() : null;
                        var due = dueDate != null ? DateTime.Parse(dueDate) : (DateTime?)null;
                        var isOverdue = due.HasValue && due.Value < now;
                        var isCompleted = c.TryGetProperty("closed", out var closedProp) ? closedProp.GetBoolean() : false;
                        var isDueComplete = c.TryGetProperty("dueComplete", out var dueCompleteProp) ? dueCompleteProp.GetBoolean() : false;
                        var hasAssignee = c.TryGetProperty("idMembers", out var membersProp) && membersProp.GetArrayLength() > 0;
                        
                        // Get assignee details
                        var assignees = new List<object>();
                        if (hasAssignee)
                        {
                            foreach (var memberIdElement in membersProp.EnumerateArray())
                            {
                                var memberId = memberIdElement.GetString();
                                if (!string.IsNullOrEmpty(memberId) && membersLookup.ContainsKey(memberId))
                                {
                                    assignees.Add(membersLookup[memberId]);
                                }
                            }
                        }

                        if (isCompleted) completedCount++;
                        else if (isOverdue) overdueCount++;
                        else if (hasAssignee) inProgressCount++;
                        else notStartedCount++;

                        if (hasAssignee) assignedCount++;
                        else unassignedCount++;

                        if (isDueComplete) dueCompleteCount++;
                        else dueIncompleteCount++;

                        return new
                        {
                            Id = c.TryGetProperty("id", out var idProp) ? idProp.GetString() : "",
                            Name = c.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "",
                            Description = c.TryGetProperty("desc", out var descProp) ? descProp.GetString() : "",
                            Closed = isCompleted,
                            DueComplete = isDueComplete,
                            DueDate = dueDate,
                            IsOverdue = isOverdue,
                            HasAssignee = hasAssignee,
                            AssigneeCount = hasAssignee ? membersProp.GetArrayLength() : 0,
                            Assignees = assignees,
                            ListId = c.TryGetProperty("idList", out var listProp) ? listProp.GetString() : "",
                            Labels = c.TryGetProperty("labels", out var labelsProp) ? 
                                labelsProp.EnumerateArray().Select(l => new { 
                                    Id = l.TryGetProperty("id", out var labelIdProp) ? labelIdProp.GetString() : "",
                                    Name = l.TryGetProperty("name", out var labelNameProp) ? labelNameProp.GetString() : "",
                                    Color = l.TryGetProperty("color", out var colorProp) ? colorProp.GetString() : ""
                                }).ToArray() : new object[0],
                            DateLastActivity = c.TryGetProperty("dateLastActivity", out var activityProp) ? activityProp.GetString() : null,
                            Url = c.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : ""
                        };
                    }).Cast<object>().ToList();

                    cardStats = new
                    {
                        Total = cardsData.Length,
                        Completed = completedCount,
                        InProgress = inProgressCount,
                        NotStarted = notStartedCount,
                        Overdue = overdueCount,
                        Assigned = assignedCount,
                        Unassigned = unassignedCount,
                        DueComplete = dueCompleteCount,
                        DueIncomplete = dueIncompleteCount
                    };
                }

                var boardStats = new
                {
                    TotalMembers = members.Count,
                    TotalLists = lists.Count,
                    TotalCards = cardStats.Total,
                    ActiveMembers = members.Count(m => ((dynamic)m).MemberType == "normal"),
                    ClosedLists = lists.Count(l => ((dynamic)l).Closed),
                    OpenLists = lists.Count(l => !((dynamic)l).Closed)
                };

                var completionPercentage = cardStats.Total > 0 ? (double)cardStats.Completed / cardStats.Total * 100 : 0;

                return new
                {
                    Success = true,
                    BoardFound = true,
                    Board = new
                    {
                        Id = boardId,
                        Name = board.GetProperty("name").GetString(),
                        Description = boardDesc,
                        Url = boardUrl2,
                        Closed = boardClosed,
                        DateLastActivity = boardDateLastActivity
                    },
                    Members = members,
                    Lists = lists,
                    Cards = cards,
                    Stats = new
                    {
                        Board = boardStats,
                        Cards = cardStats,
                        CompletionPercentage = Math.Round(completionPercentage, 2),
                        Progress = new
                        {
                            Completed = cardStats.Completed,
                            InProgress = cardStats.InProgress,
                            NotStarted = cardStats.NotStarted,
                            Overdue = cardStats.Overdue
                        },
                        Assignment = new
                        {
                            Assigned = cardStats.Assigned,
                            Unassigned = cardStats.Unassigned,
                            AssignmentRate = cardStats.Total > 0 ? Math.Round((double)cardStats.Assigned / cardStats.Total * 100, 2) : 0
                        },
                        Completion = new
                        {
                            Closed = cardStats.Completed,
                            Open = cardStats.Total - cardStats.Completed,
                            DueComplete = cardStats.DueComplete,
                            DueIncomplete = cardStats.DueIncomplete,
                            CompletionRate = cardStats.Total > 0 ? Math.Round((double)cardStats.Completed / cardStats.Total * 100, 2) : 0,
                            DueCompletionRate = (cardStats.DueComplete + cardStats.DueIncomplete) > 0 ? 
                                Math.Round((double)cardStats.DueComplete / (cardStats.DueComplete + cardStats.DueIncomplete) * 100, 2) : 0
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Trello stats for Trello BoardId {TrelloBoardId}", trelloBoardId);
                return new
                {
                    Success = false,
                    Message = $"Error getting Trello stats: {ex.Message}",
                    BoardFound = false
                };
            }
        }

        public async Task<object> ListAllBoardsAsync()
        {
            try
            {
                _logger.LogInformation("Listing all Trello boards");

                // Get all boards for the authenticated user
                var boardsUrl = $"https://api.trello.com/1/members/me/boards?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                
                var response = await _httpClient.GetAsync(boardsUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    return new
                    {
                        Success = false,
                        Message = $"Failed to fetch boards: {response.StatusCode}",
                        Boards = new List<object>()
                    };
                }

                var content = await response.Content.ReadAsStringAsync();
                var boards = JsonSerializer.Deserialize<JsonElement[]>(content);
                
                var boardList = boards.Select(b => new
                {
                    Id = b.GetProperty("id").GetString(),
                    Name = b.GetProperty("name").GetString(),
                    Description = b.TryGetProperty("desc", out var descProp) ? descProp.GetString() : "",
                    Url = b.GetProperty("url").GetString(),
                    ShortUrl = b.GetProperty("shortUrl").GetString(),
                    Closed = b.GetProperty("closed").GetBoolean(),
                    DateLastActivity = b.TryGetProperty("dateLastActivity", out var dateProp) ? dateProp.GetString() : null,
                    MemberType = b.TryGetProperty("memberType", out var memberProp) ? memberProp.GetString() : null,
                    Organization = b.TryGetProperty("idOrganization", out var orgProp) ? orgProp.GetString() : null
                }).ToList();

                return new
                {
                    Success = true,
                    Message = $"Found {boardList.Count} boards",
                    TotalBoards = boardList.Count,
                    Boards = boardList
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing Trello boards");
                return new
                {
                    Success = false,
                    Message = $"Error listing boards: {ex.Message}",
                    Boards = new List<object>()
                };
            }
        }

        public async Task<object> GetBoardMembersWithEmailResolutionAsync(string trelloBoardId)
        {
            try
            {
                _logger.LogInformation("Getting board members with email resolution for board {BoardId}", trelloBoardId);

                // Get board members with enhanced email resolution
                var membersUrl = $"https://api.trello.com/1/boards/{trelloBoardId}/members?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var membersResponse = await _httpClient.GetAsync(membersUrl);
                
                if (!membersResponse.IsSuccessStatusCode)
                {
                    return new
                    {
                        Success = false,
                        Message = $"Failed to fetch board members: {membersResponse.StatusCode}",
                        Members = new List<object>()
                    };
                }

                var membersContent = await membersResponse.Content.ReadAsStringAsync();
                var membersData = JsonSerializer.Deserialize<JsonElement[]>(membersContent);
                
                var resolvedMembers = new List<object>();
                var emailResolutionStats = new
                {
                    TotalMembers = membersData.Length,
                    EmailsResolved = 0,
                    EmailsMissing = 0,
                    ResolutionMethods = new Dictionary<string, int>()
                };

                foreach (var member in membersData)
                {
                    var memberId = member.TryGetProperty("id", out var idProp) ? idProp.GetString() : "";
                    var email = member.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : "";
                    var fullName = member.TryGetProperty("fullName", out var nameProp) ? nameProp.GetString() : "";
                    var username = member.TryGetProperty("username", out var userProp) ? userProp.GetString() : "";
                    
                    string resolvedEmail = email;
                    string resolutionMethod = "board_members";
                    
                    // Try to get additional member details if email is missing
                    if (string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(memberId))
                    {
                        try
                        {
                            var memberDetailUrl = $"https://api.trello.com/1/members/{memberId}?fields=email&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                            var memberDetailResponse = await _httpClient.GetAsync(memberDetailUrl);
                            
                            if (memberDetailResponse.IsSuccessStatusCode)
                            {
                                var memberDetailContent = await memberDetailResponse.Content.ReadAsStringAsync();
                                var memberDetail = JsonSerializer.Deserialize<JsonElement>(memberDetailContent);
                                resolvedEmail = memberDetail.TryGetProperty("email", out var detailEmailProp) ? detailEmailProp.GetString() : "";
                                resolutionMethod = "detail_lookup";
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Could not fetch additional details for member {MemberId}: {Error}", memberId, ex.Message);
                        }
                    }

                    var memberInfo = new
                    {
                        Id = memberId,
                        Username = username,
                        FullName = fullName,
                        Email = resolvedEmail,
                        AvatarUrl = member.TryGetProperty("avatarUrl", out var avatarProp) ? avatarProp.GetString() : "",
                        MemberType = member.TryGetProperty("memberType", out var typeProp) ? typeProp.GetString() : "",
                        EmailResolved = !string.IsNullOrEmpty(resolvedEmail),
                        EmailSource = resolutionMethod,
                        DisplayName = !string.IsNullOrEmpty(fullName) ? fullName : username,
                        IdentificationMethod = !string.IsNullOrEmpty(resolvedEmail) ? "email" : 
                                            (!string.IsNullOrEmpty(fullName) ? "fullname" : "username")
                    };

                    resolvedMembers.Add(memberInfo);
                }

                return new
                {
                    Success = true,
                    BoardId = trelloBoardId,
                    Members = resolvedMembers,
                    Stats = new
                    {
                        TotalMembers = resolvedMembers.Count,
                        EmailsResolved = resolvedMembers.Count(m => ((dynamic)m).EmailResolved),
                        EmailsMissing = resolvedMembers.Count(m => !((dynamic)m).EmailResolved),
                        ResolutionMethods = resolvedMembers.GroupBy(m => ((dynamic)m).EmailSource)
                            .ToDictionary(g => g.Key, g => g.Count())
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting board members with email resolution for board {BoardId}", trelloBoardId);
                return new
                {
                    Success = false,
                    Message = $"Error getting board members: {ex.Message}",
                    Members = new List<object>()
                };
            }
        }

        private async Task<string?> GetUserOrganizationIdAsync()
        {
            try
            {
                // Get user's organizations
                var organizationsUrl = $"https://api.trello.com/1/members/me/organizations?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var response = await _httpClient.GetAsync(organizationsUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var organizations = JsonSerializer.Deserialize<JsonElement[]>(content);
                    
                    if (organizations != null && organizations.Length > 0)
                    {
                        // Return the first organization ID
                        var firstOrg = organizations[0];
                        if (firstOrg.TryGetProperty("id", out var idProp))
                        {
                            var orgId = idProp.GetString();
                            _logger.LogInformation("Using organization ID: {OrganizationId}", orgId);
                            return orgId;
                        }
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to get organizations: {StatusCode} - {Error}", response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user organizations");
            }
            
            _logger.LogInformation("No organization found, creating board in personal account");
            return null;
        }
    }
}
