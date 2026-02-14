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
        Task<object> GetCardsAndListsByLabelAsync(string trelloBoardId, string labelName);
        Task<JsonElement?> GetCardByCardIdAsync(string trelloBoardId, string cardId);
        /// <summary>
        /// Toggles the state of the checklist item at checkIndex on the card (complete &lt;-&gt; incomplete).
        /// </summary>
        /// <param name="boardId">Trello board ID.</param>
        /// <param name="cardId">CardId custom field value (e.g. "1-B", "2-F").</param>
        /// <param name="checkIndex">0-based index of the check item (flattened across all checklists).</param>
        /// <returns>Success, error message if any, new state ("complete" or "incomplete"), and whether the card was auto-marked complete (all items complete).</returns>
        Task<(bool Success, string? Error, string? NewState, bool CardClosed)> ToggleCheckItemByIndexAsync(string boardId, string cardId, int checkIndex);
        /// <summary>Gets a sprint list and its cards (with checklists) from a board. Tries list name "Sprint{N}" and "Sprint {N}".</summary>
        Task<SprintSnapshot?> GetSprintFromBoardAsync(string boardId, string sprintListName);
        /// <summary>Overrides a list on a board: archives existing cards, then creates cards from the given snapshot (with checklists and CardId custom field).</summary>
        Task<(bool Success, string? Error)> OverrideSprintOnBoardAsync(string boardId, string listId, IReadOnlyList<SprintSnapshotCard> cards);
        /// <summary>If the next sprint list does not exist on the board, creates it and adds empty cards from the project template (TrelloBoardJson). Returns the new list id when created, null otherwise. When dueDateForNewCards is set, cards are created with that due date instead of template dates.</summary>
        Task<string?> EnsureNextEmptySprintOnBoardAsync(string boardId, TrelloProjectCreationRequest request, int nextSprintNumber, DateTime? dueDateForNewCards = null);
        /// <summary>Invite a member to an existing Trello board by email (e.g. to add PM to a board created before allowBillableGuest fix).</summary>
        Task<(bool Success, string? Error)> InviteMemberToBoardByEmailAsync(string boardId, string email);
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

                // Invite user to the board (allowBillableGuest=true required in some workspaces to avoid 403)
                var inviteUrl = $"https://api.trello.com/1/boards/{boardId}/members?email={Uri.EscapeDataString(email)}&type=normal&allowBillableGuest=true&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                
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

        /// <inheritdoc />
        public async Task<(bool Success, string? Error)> InviteMemberToBoardByEmailAsync(string boardId, string email)
        {
            if (string.IsNullOrWhiteSpace(boardId) || string.IsNullOrWhiteSpace(email))
                return (false, "BoardId and email are required.");
            try
            {
                var inviteUrl = $"https://api.trello.com/1/boards/{Uri.EscapeDataString(boardId)}/members?email={Uri.EscapeDataString(email)}&type=normal&allowBillableGuest=true&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var response = await _httpClient.PutAsync(inviteUrl, null);
                var responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Invited {Email} to board {BoardId}", email, boardId);
                    return (true, null);
                }
                var error = GetInviteErrorMessage(responseContent);
                _logger.LogWarning("Failed to invite {Email} to board {BoardId}: {Error}", email, boardId, error);
                return (false, error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inviting {Email} to board {BoardId}", email, boardId);
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Normalizes project description for Trello board desc. If the value is JSON with a "content" array of objects
        /// (e.g. {"content":[{"type":"paragraph","text":"..."}]}), extracts and concatenates the "text" values so
        /// "About this board" shows readable text instead of raw JSON.
        /// </summary>
        private static string NormalizeBoardDescriptionForTrello(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return "";
            var s = description.Trim();
            if (s.Length < 10 || (s[0] != '{' && s[0] != '['))
                return s;
            try
            {
                using var doc = JsonDocument.Parse(s);
                var root = doc.RootElement;
                if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                {
                    var parts = new List<string>();
                    foreach (var item in content.EnumerateArray())
                    {
                        if (item.TryGetProperty("text", out var textProp))
                        {
                            var t = textProp.GetString();
                            if (!string.IsNullOrWhiteSpace(t))
                                parts.Add(t.Trim());
                        }
                    }
                    if (parts.Count > 0)
                        return string.Join("\n\n", parts);
                }
            }
            catch (JsonException) { /* not JSON or wrong shape, use as-is */ }
            return s;
        }

        /// <summary>
        /// Removes leading checkbox-style "[]", "[ ]", "[x]" line prefix from checklist item text before sending to Trello (DB may store these prefixes).
        /// </summary>
        private static string StripChecklistLinePrefix(string? item)
        {
            if (string.IsNullOrEmpty(item)) return item ?? string.Empty;
            var s = item.TrimStart();
            if (s.StartsWith("[", StringComparison.Ordinal))
            {
                int close = s.IndexOf(']');
                if (close >= 0)
                {
                    s = s.Substring(close + 1).TrimStart();
                }
            }
            return s.Length > 0 ? s : item;
        }

        /// <summary>
        /// Returns a user-friendly message when Trello returns "Must reactivate user first" (403) for a previously deleted Atlassian user.
        /// </summary>
        private static string GetInviteErrorMessage(string apiErrorResponse)
        {
            if (string.IsNullOrWhiteSpace(apiErrorResponse))
                return apiErrorResponse ?? "Unknown error";
            if (apiErrorResponse.Contains("reactivate", StringComparison.OrdinalIgnoreCase))
                return "This email was previously an Atlassian user who was deleted. They must be reactivated in Atlassian admin (admin.atlassian.com) before they can be re-invited to boards.";
            return apiErrorResponse;
        }

        /// <summary>
        /// Creates a Trello board with lists, cards, labels, and optionally invites members
        /// </summary>
        private async Task<(string? BoardId, string? BoardUrl, string? BoardName, Dictionary<string, string> RoleLabelIds, Dictionary<string, string> ListIds, Dictionary<string, string> CustomFieldIds, List<string> Errors)> CreateBoardWithContentAsync(
            TrelloProjectCreationRequest request, 
            string boardName, 
            string? organizationId, 
            bool sendEmails)
        {
            var errors = new List<string>();
            string? trelloBoardId = null;
            string? boardUrl = null;
            string? trelloBoardName = null;
            var roleLabelIds = new Dictionary<string, string>();
            var listIds = new Dictionary<string, string>();
            var customFieldIds = new Dictionary<string, string>();

            try
            {
                // Step 1: Create the board (normalize description so JSON content array is shown as readable text in "About this board")
                var descForTrello = NormalizeBoardDescriptionForTrello(request.ProjectDescription);
                var createBoardUrl = $"https://api.trello.com/1/boards?name={Uri.EscapeDataString(boardName)}&desc={Uri.EscapeDataString(descForTrello)}&defaultLists=false&prefs_permissionLevel=public&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                
                if (!string.IsNullOrEmpty(organizationId))
                {
                    createBoardUrl += $"&idOrganization={organizationId}";
                }
                
                if (request.DueDate.HasValue)
                {
                    createBoardUrl += $"&due={request.DueDate.Value:yyyy-MM-dd}";
                }

                _logger.LogInformation("Creating Trello board with URL: {CreateBoardUrl}", createBoardUrl);
                _logger.LogInformation("Board name: {BoardName}, Description: {Description}, SendEmails: {SendEmails}", boardName, request.ProjectDescription, sendEmails);

                var createBoardResponse = await _httpClient.PostAsync(createBoardUrl, null);
                
                if (!createBoardResponse.IsSuccessStatusCode)
                {
                    var errorContent = await createBoardResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Trello board creation failed with status {StatusCode}: {ErrorContent}", 
                        createBoardResponse.StatusCode, errorContent);
                    errors.Add($"Failed to create Trello board: {createBoardResponse.StatusCode} - {errorContent}");
                    return (null, null, null, roleLabelIds, listIds, customFieldIds, errors);
                }

                var boardJson = await createBoardResponse.Content.ReadAsStringAsync();
                var boardData = JsonSerializer.Deserialize<JsonElement>(boardJson);
                trelloBoardId = boardData.GetProperty("id").GetString();
                boardUrl = boardData.GetProperty("url").GetString();
                trelloBoardName = boardData.GetProperty("name").GetString();

                // Step 2: Invite team members (only if sendEmails is true)
                if (sendEmails)
                {
                    var membersToInvite = request.TeamMembers;
                    if (_trelloConfig.SendInvitationToPMOnly)
                    {
                        membersToInvite = request.TeamMembers
                            .Where(m => !string.IsNullOrWhiteSpace(m.RoleName) && 
                                (m.RoleName.Contains("Product Manager", StringComparison.OrdinalIgnoreCase) || 
                                 m.RoleName.Contains("PM", StringComparison.OrdinalIgnoreCase)))
                            .ToList();
                        
                        _logger.LogInformation("📧 [TRELLO INVITATION] SendInvitationToPMOnly is enabled. Filtered from {TotalCount} to {PMCount} Product Manager(s) only", 
                            request.TeamMembers.Count, membersToInvite.Count);
                    }
                    
                    _logger.LogInformation("Starting member invitation process for {MemberCount} members", membersToInvite.Count);
                    
                    foreach (var member in membersToInvite)
                    {
                        try
                        {
                            // Add as board member (type=normal). allowBillableGuest=true required by Trello API for invite-by-email in some workspaces
                            // (403 "Member not allowed to add a multi-board guest without allowBillableGuest parameter" when false).
                            var inviteUrl = $"https://api.trello.com/1/boards/{trelloBoardId}/members?email={Uri.EscapeDataString(member.Email)}&type=normal&allowBillableGuest=true&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                            var inviteResponse = await _httpClient.PutAsync(inviteUrl, null);
                            var responseContent = await inviteResponse.Content.ReadAsStringAsync();
                            
                            if (!inviteResponse.IsSuccessStatusCode)
                            {
                                var inviteError = GetInviteErrorMessage(responseContent);
                                _logger.LogWarning("Failed to invite {Email} to Trello board: {Error}", member.Email, inviteError);
                                errors.Add($"Failed to invite {member.Email}: {inviteError}");
                            }
                            else
                            {
                                _logger.LogInformation("✅ Successfully invited {Email} to Trello board", member.Email);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error inviting member {Email}", member.Email);
                            errors.Add($"Error inviting {member.Email}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("📧 [TRELLO INVITATION] Skipping member invitations (sendEmails=false)");
                }

                // Step 3: Create role labels
                var teamMemberRoles = request.TeamMembers.Select(m => m.RoleName).Where(r => !string.IsNullOrEmpty(r)).Distinct().ToList();
                var cardRoles = request.SprintPlan.Cards.Select(c => c.RoleName).Where(r => !string.IsNullOrEmpty(r)).Distinct().ToList();
                var uniqueRoles = teamMemberRoles.Union(cardRoles).Distinct().ToList();
                
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

                // Step 5: Ensure custom fields exist on the board
                customFieldIds = await EnsureCustomFieldsExistAsync(trelloBoardId, errors);

                return (trelloBoardId, boardUrl, trelloBoardName, roleLabelIds, listIds, customFieldIds, errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating board with content");
                errors.Add($"Error creating board: {ex.Message}");
                return (trelloBoardId, boardUrl, trelloBoardName, roleLabelIds, listIds, customFieldIds, errors);
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
                
                // Check if we should create PM Empty Board (SystemBoard + EmptyBoard)
                if (_trelloConfig.CreatePMEmptyBoard)
                {
                    _logger.LogInformation("📋 [TRELLO] CreatePMEmptyBoard is enabled. Creating SystemBoard first (no emails), then EmptyBoard (with emails)");
                    
                    // Step 1: Create SystemBoard (full board, NO emails)
                    var systemBoardName = $"{boardName}_System";
                    var (systemBoardId, systemBoardUrl, systemBoardNameResult, systemRoleLabelIds, systemListIds, systemCustomFieldIds, systemErrors) = 
                        await CreateBoardWithContentAsync(request, systemBoardName, organizationId, sendEmails: false);
                    
                    if (string.IsNullOrEmpty(systemBoardId))
                    {
                        response.Success = false;
                        response.Message = "Failed to create SystemBoard";
                        response.Errors = systemErrors;
                        return response;
                    }
                    
                    response.SystemBoardId = systemBoardId;
                    response.SystemBoardUrl = systemBoardUrl;
                    errors.AddRange(systemErrors);
                    
                    // Create all cards on SystemBoard (full content)
                    await CreateCardsOnBoardAsync(systemBoardId, request, systemRoleLabelIds, systemListIds, systemCustomFieldIds, errors);
                    
                    _logger.LogInformation("✅ [TRELLO] SystemBoard created successfully: {SystemBoardId}", systemBoardId);
                    
                    // Step 2: Create EmptyBoard (simplified board, WITH emails)
                    var (emptyBoardId, emptyBoardUrl, emptyBoardName, emptyRoleLabelIds, emptyListIds, emptyCustomFieldIds, emptyErrors) = 
                        await CreateBoardWithContentAsync(request, boardName, organizationId, sendEmails: true);
                    
                    if (string.IsNullOrEmpty(emptyBoardId))
                    {
                        response.Success = false;
                        response.Message = "Failed to create EmptyBoard";
                        response.Errors.AddRange(emptyErrors);
                        return response;
                    }
                    
                    response.BoardId = emptyBoardId;
                    response.BoardUrl = emptyBoardUrl;
                    response.BoardName = emptyBoardName;
                    errors.AddRange(emptyErrors);
                    
                    // Invite members and track invitations
                    var membersToInvite = request.TeamMembers;
                    if (_trelloConfig.SendInvitationToPMOnly)
                    {
                        membersToInvite = request.TeamMembers
                            .Where(m => !string.IsNullOrWhiteSpace(m.RoleName) && 
                                (m.RoleName.Contains("Product Manager", StringComparison.OrdinalIgnoreCase) || 
                                 m.RoleName.Contains("PM", StringComparison.OrdinalIgnoreCase)))
                            .ToList();
                    }
                    
                    foreach (var member in membersToInvite)
                    {
                        try
                        {
                            // Add as board member (type=normal). allowBillableGuest=true required by Trello API for invite-by-email in some workspaces
                            // (403 "Member not allowed to add a multi-board guest without allowBillableGuest parameter" when false).
                            var inviteUrl = $"https://api.trello.com/1/boards/{emptyBoardId}/members?email={Uri.EscapeDataString(member.Email)}&type=normal&allowBillableGuest=true&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                            var inviteResponse = await _httpClient.PutAsync(inviteUrl, null);
                            var inviteResponseContent = inviteResponse.IsSuccessStatusCode ? null : await inviteResponse.Content.ReadAsStringAsync();
                            
                            if (inviteResponse.IsSuccessStatusCode)
                            {
                                response.InvitedUsers.Add(new TrelloInvitedUser
                                {
                                    Email = member.Email,
                                    Name = $"{member.FirstName} {member.LastName}",
                                    Status = "Invited"
                                });
                            }
                            else
                            {
                                var inviteError = GetInviteErrorMessage(inviteResponseContent ?? "");
                                _logger.LogWarning("Failed to invite {Email} to EmptyBoard: {Error}", member.Email, inviteError);
                                errors.Add($"Failed to invite {member.Email}: {inviteError}");
                                response.InvitedUsers.Add(new TrelloInvitedUser
                                {
                                    Email = member.Email,
                                    Name = $"{member.FirstName} {member.LastName}",
                                    Status = "Failed"
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error inviting member {Email}", member.Email);
                            errors.Add($"Error inviting {member.Email}: {ex.Message}");
                        }
                    }
                    
                    // Create simplified cards on EmptyBoard
                    await CreateEmptyBoardCardsAsync(emptyBoardId, request, systemBoardId, emptyRoleLabelIds, emptyListIds, emptyCustomFieldIds, errors);
                    
                    _logger.LogInformation("✅ [TRELLO] EmptyBoard created successfully: {EmptyBoardId}", emptyBoardId);
                    
                    response.Success = true;
                    response.Message = "Trello project created successfully with SystemBoard and EmptyBoard";
                    response.Errors = errors;
                    
                    return response;
                }
                else
                {
                    // Original behavior: Create single board with emails
                    var (trelloBoardId, boardUrl, trelloBoardName, roleLabelIds, listIds, customFieldIds, boardErrors) = 
                        await CreateBoardWithContentAsync(request, boardName, organizationId, sendEmails: true);
                    
                    if (string.IsNullOrEmpty(trelloBoardId))
                    {
                        response.Success = false;
                        response.Message = "Failed to create Trello board";
                        response.Errors = boardErrors;
                        return response;
                    }
                    
                    response.BoardId = trelloBoardId;
                    response.BoardUrl = boardUrl;
                    response.BoardName = trelloBoardName;
                    errors.AddRange(boardErrors);
                    
                    // Invite members and track invitations
                    var membersToInvite = request.TeamMembers;
                    if (_trelloConfig.SendInvitationToPMOnly)
                    {
                        membersToInvite = request.TeamMembers
                            .Where(m => !string.IsNullOrWhiteSpace(m.RoleName) && 
                                (m.RoleName.Contains("Product Manager", StringComparison.OrdinalIgnoreCase) || 
                                 m.RoleName.Contains("PM", StringComparison.OrdinalIgnoreCase)))
                            .ToList();
                    }
                    
                    foreach (var member in membersToInvite)
                    {
                        try
                        {
                            // Add as board member (type=normal). allowBillableGuest=true required by Trello API for invite-by-email in some workspaces
                            // (403 "Member not allowed to add a multi-board guest without allowBillableGuest parameter" when false).
                            var inviteUrl = $"https://api.trello.com/1/boards/{trelloBoardId}/members?email={Uri.EscapeDataString(member.Email)}&type=normal&allowBillableGuest=true&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                            var inviteResponse = await _httpClient.PutAsync(inviteUrl, null);
                            var inviteResponseContent = inviteResponse.IsSuccessStatusCode ? null : await inviteResponse.Content.ReadAsStringAsync();
                            
                            if (inviteResponse.IsSuccessStatusCode)
                            {
                                response.InvitedUsers.Add(new TrelloInvitedUser
                                {
                                    Email = member.Email,
                                    Name = $"{member.FirstName} {member.LastName}",
                                    Status = "Invited"
                                });
                            }
                            else
                            {
                                var inviteError = GetInviteErrorMessage(inviteResponseContent ?? "");
                                _logger.LogWarning("Failed to invite {Email} to Trello board: {Error}", member.Email, inviteError);
                                errors.Add($"Failed to invite {member.Email}: {inviteError}");
                                response.InvitedUsers.Add(new TrelloInvitedUser
                                {
                                    Email = member.Email,
                                    Name = $"{member.FirstName} {member.LastName}",
                                    Status = "Failed"
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error inviting member {Email}", member.Email);
                            errors.Add($"Error inviting {member.Email}: {ex.Message}");
                        }
                    }

                    // Create all cards on the board
                    await CreateCardsOnBoardAsync(trelloBoardId, request, roleLabelIds, listIds, customFieldIds, errors);
                    
                    response.Success = true;
                    response.Message = "Trello project created successfully";
                    response.Errors = errors;
                    
                    _logger.LogInformation("Successfully created Trello project with BoardName {BoardName} and Trello BoardId {TrelloBoardId} with {LabelCount} role labels", 
                        boardName, response.BoardId, roleLabelIds.Count);
                    
                    return response;
                }
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

        /// <summary>
        /// Creates all cards on a board with full content
        /// </summary>
        private async Task CreateCardsOnBoardAsync(string trelloBoardId, TrelloProjectCreationRequest request, 
            Dictionary<string, string> roleLabelIds, Dictionary<string, string> listIds, 
            Dictionary<string, string> customFieldIds, List<string> errors)
        {
            // Create cards (tasks)
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

                            // Create checklist with items if checklist items are provided
                            if (card.ChecklistItems != null && card.ChecklistItems.Count > 0)
                            {
                                try
                                {
                                    _logger.LogInformation("Creating checklist for card {CardName} with {ItemCount} items", card.Name, card.ChecklistItems.Count);
                                    
                                    // Step 1: Create the checklist
                                    var createChecklistUrl = $"https://api.trello.com/1/checklists?name=Checklist&idCard={cardId}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                                    var createChecklistResponse = await _httpClient.PostAsync(createChecklistUrl, null);
                                    
                                    if (createChecklistResponse.IsSuccessStatusCode)
                                    {
                                        var checklistJson = await createChecklistResponse.Content.ReadAsStringAsync();
                                        var checklistData = JsonSerializer.Deserialize<JsonElement>(checklistJson);
                                        var checklistId = checklistData.GetProperty("id").GetString();
                                        
                                        _logger.LogInformation("Checklist created with ID {ChecklistId} for card {CardName}", checklistId, card.Name);
                                        
                                        // Step 2: Add items to the checklist
                                        int position = 1;
                                        foreach (var item in card.ChecklistItems)
                                        {
                                            try
                                            {
                                                var itemName = StripChecklistLinePrefix(item);
                                                var addItemUrl = $"https://api.trello.com/1/checklists/{checklistId}/checkItems?name={Uri.EscapeDataString(itemName)}&pos={position}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                                                var addItemResponse = await _httpClient.PostAsync(addItemUrl, null);
                                                
                                                if (addItemResponse.IsSuccessStatusCode)
                                                {
                                                    _logger.LogInformation("Added checklist item {Item} to card {CardName}", item, card.Name);
                                                }
                                                else
                                                {
                                                    var errorContent = await addItemResponse.Content.ReadAsStringAsync();
                                                    _logger.LogWarning("Failed to add checklist item {Item} to card {CardName}: {Error}", item, card.Name, errorContent);
                                                    errors.Add($"Failed to add checklist item '{item}' to card '{card.Name}'");
                                                }
                                                
                                                position++;
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogError(ex, "Error adding checklist item {Item} to card {CardName}", item, card.Name);
                                                errors.Add($"Error adding checklist item '{item}' to card '{card.Name}': {ex.Message}");
                                            }
                                        }
                                        
                                        _logger.LogInformation("Successfully created checklist with {ItemCount} items for card {CardName}", card.ChecklistItems.Count, card.Name);
                                    }
                                    else
                                    {
                                        var errorContent = await createChecklistResponse.Content.ReadAsStringAsync();
                                        _logger.LogWarning("Failed to create checklist for card {CardName}: {Error}", card.Name, errorContent);
                                        errors.Add($"Failed to create checklist for card '{card.Name}': {errorContent}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error creating checklist for card {CardName}", card.Name);
                                    errors.Add($"Error creating checklist for card '{card.Name}': {ex.Message}");
                                }
                            }
                            else
                            {
                                _logger.LogInformation("No checklist items provided for card {CardName}, skipping checklist creation", card.Name);
                            }

                            // Set custom fields on the card
                            await SetCardCustomFieldsAsync(trelloBoardId, cardId, card, customFieldIds, errors);
                        }
                        else
                        {
                            var errorContent = await createCardResponse.Content.ReadAsStringAsync();
                            _logger.LogError("Failed to create card {CardName}: {Error}", card.Name, errorContent);
                            errors.Add($"Failed to create card: {card.Name} - {errorContent}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating card {CardName}", card.Name);
                        errors.Add($"Error creating card {card.Name}: {ex.Message}");
                    }
                }
        }

        /// <summary>
        /// Creates simplified cards on EmptyBoard based on SprintPlan structure
        /// Sprint1 stays exactly the same, other sprints are simplified
        /// </summary>
        private async Task CreateEmptyBoardCardsAsync(string emptyBoardId, TrelloProjectCreationRequest request, 
            string systemBoardId, Dictionary<string, string> roleLabelIds, Dictionary<string, string> listIds, 
            Dictionary<string, string> customFieldIds, List<string> errors)
        {
            foreach (var card in request.SprintPlan.Cards)
            {
                try
                {
                    if (!listIds.ContainsKey(card.ListName))
                    {
                        errors.Add($"List not found for card: {card.Name}");
                        continue;
                    }
                    
                    var emptyListId = listIds[card.ListName];
                    
                    // Determine if this is Sprint1 (keep exactly the same) or other sprint (simplify)
                    var isSprint1 = card.ListName.Equals("Sprint1", StringComparison.OrdinalIgnoreCase);
                    
                    // Determine description based on list and role
                    string finalDescription;
                    string checklistItem;
                    
                    if (isSprint1)
                    {
                        // Sprint1 stays exactly the same
                        finalDescription = card.Description ?? "";
                        checklistItem = "Create a Checklist..."; // Will be replaced with actual items if needed
                    }
                    else
                    {
                        // Other sprints: simplified
                        // Check if card has Product Manager role
                        bool isPMCard = !string.IsNullOrEmpty(card.RoleName) && 
                            (card.RoleName.Contains("Product Manager", StringComparison.OrdinalIgnoreCase) || 
                             card.RoleName.Contains("PM", StringComparison.OrdinalIgnoreCase));
                        
                        finalDescription = isPMCard ? "Add a User Story here..." : "To be filled...";
                        checklistItem = "Create a Checklist...";
                    }
                    
                    // Create card on EmptyBoard (keep due date from original/system board)
                    var createCardUrl = $"https://api.trello.com/1/cards?name={Uri.EscapeDataString(card.Name)}&desc={Uri.EscapeDataString(finalDescription)}&idList={emptyListId}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
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
                        var emptyCardId = cardData.GetProperty("id").GetString();
                        
                        // Create checklist
                        try
                        {
                            var createChecklistUrl = $"https://api.trello.com/1/checklists?name=Checklist&idCard={emptyCardId}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                            var createChecklistResponse = await _httpClient.PostAsync(createChecklistUrl, null);
                            
                            if (createChecklistResponse.IsSuccessStatusCode)
                            {
                                var checklistJson = await createChecklistResponse.Content.ReadAsStringAsync();
                                var checklistData = JsonSerializer.Deserialize<JsonElement>(checklistJson);
                                var checklistId = checklistData.GetProperty("id").GetString();
                                
                                if (isSprint1 && card.ChecklistItems != null && card.ChecklistItems.Count > 0)
                                {
                                    // Sprint1: Add all checklist items
                                    int position = 1;
                                    foreach (var item in card.ChecklistItems)
                                    {
                                        var itemName = StripChecklistLinePrefix(item);
                                        var addItemUrl = $"https://api.trello.com/1/checklists/{checklistId}/checkItems?name={Uri.EscapeDataString(itemName)}&pos={position}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                                        await _httpClient.PostAsync(addItemUrl, null);
                                        position++;
                                    }
                                }
                                else
                                {
                                    // Other sprints: Add single checklist item
                                    var addItemUrl = $"https://api.trello.com/1/checklists/{checklistId}/checkItems?name={Uri.EscapeDataString(checklistItem)}&pos=1&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                                    await _httpClient.PostAsync(addItemUrl, null);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to create checklist for empty board card {CardName}", card.Name);
                        }
                        
                        // Set custom fields: only CardId is populated, rest are empty
                        if (!string.IsNullOrEmpty(card.CardId) && customFieldIds.ContainsKey("CardId"))
                        {
                            await SetCustomFieldValueAsync(emptyCardId, customFieldIds["CardId"], "text", card.CardId, errors);
                        }
                        
                        // Set other custom fields as empty (Priority, Status, Risk, ModuleId, Dependencies, Branched)
                        // These will remain unset/empty as per requirements
                    }
                    else
                    {
                        var errorContent = await createCardResponse.Content.ReadAsStringAsync();
                        _logger.LogWarning("Failed to create empty board card {CardName}: {Error}", card.Name, errorContent);
                        errors.Add($"Failed to create empty board card: {card.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating empty board card {CardName}", card.Name);
                    errors.Add($"Error creating empty board card {card.Name}: {ex.Message}");
                }
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

        public async Task<object> GetCardsAndListsByLabelAsync(string trelloBoardId, string labelName)
        {
            try
            {
                _logger.LogInformation("Getting cards and lists filtered by label '{LabelName}' for board {BoardId}", labelName, trelloBoardId);

                // Step 1: Get all labels for the board to find the label ID by name
                var labelsUrl = $"https://api.trello.com/1/boards/{trelloBoardId}/labels?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var labelsResponse = await _httpClient.GetAsync(labelsUrl);

                if (!labelsResponse.IsSuccessStatusCode)
                {
                    var errorContent = await labelsResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get labels for board {BoardId}: {Error}", trelloBoardId, errorContent);
                    return new
                    {
                        Success = false,
                        Message = $"Failed to get labels for board: {labelsResponse.StatusCode}",
                        LabelFound = false
                    };
                }

                var labelsContent = await labelsResponse.Content.ReadAsStringAsync();
                var labelsData = JsonSerializer.Deserialize<JsonElement[]>(labelsContent);

                // Find the label by name (case-insensitive)
                var label = labelsData.FirstOrDefault(l =>
                {
                    var name = l.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "";
                    return string.Equals(name, labelName, StringComparison.OrdinalIgnoreCase);
                });

                if (label.ValueKind == JsonValueKind.Undefined)
                {
                    _logger.LogWarning("Label '{LabelName}' not found on board {BoardId}", labelName, trelloBoardId);
                    return new
                    {
                        Success = false,
                        Message = $"Label '{labelName}' not found on board",
                        LabelFound = false,
                        AvailableLabels = labelsData.Select(l => l.TryGetProperty("name", out var n) ? n.GetString() : "").Where(n => !string.IsNullOrEmpty(n)).ToList()
                    };
                }

                var labelId = label.GetProperty("id").GetString();
                var labelColor = label.TryGetProperty("color", out var colorProp) ? colorProp.GetString() : "";
                _logger.LogInformation("Found label '{LabelName}' with ID {LabelId}", labelName, labelId);

                // Step 2: Get all lists for the board
                var listsUrl = $"https://api.trello.com/1/boards/{trelloBoardId}/lists?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
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
                        Position = l.TryGetProperty("pos", out var posProp) ? posProp.GetDouble() : 0.0
                    }).Cast<object>().ToList();
                }

                // Step 2.5: Get custom field definitions for the board to map field IDs to names
                var customFieldNameMap = new Dictionary<string, string>();
                try
                {
                    var customFieldsUrl = $"https://api.trello.com/1/boards/{trelloBoardId}/customFields?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                    var customFieldsResponse = await _httpClient.GetAsync(customFieldsUrl);
                    
                    if (customFieldsResponse.IsSuccessStatusCode)
                    {
                        var customFieldsContent = await customFieldsResponse.Content.ReadAsStringAsync();
                        var customFieldsData = JsonSerializer.Deserialize<JsonElement[]>(customFieldsContent);
                        
                        foreach (var fieldDef in customFieldsData ?? Array.Empty<JsonElement>())
                        {
                            var fieldId = fieldDef.TryGetProperty("id", out var idProp) ? idProp.GetString() : "";
                            var fieldName = fieldDef.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "";
                            
                            if (!string.IsNullOrEmpty(fieldId) && !string.IsNullOrEmpty(fieldName))
                            {
                                customFieldNameMap[fieldId] = fieldName;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to fetch custom field definitions for board {BoardId}: {Error}", trelloBoardId, ex.Message);
                }

                // Step 3: Get all cards for the board and filter by label name (include checklists and custom fields)
                var cardsUrl = $"https://api.trello.com/1/boards/{trelloBoardId}/cards?checklists=all&customFieldItems=true&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var cardsResponse = await _httpClient.GetAsync(cardsUrl);
                var cards = new List<object>();

                if (cardsResponse.IsSuccessStatusCode)
                {
                    var cardsContent = await cardsResponse.Content.ReadAsStringAsync();
                    var cardsData = JsonSerializer.Deserialize<JsonElement[]>(cardsContent);

                    // Filter cards that have the specified label name
                    var filteredCards = cardsData.Where(c =>
                    {
                        if (!c.TryGetProperty("labels", out var labelsProp))
                            return false;

                        return labelsProp.EnumerateArray().Any(l =>
                        {
                            var cardLabelName = l.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "";
                            return string.Equals(cardLabelName, labelName, StringComparison.OrdinalIgnoreCase);
                        });
                    }).ToList();

                    // Process each filtered card and fetch checklists if not included
                    foreach (var cardElement in filteredCards)
                    {
                        var cardId = cardElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : "";
                        var checklists = new List<object>();

                        // Check if checklists are already included in the card data
                        if (cardElement.TryGetProperty("checklists", out var checklistsProp))
                        {
                            // Checklists are included in the response
                            checklists = checklistsProp.EnumerateArray().Select(cl => new
                            {
                                Id = cl.TryGetProperty("id", out var clIdProp) ? clIdProp.GetString() : "",
                                Name = cl.TryGetProperty("name", out var clNameProp) ? clNameProp.GetString() : "",
                                Position = cl.TryGetProperty("pos", out var clPosProp) ? clPosProp.GetDouble() : 0.0,
                                CheckItems = cl.TryGetProperty("checkItems", out var checkItemsProp) ?
                                    checkItemsProp.EnumerateArray().Select(ci => new
                                    {
                                        Id = ci.TryGetProperty("id", out var ciIdProp) ? ciIdProp.GetString() : "",
                                        Name = ci.TryGetProperty("name", out var ciNameProp) ? ciNameProp.GetString() : "",
                                        State = ci.TryGetProperty("state", out var ciStateProp) ? ciStateProp.GetString() : "incomplete",
                                        Position = ci.TryGetProperty("pos", out var ciPosProp) ? ciPosProp.GetDouble() : 0.0
                                    }).ToArray() : new object[0]
                            }).Cast<object>().ToList();
                        }
                        else if (!string.IsNullOrEmpty(cardId))
                        {
                            // Fetch checklists separately if not included
                            try
                            {
                                var checklistsUrl = $"https://api.trello.com/1/cards/{cardId}/checklists?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                                var checklistsResponse = await _httpClient.GetAsync(checklistsUrl);
                                
                                if (checklistsResponse.IsSuccessStatusCode)
                                {
                                    var checklistsContent = await checklistsResponse.Content.ReadAsStringAsync();
                                    var checklistsData = JsonSerializer.Deserialize<JsonElement[]>(checklistsContent);
                                    
                                    checklists = checklistsData.Select(cl => new
                                    {
                                        Id = cl.TryGetProperty("id", out var clIdProp) ? clIdProp.GetString() : "",
                                        Name = cl.TryGetProperty("name", out var clNameProp) ? clNameProp.GetString() : "",
                                        Position = cl.TryGetProperty("pos", out var clPosProp) ? clPosProp.GetDouble() : 0.0,
                                        CheckItems = cl.TryGetProperty("checkItems", out var checkItemsProp) ?
                                            checkItemsProp.EnumerateArray().Select(ci => new
                                            {
                                                Id = ci.TryGetProperty("id", out var ciIdProp) ? ciIdProp.GetString() : "",
                                                Name = ci.TryGetProperty("name", out var ciNameProp) ? ciNameProp.GetString() : "",
                                                State = ci.TryGetProperty("state", out var ciStateProp) ? ciStateProp.GetString() : "incomplete",
                                                Position = ci.TryGetProperty("pos", out var ciPosProp) ? ciPosProp.GetDouble() : 0.0
                                            }).ToArray() : new object[0]
                                    }).Cast<object>().ToList();
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Failed to fetch checklists for card {CardId}: {Error}", cardId, ex.Message);
                            }
                        }

                        // Process custom fields from the card
                        var customFields = new Dictionary<string, object>();
                        if (cardElement.TryGetProperty("customFieldItems", out var customFieldItemsProp))
                        {
                            foreach (var fieldItem in customFieldItemsProp.EnumerateArray())
                            {
                                var idCustomField = fieldItem.TryGetProperty("idCustomField", out var customFieldIdProp) ? customFieldIdProp.GetString() : "";
                                
                                // Get field name from the mapping we created
                                var fieldName = !string.IsNullOrEmpty(idCustomField) && customFieldNameMap.TryGetValue(idCustomField, out var name) 
                                    ? name 
                                    : idCustomField ?? "Unknown";
                                
                                object? fieldValue = null;
                                
                                // Handle different custom field types
                                if (fieldItem.TryGetProperty("value", out var valueProp))
                                {
                                    if (valueProp.ValueKind == JsonValueKind.String)
                                    {
                                        var stringValue = valueProp.GetString() ?? "";
                                        // Handle double-encoded strings (e.g., "\"12345\"")
                                        if (stringValue.StartsWith("\"") && stringValue.EndsWith("\""))
                                        {
                                            try
                                            {
                                                // Try to unescape JSON string
                                                stringValue = JsonSerializer.Deserialize<string>(stringValue) ?? stringValue;
                                            }
                                            catch
                                            {
                                                // If deserialization fails, just remove surrounding quotes
                                                stringValue = stringValue.Trim('"');
                                            }
                                        }
                                        fieldValue = stringValue;
                                    }
                                    else if (valueProp.ValueKind == JsonValueKind.Object)
                                    {
                                        // For dropdown/select fields - check for text property
                                        if (valueProp.TryGetProperty("text", out var textProp))
                                        {
                                            fieldValue = textProp.GetString() ?? "";
                                        }
                                        // For number fields stored as object
                                        else if (valueProp.TryGetProperty("number", out var numberProp))
                                        {
                                            fieldValue = numberProp.GetRawText();
                                        }
                                        // For date fields
                                        else if (valueProp.TryGetProperty("date", out var dateProp))
                                        {
                                            fieldValue = dateProp.GetString() ?? "";
                                        }
                                        // For checkbox fields
                                        else if (valueProp.TryGetProperty("checked", out var checkedProp))
                                        {
                                            fieldValue = checkedProp.GetBoolean();
                                        }
                                    }
                                    else if (valueProp.ValueKind == JsonValueKind.Number)
                                    {
                                        fieldValue = valueProp.GetRawText();
                                    }
                                    else if (valueProp.ValueKind == JsonValueKind.True || valueProp.ValueKind == JsonValueKind.False)
                                    {
                                        fieldValue = valueProp.GetBoolean();
                                    }
                                }
                                
                                if (!string.IsNullOrEmpty(fieldName) && fieldValue != null)
                                {
                                    customFields[fieldName] = fieldValue;
                                }
                            }
                        }

                        cards.Add(new
                        {
                            Id = cardId,
                            Name = cardElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "",
                            Description = cardElement.TryGetProperty("desc", out var descProp) ? descProp.GetString() : "",
                            ListId = cardElement.TryGetProperty("idList", out var listProp) ? listProp.GetString() : "",
                            Closed = cardElement.TryGetProperty("closed", out var closedProp) ? closedProp.GetBoolean() : false,
                            DueDate = cardElement.TryGetProperty("due", out var dueProp) ? dueProp.GetString() : null,
                            DueComplete = cardElement.TryGetProperty("dueComplete", out var dueCompleteProp) ? dueCompleteProp.GetBoolean() : false,
                            Url = cardElement.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : "",
                            Labels = cardElement.TryGetProperty("labels", out var labelsProp) ?
                                labelsProp.EnumerateArray().Select(l => new
                                {
                                    Id = l.TryGetProperty("id", out var labelIdProp) ? labelIdProp.GetString() : "",
                                    Name = l.TryGetProperty("name", out var labelNameProp) ? labelNameProp.GetString() : "",
                                    Color = l.TryGetProperty("color", out var labelColorProp) ? labelColorProp.GetString() : ""
                                }).ToArray() : new object[0],
                            Checklists = checklists,
                            CustomFields = customFields,
                            DateLastActivity = cardElement.TryGetProperty("dateLastActivity", out var activityProp) ? activityProp.GetString() : null
                        });
                    }

                    _logger.LogInformation("Filtered {FilteredCount} cards from {TotalCount} total cards by label '{LabelName}'", cards.Count, cardsData.Length, labelName);
                }
                else
                {
                    var errorContent = await cardsResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get cards for board {BoardId}: {Error}", trelloBoardId, errorContent);
                }

                _logger.LogInformation("Retrieved {CardCount} cards and {ListCount} lists for label '{LabelName}' on board {BoardId}", cards.Count, lists.Count, labelName, trelloBoardId);

                return new
                {
                    Success = true,
                    LabelFound = true,
                    Label = new
                    {
                        Id = labelId,
                        Name = labelName,
                        Color = labelColor
                    },
                    Lists = lists,
                    Cards = cards,
                    CardCount = cards.Count,
                    ListCount = lists.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cards and lists by label '{LabelName}' for board {BoardId}", labelName, trelloBoardId);
                return new
                {
                    Success = false,
                    Message = $"Error getting cards and lists by label: {ex.Message}",
                    LabelFound = false
                };
            }
        }

        /// <summary>
        /// Ensures custom fields exist on the board, creating them if they don't exist
        /// Returns a dictionary mapping field names to field IDs
        /// </summary>
        private async Task<Dictionary<string, string>> EnsureCustomFieldsExistAsync(string boardId, List<string> errors)
        {
            var customFieldIds = new Dictionary<string, string>();
            
            try
            {
                // Get existing custom fields
                var getFieldsUrl = $"https://api.trello.com/1/boards/{boardId}/customFields?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var getFieldsResponse = await _httpClient.GetAsync(getFieldsUrl);
                
                var existingFields = new Dictionary<string, string>();
                if (getFieldsResponse.IsSuccessStatusCode)
                {
                    var fieldsJson = await getFieldsResponse.Content.ReadAsStringAsync();
                    var fieldsArray = JsonSerializer.Deserialize<JsonElement[]>(fieldsJson);
                    
                    foreach (var field in fieldsArray ?? Array.Empty<JsonElement>())
                    {
                        var fieldId = field.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                        var fieldName = field.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                        
                        if (!string.IsNullOrEmpty(fieldId) && !string.IsNullOrEmpty(fieldName))
                        {
                            existingFields[fieldName] = fieldId;
                            customFieldIds[fieldName] = fieldId;
                        }
                    }
                }

                // Define required custom fields
                var requiredFields = new[]
                {
                    new { Name = "Priority", Type = "number", Options = (string[]?)null },
                    new { Name = "Status", Type = "list", Options = new[] { "To Do", "In Progress", "Done" } },
                    new { Name = "Risk", Type = "list", Options = new[] { "Low", "Medium", "High" } },
                    new { Name = "ModuleId", Type = "text", Options = (string[]?)null },
                    new { Name = "CardId", Type = "text", Options = (string[]?)null },
                    new { Name = "Dependencies", Type = "text", Options = (string[]?)null },
                    new { Name = "Branched", Type = "checkbox", Options = (string[]?)null }
                };

                // Create missing custom fields
                foreach (var field in requiredFields)
                {
                    if (existingFields.ContainsKey(field.Name))
                    {
                        _logger.LogInformation("Custom field '{FieldName}' already exists on board", field.Name);
                        continue;
                    }

                    try
                    {
                        // Trello API: POST /1/customFields (not /boards/{id}/customFields)
                        // Note: Custom Fields Power-Up must be enabled on the board
                        var createFieldUrl = $"https://api.trello.com/1/customFields?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                        
                        // Trello API format for creating custom fields
                        // idModel is the board ID, modelType must be "board"
                        var fieldData = new Dictionary<string, object>
                        {
                            { "idModel", boardId },
                            { "modelType", "board" },
                            { "name", field.Name },
                            { "type", field.Type },
                            { "pos", "bottom" }
                        };
                        
                        var fieldJson = JsonSerializer.Serialize(fieldData);
                        var content = new StringContent(fieldJson, Encoding.UTF8, "application/json");
                        
                        _logger.LogInformation("Creating custom field '{FieldName}' of type '{FieldType}' on board {BoardId}", field.Name, field.Type, boardId);
                        var createFieldResponse = await _httpClient.PostAsync(createFieldUrl, content);
                        
                        var responseContent = await createFieldResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation("Custom field creation response for '{FieldName}': Status {StatusCode}, Content: {Content}", field.Name, createFieldResponse.StatusCode, responseContent);
                        
                        if (createFieldResponse.IsSuccessStatusCode)
                        {
                            var responseJson = responseContent;
                            var fieldResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
                            var fieldId = fieldResponse.GetProperty("id").GetString();
                            customFieldIds[field.Name] = fieldId;
                            
                            _logger.LogInformation("Created custom field '{FieldName}' with ID '{FieldId}'", field.Name, fieldId);
                            
                            // For list type fields, add options
                            if (field.Type == "list" && field.Options != null)
                            {
                                foreach (var option in field.Options)
                                {
                                    try
                                    {
                                        var addOptionUrl = $"https://api.trello.com/1/customFields/{fieldId}/options?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                                        var optionData = new { value = new { text = option } };
                                        var optionJson = JsonSerializer.Serialize(optionData);
                                        var optionContent = new StringContent(optionJson, Encoding.UTF8, "application/json");
                                        
                                        var addOptionResponse = await _httpClient.PostAsync(addOptionUrl, optionContent);
                                        if (addOptionResponse.IsSuccessStatusCode)
                                        {
                                            _logger.LogInformation("Added option '{Option}' to custom field '{FieldName}'", option, field.Name);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to add option '{Option}' to custom field '{FieldName}'", option, field.Name);
                                    }
                                }
                            }
                        }
                        else
                        {
                            var errorContent = await createFieldResponse.Content.ReadAsStringAsync();
                            _logger.LogWarning("Failed to create custom field '{FieldName}': {Error}. This may be because the Custom Fields Power-Up is not enabled on the board. Custom fields require the Power-Up to be enabled manually in Trello.", field.Name, errorContent);
                            // Don't add to errors - this is non-blocking. Custom fields are optional if Power-Up isn't enabled.
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating custom field '{FieldName}'", field.Name);
                        errors.Add($"Error creating custom field '{field.Name}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring custom fields exist on board {BoardId}", boardId);
                _logger.LogWarning("Custom fields may not be available if the Custom Fields Power-Up is not enabled on the Trello board. To enable: Open the board in Trello → Power-Ups → Search for 'Custom Fields' → Enable it.");
                // Don't add to errors - custom fields are optional if Power-Up isn't enabled
            }
            
            if (customFieldIds.Count == 0)
            {
                _logger.LogWarning("No custom fields are available on board {BoardId}. This may be because the Custom Fields Power-Up is not enabled. Cards will be created without custom fields.", boardId);
            }
            
            return customFieldIds;
        }

        /// <summary>
        /// Sets custom field values on a Trello card
        /// </summary>
        private async Task SetCardCustomFieldsAsync(string boardId, string cardId, TrelloCard card, Dictionary<string, string> customFieldIds, List<string> errors)
        {
            try
            {
                // Set Priority (number)
                if (customFieldIds.ContainsKey("Priority") && card.Priority > 0)
                {
                    await SetCustomFieldValueAsync(cardId, customFieldIds["Priority"], "number", card.Priority.ToString(), errors);
                }

                // Set Status (list)
                if (customFieldIds.ContainsKey("Status") && !string.IsNullOrEmpty(card.Status))
                {
                    await SetCustomFieldValueAsync(cardId, customFieldIds["Status"], "list", card.Status, errors);
                }

                // Set Risk (list)
                if (customFieldIds.ContainsKey("Risk") && !string.IsNullOrEmpty(card.Risk))
                {
                    await SetCustomFieldValueAsync(cardId, customFieldIds["Risk"], "list", card.Risk, errors);
                }

                // Set ModuleId (text)
                if (customFieldIds.ContainsKey("ModuleId") && !string.IsNullOrEmpty(card.ModuleId))
                {
                    await SetCustomFieldValueAsync(cardId, customFieldIds["ModuleId"], "text", card.ModuleId, errors);
                }

                // Set CardId (text)
                if (customFieldIds.ContainsKey("CardId") && !string.IsNullOrEmpty(card.CardId))
                {
                    await SetCustomFieldValueAsync(cardId, customFieldIds["CardId"], "text", card.CardId, errors);
                }

                // Set Dependencies (text - comma-separated)
                if (customFieldIds.ContainsKey("Dependencies") && card.Dependencies != null && card.Dependencies.Count > 0)
                {
                    var dependenciesText = string.Join(", ", card.Dependencies);
                    await SetCustomFieldValueAsync(cardId, customFieldIds["Dependencies"], "text", dependenciesText, errors);
                }

                // Set Branched (checkbox) - only for developer roles
                if (customFieldIds.ContainsKey("Branched") && card.Branched.HasValue)
                {
                    var isDeveloper = !string.IsNullOrEmpty(card.RoleName) && 
                                     (card.RoleName.Contains("Developer", StringComparison.OrdinalIgnoreCase) ||
                                      card.RoleName.Contains("Backend", StringComparison.OrdinalIgnoreCase) ||
                                      card.RoleName.Contains("Frontend", StringComparison.OrdinalIgnoreCase));
                    
                    if (isDeveloper)
                    {
                        await SetCustomFieldValueAsync(cardId, customFieldIds["Branched"], "checkbox", card.Branched.Value ? "true" : "false", errors);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting custom fields for card {CardId}", cardId);
                errors.Add($"Error setting custom fields for card '{card.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Sets a single custom field value on a card
        /// </summary>
        private async Task SetCustomFieldValueAsync(string cardId, string customFieldId, string fieldType, string value, List<string> errors)
        {
            try
            {
                var setFieldUrl = $"https://api.trello.com/1/cards/{cardId}/customField/{customFieldId}/item?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                
                object fieldValue;
                if (fieldType == "number")
                {
                    if (double.TryParse(value, out var numValue))
                    {
                        fieldValue = new { value = new { number = numValue.ToString() } };
                    }
                    else
                    {
                        _logger.LogWarning("Invalid number value for custom field: {Value}", value);
                        return;
                    }
                }
                else if (fieldType == "list")
                {
                    // For list fields, we need to get the option ID by matching the text value
                    // First, get the custom field definition to find the option ID
                    var getFieldUrl = $"https://api.trello.com/1/customFields/{customFieldId}?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                    var getFieldResponse = await _httpClient.GetAsync(getFieldUrl);
                    
                    string? optionId = null;
                    if (getFieldResponse.IsSuccessStatusCode)
                    {
                        var fieldDefJson = await getFieldResponse.Content.ReadAsStringAsync();
                        var fieldDef = JsonSerializer.Deserialize<JsonElement>(fieldDefJson);
                        
                        if (fieldDef.TryGetProperty("options", out var optionsProp) && optionsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var option in optionsProp.EnumerateArray())
                            {
                                var optionText = option.TryGetProperty("value", out var optVal) && optVal.TryGetProperty("text", out var optText) 
                                    ? optText.GetString() 
                                    : null;
                                
                                if (string.Equals(optionText, value, StringComparison.OrdinalIgnoreCase))
                                {
                                    optionId = option.TryGetProperty("id", out var optId) ? optId.GetString() : null;
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(optionId))
                    {
                        fieldValue = new { idValue = optionId };
                    }
                    else
                    {
                        // Fallback: try using text value directly (some Trello versions might accept this)
                        _logger.LogWarning("Could not find option ID for list value '{Value}', trying text format", value);
                        fieldValue = new { value = new { text = value } };
                    }
                }
                else if (fieldType == "checkbox")
                {
                    var boolValue = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    fieldValue = new { value = new { @checked = boolValue } };
                }
                else // text
                {
                    fieldValue = new { value = new { text = value } };
                }
                
                var fieldJson = JsonSerializer.Serialize(fieldValue);
                var content = new StringContent(fieldJson, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PutAsync(setFieldUrl, content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Set custom field '{FieldId}' to '{Value}' on card {CardId}", customFieldId, value, cardId);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to set custom field '{FieldId}' to '{Value}' on card {CardId}: {Error}", customFieldId, value, cardId, errorContent);
                    errors.Add($"Failed to set custom field on card: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting custom field '{FieldId}' to '{Value}' on card {CardId}", customFieldId, value, cardId);
                errors.Add($"Error setting custom field: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a Trello card by CardId custom field value
        /// </summary>
        public async Task<JsonElement?> GetCardByCardIdAsync(string trelloBoardId, string cardId)
        {
            try
            {
                _logger.LogInformation("🔍 [TRELLO] Searching for card with CardId '{CardId}' in board {BoardId}", cardId, trelloBoardId);

                // Get all cards from the board
                var cardsUrl = $"https://api.trello.com/1/boards/{trelloBoardId}/cards?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}&customFieldItems=true";
                var response = await _httpClient.GetAsync(cardsUrl);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to get cards from board {BoardId}: {Error}", trelloBoardId, errorContent);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var cards = JsonSerializer.Deserialize<JsonElement[]>(content);

                if (cards == null)
                {
                    _logger.LogWarning("No cards found in board {BoardId}", trelloBoardId);
                    return null;
                }

                // Get custom field definitions to find CardId field
                var customFieldsUrl = $"https://api.trello.com/1/boards/{trelloBoardId}/customFields?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var fieldsResponse = await _httpClient.GetAsync(customFieldsUrl);
                
                string? cardIdFieldId = null;
                if (fieldsResponse.IsSuccessStatusCode)
                {
                    var fieldsContent = await fieldsResponse.Content.ReadAsStringAsync();
                    var fields = JsonSerializer.Deserialize<JsonElement[]>(fieldsContent);
                    
                    if (fields != null)
                    {
                        foreach (var field in fields)
                        {
                            if (field.TryGetProperty("name", out var nameProp) && nameProp.GetString() == "CardId")
                            {
                                if (field.TryGetProperty("id", out var idProp))
                                {
                                    cardIdFieldId = idProp.GetString();
                                    break;
                                }
                            }
                        }
                    }
                }

                // Search through cards to find one with matching CardId
                foreach (var card in cards)
                {
                    if (card.TryGetProperty("customFieldItems", out var customFieldsProp))
                    {
                        foreach (var customField in customFieldsProp.EnumerateArray())
                        {
                            if (customField.TryGetProperty("idCustomField", out var fieldIdProp))
                            {
                                var fieldId = fieldIdProp.GetString();
                                if (fieldId == cardIdFieldId)
                                {
                                    if (customField.TryGetProperty("value", out var valueProp))
                                    {
                                        string? cardIdValue = null;
                                        if (valueProp.TryGetProperty("text", out var textProp))
                                        {
                                            cardIdValue = textProp.GetString();
                                        }

                                        if (cardIdValue == cardId)
                                        {
                                            _logger.LogInformation("✅ [TRELLO] Found card with CardId '{CardId}'", cardId);
                                            return card;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                _logger.LogWarning("⚠️ [TRELLO] Card with CardId '{CardId}' not found in board {BoardId}", cardId, trelloBoardId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [TRELLO] Error getting card by CardId '{CardId}': {Message}", cardId, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Toggles the state of the checklist item at checkIndex on the card (complete &lt;-&gt; incomplete).
        /// If the item is set to complete and all checklist items are then complete, the card is marked complete (dueComplete=true) via Trello API.
        /// </summary>
        public async Task<(bool Success, string? Error, string? NewState, bool CardClosed)> ToggleCheckItemByIndexAsync(string boardId, string cardId, int checkIndex)
        {
            try
            {
                if (checkIndex < 0)
                {
                    return (false, "checkIndex must be >= 0", null, false);
                }

                var card = await GetCardByCardIdAsync(boardId, cardId);
                if (card == null)
                {
                    return (false, $"Trello card with CardId '{cardId}' not found in board {boardId}", null, false);
                }

                var cardVal = card.Value;
                if (!cardVal.TryGetProperty("id", out var idProp))
                {
                    return (false, "Card has no id.", null, false);
                }
                var trelloCardId = idProp.GetString();
                if (string.IsNullOrEmpty(trelloCardId))
                {
                    return (false, "Card id is null or empty.", null, false);
                }

                var checklistsUrl = $"https://api.trello.com/1/cards/{trelloCardId}/checklists?checkItems=all&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var checklistsResponse = await _httpClient.GetAsync(checklistsUrl);
                if (!checklistsResponse.IsSuccessStatusCode)
                {
                    var err = await checklistsResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to get checklists for card {CardId}: {Error}", trelloCardId, err);
                    return (false, $"Failed to get checklists for card: {err}", null, false);
                }

                var checklistsJson = await checklistsResponse.Content.ReadAsStringAsync();
                var checklists = JsonSerializer.Deserialize<JsonElement[]>(checklistsJson);
                if (checklists == null || checklists.Length == 0)
                {
                    return (false, "Card has no checklists.", null, false);
                }

                string? idCheckItem = null;
                string? currentState = null;
                int idx = 0;
                foreach (var cl in checklists)
                {
                    if (!cl.TryGetProperty("checkItems", out var itemsProp) || itemsProp.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }
                    foreach (var item in itemsProp.EnumerateArray())
                    {
                        if (idx == checkIndex)
                        {
                            if (item.TryGetProperty("id", out var itemIdProp))
                            {
                                idCheckItem = itemIdProp.GetString();
                            }
                            if (item.TryGetProperty("state", out var stateProp))
                            {
                                currentState = stateProp.GetString();
                            }
                            break;
                        }
                        idx++;
                    }
                    if (idCheckItem != null)
                    {
                        break;
                    }
                }

                if (string.IsNullOrEmpty(idCheckItem))
                {
                    return (false, $"Check item at index {checkIndex} not found. Card has fewer than {checkIndex + 1} check items.", null, false);
                }

                var newState = string.Equals(currentState, "complete", StringComparison.OrdinalIgnoreCase)
                    ? "incomplete"
                    : "complete";

                var updateUrl = $"https://api.trello.com/1/cards/{trelloCardId}/checkItem/{idCheckItem}?state={Uri.EscapeDataString(newState)}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var updateResponse = await _httpClient.PutAsync(updateUrl, null);
                if (!updateResponse.IsSuccessStatusCode)
                {
                    var err = await updateResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to update check item {CheckItemId} on card {CardId}: {Error}", idCheckItem, trelloCardId, err);
                    return (false, $"Failed to toggle check item: {err}", null, false);
                }

                _logger.LogInformation("✅ [TRELLO] Toggled check item at index {CheckIndex} on card {CardId} to {State}", checkIndex, cardId, newState);

                var cardMarkedComplete = false;
                if (string.Equals(newState, "complete", StringComparison.OrdinalIgnoreCase))
                {
                    var allComplete = await AreAllCheckItemsCompleteAsync(trelloCardId);
                    if (allComplete)
                    {
                        cardMarkedComplete = await MarkCardCompleteAsync(trelloCardId);
                        if (cardMarkedComplete)
                        {
                            _logger.LogInformation("✅ [TRELLO] All checklist items complete; card {CardId} marked complete (dueComplete).", cardId);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ [TRELLO] All checklist items complete but failed to mark card {CardId} complete.", cardId);
                        }
                    }
                }
                else
                {
                    // User unchecked an item; clear dueComplete so the card is no longer shown as complete
                    var cleared = await MarkCardIncompleteAsync(trelloCardId);
                    if (cleared)
                        _logger.LogInformation("✅ [TRELLO] Checklist item unchecked; card {CardId} set to dueComplete=false.", cardId);
                }

                return (true, null, newState, cardMarkedComplete);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [TRELLO] Error toggling check item at index {CheckIndex} for CardId '{CardId}': {Message}", checkIndex, cardId, ex.Message);
                return (false, ex.Message, null, false);
            }
        }

        /// <summary>
        /// Returns true if the card has at least one check item and all check items are in "complete" state.
        /// </summary>
        private async Task<bool> AreAllCheckItemsCompleteAsync(string trelloCardId)
        {
            try
            {
                var checklistsUrl = $"https://api.trello.com/1/cards/{trelloCardId}/checklists?checkItems=all&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var response = await _httpClient.GetAsync(checklistsUrl);
                if (!response.IsSuccessStatusCode)
                    return false;

                var json = await response.Content.ReadAsStringAsync();
                var checklists = JsonSerializer.Deserialize<JsonElement[]>(json);
                if (checklists == null || checklists.Length == 0)
                    return false;

                int totalItems = 0;
                int completeCount = 0;
                foreach (var cl in checklists)
                {
                    if (!cl.TryGetProperty("checkItems", out var itemsProp) || itemsProp.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var item in itemsProp.EnumerateArray())
                    {
                        totalItems++;
                        if (item.TryGetProperty("state", out var stateProp) && string.Equals(stateProp.GetString(), "complete", StringComparison.OrdinalIgnoreCase))
                            completeCount++;
                    }
                }
                return totalItems > 0 && totalItems == completeCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check if all items complete for card {CardId}", trelloCardId);
                return false;
            }
        }

        /// <summary>
        /// Marks the Trello card as complete via PUT /1/cards/{id}?dueComplete=true. Card stays on the board (not closed/archived).
        /// </summary>
        private async Task<bool> MarkCardCompleteAsync(string trelloCardId)
        {
            try
            {
                var url = $"https://api.trello.com/1/cards/{trelloCardId}?dueComplete=true&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var response = await _httpClient.PutAsync(url, null);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Card {CardId} marked complete (dueComplete) via Trello API.", trelloCardId);
                    return true;
                }
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to mark card {CardId} complete: {Error}", trelloCardId, err);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking card {CardId} complete", trelloCardId);
                return false;
            }
        }

        /// <summary>
        /// Sets the Trello card to dueComplete=false via PUT /1/cards/{id}?dueComplete=false. Used when user unchecks an item on a previously complete card.
        /// </summary>
        private async Task<bool> MarkCardIncompleteAsync(string trelloCardId)
        {
            try
            {
                var url = $"https://api.trello.com/1/cards/{trelloCardId}?dueComplete=false&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var response = await _httpClient.PutAsync(url, null);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Card {CardId} set dueComplete=false via Trello API.", trelloCardId);
                    return true;
                }
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to set card {CardId} dueComplete=false: {Error}", trelloCardId, err);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting card {CardId} dueComplete=false", trelloCardId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<SprintSnapshot?> GetSprintFromBoardAsync(string boardId, string sprintListName)
        {
            try
            {
                var listsUrl = $"https://api.trello.com/1/boards/{boardId}/lists?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var listsResponse = await _httpClient.GetAsync(listsUrl);
                if (!listsResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get lists for board {BoardId}: {Status}", boardId, listsResponse.StatusCode);
                    return null;
                }
                var listsJson = await listsResponse.Content.ReadAsStringAsync();
                var lists = JsonSerializer.Deserialize<JsonElement[]>(listsJson);
                if (lists == null || lists.Length == 0)
                    return null;
                string? listId = null;
                string? listName = null;
                foreach (var list in lists)
                {
                    var name = list.TryGetProperty("name", out var n) ? n.GetString() : "";
                    if (string.Equals(name, sprintListName, StringComparison.OrdinalIgnoreCase))
                    {
                        listId = list.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                        listName = name ?? sprintListName;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(listId))
                    return null;
                string? cardIdFieldId = null;
                var cfUrl = $"https://api.trello.com/1/boards/{boardId}/customFields?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var cfRes = await _httpClient.GetAsync(cfUrl);
                if (cfRes.IsSuccessStatusCode)
                {
                    var cfContent = await cfRes.Content.ReadAsStringAsync();
                    var cfArr = JsonSerializer.Deserialize<JsonElement[]>(cfContent);
                    if (cfArr != null)
                        foreach (var f in cfArr)
                            if (f.TryGetProperty("name", out var fn) && fn.GetString() == "CardId" && f.TryGetProperty("id", out var fi))
                            { cardIdFieldId = fi.GetString(); break; }
                }
                var cardsUrl = $"https://api.trello.com/1/lists/{listId}/cards?checklists=all&customFieldItems=true&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var cardsResponse = await _httpClient.GetAsync(cardsUrl);
                if (!cardsResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get cards for list {ListId}: {Status}", listId, cardsResponse.StatusCode);
                    return new SprintSnapshot { ListId = listId, ListName = listName ?? sprintListName, Cards = new List<SprintSnapshotCard>() };
                }
                var cardsJson = await cardsResponse.Content.ReadAsStringAsync();
                var cardsData = JsonSerializer.Deserialize<JsonElement[]>(cardsJson);
                var cards = new List<SprintSnapshotCard>();
                if (cardsData != null)
                {
                    foreach (var c in cardsData)
                    {
                        var name = c.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
                        var desc = c.TryGetProperty("desc", out var dp) ? dp.GetString() ?? "" : "";
                        DateTime? due = null;
                        if (c.TryGetProperty("due", out var dueProp))
                        {
                            var dueStr = dueProp.GetString();
                            if (!string.IsNullOrEmpty(dueStr) && DateTime.TryParse(dueStr, out var d))
                                due = d;
                        }
                        var roleName = "";
                        if (c.TryGetProperty("labels", out var labelsProp) && labelsProp.ValueKind == JsonValueKind.Array && labelsProp.GetArrayLength() > 0)
                        {
                            var first = labelsProp[0];
                            roleName = first.TryGetProperty("name", out var ln) ? ln.GetString() ?? "" : "";
                        }
                        var cardId = "";
                        if (!string.IsNullOrEmpty(cardIdFieldId) && c.TryGetProperty("customFieldItems", out var cfProp) && cfProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cf in cfProp.EnumerateArray())
                            {
                                var idField = cf.TryGetProperty("idCustomField", out var idf) ? idf.GetString() : null;
                                if (idField != cardIdFieldId) continue;
                                if (cf.TryGetProperty("value", out var vProp) && vProp.TryGetProperty("text", out var textProp))
                                    cardId = textProp.GetString() ?? "";
                                break;
                            }
                        }
                        var checklistItems = new List<string>();
                        if (c.TryGetProperty("checklists", out var clProp) && clProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cl in clProp.EnumerateArray())
                            {
                                if (!cl.TryGetProperty("checkItems", out var itemsProp) || itemsProp.ValueKind != JsonValueKind.Array)
                                    continue;
                                foreach (var item in itemsProp.EnumerateArray())
                                {
                                    var itemName = item.TryGetProperty("name", out var inProp) ? inProp.GetString() : null;
                                    if (!string.IsNullOrEmpty(itemName))
                                        checklistItems.Add(itemName);
                                }
                            }
                        }
                        cards.Add(new SprintSnapshotCard
                        {
                            Name = name ?? "",
                            Description = desc ?? "",
                            DueDate = due,
                            RoleName = roleName ?? "",
                            ChecklistItems = checklistItems,
                            CardId = string.IsNullOrEmpty(cardId) ? null : cardId
                        });
                    }
                }
                return new SprintSnapshot { ListId = listId, ListName = listName ?? sprintListName, Cards = cards };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sprint {SprintName} from board {BoardId}", sprintListName, boardId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? Error)> OverrideSprintOnBoardAsync(string boardId, string listId, IReadOnlyList<SprintSnapshotCard> cards)
        {
            var errors = new List<string>();
            try
            {
                var listCardsUrl = $"https://api.trello.com/1/lists/{listId}/cards?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var listCardsResponse = await _httpClient.GetAsync(listCardsUrl);
                if (listCardsResponse.IsSuccessStatusCode)
                {
                    var listCardsJson = await listCardsResponse.Content.ReadAsStringAsync();
                    var existingCards = JsonSerializer.Deserialize<JsonElement[]>(listCardsJson);
                    if (existingCards != null)
                    {
                        foreach (var ec in existingCards)
                        {
                            var cardId = ec.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                            if (string.IsNullOrEmpty(cardId)) continue;
                            var closeUrl = $"https://api.trello.com/1/cards/{cardId}?closed=true&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                            await _httpClient.PutAsync(closeUrl, null);
                        }
                    }
                }
                var labelsUrl = $"https://api.trello.com/1/boards/{boardId}/labels?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var roleLabelIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var labelsResponse = await _httpClient.GetAsync(labelsUrl);
                if (labelsResponse.IsSuccessStatusCode)
                {
                    var labelsJson = await labelsResponse.Content.ReadAsStringAsync();
                    var labelsData = JsonSerializer.Deserialize<JsonElement[]>(labelsJson);
                    if (labelsData != null)
                    {
                        foreach (var lb in labelsData)
                        {
                            var name = lb.TryGetProperty("name", out var n) ? n.GetString() : null;
                            var id = lb.TryGetProperty("id", out var i) ? i.GetString() : null;
                            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id))
                                roleLabelIds[name] = id;
                        }
                    }
                }
                var customFieldsUrl = $"https://api.trello.com/1/boards/{boardId}/customFields?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                string? cardIdFieldId = null;
                var cfResponse = await _httpClient.GetAsync(customFieldsUrl);
                if (cfResponse.IsSuccessStatusCode)
                {
                    var cfJson = await cfResponse.Content.ReadAsStringAsync();
                    var cfData = JsonSerializer.Deserialize<JsonElement[]>(cfJson);
                    if (cfData != null)
                    {
                        foreach (var f in cfData)
                        {
                            if (f.TryGetProperty("name", out var fn) && fn.GetString() == "CardId" && f.TryGetProperty("id", out var fi))
                            {
                                cardIdFieldId = fi.GetString();
                                break;
                            }
                        }
                    }
                }
                foreach (var card in cards)
                {
                    var createCardUrl = $"https://api.trello.com/1/cards?name={Uri.EscapeDataString(card.Name)}&desc={Uri.EscapeDataString(card.Description ?? "")}&idList={listId}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                    if (card.DueDate.HasValue)
                        createCardUrl += $"&due={card.DueDate.Value:yyyy-MM-dd}";
                    if (!string.IsNullOrEmpty(card.RoleName) && roleLabelIds.TryGetValue(card.RoleName, out var labelId))
                        createCardUrl += $"&idLabels={labelId}";
                    var createResponse = await _httpClient.PostAsync(createCardUrl, null);
                    if (!createResponse.IsSuccessStatusCode)
                    {
                        var err = await createResponse.Content.ReadAsStringAsync();
                        errors.Add($"Failed to create card '{card.Name}': {err}");
                        continue;
                    }
                    var cardJson = await createResponse.Content.ReadAsStringAsync();
                    var cardData = JsonSerializer.Deserialize<JsonElement>(cardJson);
                    var newCardId = cardData.GetProperty("id").GetString();
                    if (card.ChecklistItems != null && card.ChecklistItems.Count > 0)
                    {
                        var createClUrl = $"https://api.trello.com/1/checklists?name=Checklist&idCard={newCardId}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                        var clResponse = await _httpClient.PostAsync(createClUrl, null);
                        if (clResponse.IsSuccessStatusCode)
                        {
                            var clJson = await clResponse.Content.ReadAsStringAsync();
                            var clData = JsonSerializer.Deserialize<JsonElement>(clJson);
                            var checklistId = clData.GetProperty("id").GetString();
                            int pos = 1;
                            foreach (var item in card.ChecklistItems)
                            {
                                var itemName = StripChecklistLinePrefix(item);
                                var addItemUrl = $"https://api.trello.com/1/checklists/{checklistId}/checkItems?name={Uri.EscapeDataString(itemName)}&pos={pos}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                                await _httpClient.PostAsync(addItemUrl, null);
                                pos++;
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(card.CardId) && !string.IsNullOrEmpty(cardIdFieldId))
                        await SetCustomFieldValueAsync(newCardId, cardIdFieldId, "text", card.CardId, errors);
                }
                if (errors.Count > 0)
                    return (false, string.Join("; ", errors));
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error overriding sprint on board {BoardId} list {ListId}", boardId, listId);
                return (false, ex.Message);
            }
        }

        /// <inheritdoc />
        public async Task<string?> EnsureNextEmptySprintOnBoardAsync(string boardId, TrelloProjectCreationRequest request, int nextSprintNumber, DateTime? dueDateForNewCards = null)
        {
            if (request?.SprintPlan?.Lists == null || request.SprintPlan.Lists.Count == 0)
            {
                _logger.LogDebug("EnsureNextEmptySprint: no lists in template, skipping next sprint {NextSprint}.", nextSprintNumber);
                return null;
            }
            var sprintNameNoSpace = $"Sprint{nextSprintNumber}";
            var sprintNameWithSpace = $"Sprint {nextSprintNumber}";
            var templateList = request.SprintPlan.Lists
                .FirstOrDefault(l => l.Position == nextSprintNumber || string.Equals(l.Name, sprintNameWithSpace, StringComparison.OrdinalIgnoreCase) || string.Equals(l.Name, sprintNameNoSpace, StringComparison.OrdinalIgnoreCase));
            if (templateList == null)
            {
                _logger.LogDebug("EnsureNextEmptySprint: no list for sprint {NextSprint} in template (Lists: {Names}).", nextSprintNumber, string.Join(", ", request.SprintPlan.Lists.Select(l => l.Name)));
                return null;
            }
            var listName = templateList.Name;
            var templateCards = (request.SprintPlan.Cards ?? new List<TrelloCard>())
                .Where(c => string.Equals(c.ListName, listName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var listsUrl = $"https://api.trello.com/1/boards/{boardId}/lists?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
            var listsResponse = await _httpClient.GetAsync(listsUrl);
            if (!listsResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("EnsureNextEmptySprint: failed to get lists for board {BoardId}: {Status}.", boardId, listsResponse.StatusCode);
                return null;
            }
            var listsJson = await listsResponse.Content.ReadAsStringAsync();
            var lists = JsonSerializer.Deserialize<JsonElement[]>(listsJson);
            if (lists != null)
            {
                foreach (var list in lists)
                {
                    var name = list.TryGetProperty("name", out var n) ? n.GetString() : "";
                    if (string.Equals(name, listName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("EnsureNextEmptySprint: list {ListName} already exists on board {BoardId}, skipping.", listName, boardId);
                        return null;
                    }
                }
            }

            try
            {
                var createListUrl = $"https://api.trello.com/1/boards/{boardId}/lists?name={Uri.EscapeDataString(listName)}&pos={templateList.Position}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var createListResponse = await _httpClient.PostAsync(createListUrl, null);
                if (!createListResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("EnsureNextEmptySprint: failed to create list {ListName} on board {BoardId}: {Status}.", listName, boardId, createListResponse.StatusCode);
                    return null;
                }
                var listJson = await createListResponse.Content.ReadAsStringAsync();
                var listData = JsonSerializer.Deserialize<JsonElement>(listJson);
                var newListId = listData.GetProperty("id").GetString();
                if (string.IsNullOrEmpty(newListId))
                {
                    _logger.LogWarning("EnsureNextEmptySprint: created list but no list id returned.");
                    return null;
                }

                var roleLabelIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var labelsUrl = $"https://api.trello.com/1/boards/{boardId}/labels?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var labelsResponse = await _httpClient.GetAsync(labelsUrl);
                if (labelsResponse.IsSuccessStatusCode)
                {
                    var labelsJson = await labelsResponse.Content.ReadAsStringAsync();
                    var labelsData = JsonSerializer.Deserialize<JsonElement[]>(labelsJson);
                    if (labelsData != null)
                        foreach (var lb in labelsData)
                        {
                            var nm = lb.TryGetProperty("name", out var nn) ? nn.GetString() : null;
                            var id = lb.TryGetProperty("id", out var ii) ? ii.GetString() : null;
                            if (!string.IsNullOrEmpty(nm) && !string.IsNullOrEmpty(id))
                                roleLabelIds[nm] = id;
                        }
                }

                string? cardIdFieldId = null;
                var cfUrl = $"https://api.trello.com/1/boards/{boardId}/customFields?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var cfRes = await _httpClient.GetAsync(cfUrl);
                if (cfRes.IsSuccessStatusCode)
                {
                    var cfContent = await cfRes.Content.ReadAsStringAsync();
                    var cfArr = JsonSerializer.Deserialize<JsonElement[]>(cfContent);
                    if (cfArr != null)
                        foreach (var f in cfArr)
                            if (f.TryGetProperty("name", out var fn) && fn.GetString() == "CardId" && f.TryGetProperty("id", out var fi))
                            { cardIdFieldId = fi.GetString(); break; }
                }

                var errors = new List<string>();
                foreach (var card in templateCards)
                {
                    var desc = "To be filled...";
                    var createCardUrl = $"https://api.trello.com/1/cards?name={Uri.EscapeDataString(card.Name)}&desc={Uri.EscapeDataString(desc)}&idList={newListId}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                    var cardDue = dueDateForNewCards ?? (card.DueDate.HasValue ? (DateTime?)card.DueDate.Value : null);
                    if (cardDue.HasValue)
                        createCardUrl += $"&due={cardDue.Value:yyyy-MM-dd}";
                    if (!string.IsNullOrEmpty(card.RoleName) && roleLabelIds.TryGetValue(card.RoleName, out var labelId))
                        createCardUrl += $"&idLabels={labelId}";
                    var cardRes = await _httpClient.PostAsync(createCardUrl, null);
                    if (!cardRes.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("EnsureNextEmptySprint: failed to create card {CardName}: {Status}.", card.Name, cardRes.StatusCode);
                        continue;
                    }
                    var cardResJson = await cardRes.Content.ReadAsStringAsync();
                    var cardResData = JsonSerializer.Deserialize<JsonElement>(cardResJson);
                    var newCardId = cardResData.GetProperty("id").GetString();
                    var createClUrl = $"https://api.trello.com/1/checklists?name=Checklist&idCard={newCardId}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                    var clRes = await _httpClient.PostAsync(createClUrl, null);
                    if (clRes.IsSuccessStatusCode)
                    {
                        var clJson = await clRes.Content.ReadAsStringAsync();
                        var clData = JsonSerializer.Deserialize<JsonElement>(clJson);
                        var checklistId = clData.GetProperty("id").GetString();
                        var addItemUrl = $"https://api.trello.com/1/checklists/{checklistId}/checkItems?name={Uri.EscapeDataString("Create a Checklist...")}&pos=1&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                        await _httpClient.PostAsync(addItemUrl, null);
                    }
                    if (!string.IsNullOrEmpty(card.CardId) && !string.IsNullOrEmpty(cardIdFieldId))
                        await SetCustomFieldValueAsync(newCardId!, cardIdFieldId, "text", card.CardId, errors);
                }
                _logger.LogInformation("EnsureNextEmptySprint: created list {ListName} with {Count} empty cards on board {BoardId}.", listName, templateCards.Count, boardId);

                // Reorder all lists to canonical order (Sprint 1, 2, ... 7, Bugs last) on the live Trello board
                var listsUrl2 = $"https://api.trello.com/1/boards/{boardId}/lists?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var listsResponse2 = await _httpClient.GetAsync(listsUrl2);
                if (listsResponse2.IsSuccessStatusCode)
                {
                    var listsJson2 = await listsResponse2.Content.ReadAsStringAsync();
                    var lists2 = JsonSerializer.Deserialize<JsonElement[]>(listsJson2);
                    if (lists2 != null && lists2.Length > 0)
                    {
                        var canonicalNames = new[] { "Sprint 1", "Sprint 2", "Sprint 3", "Sprint 4", "Sprint 5", "Sprint 6", "Sprint 7", "Bugs" };
                        var orderedIds = new List<string>();
                        foreach (var name in canonicalNames)
                        {
                            var listEl = lists2.FirstOrDefault(l =>
                                string.Equals(l.TryGetProperty("name", out var n) ? n.GetString() : null, name, StringComparison.OrdinalIgnoreCase));
                            if (listEl.ValueKind != JsonValueKind.Undefined && listEl.TryGetProperty("id", out var idProp))
                            {
                                var id = idProp.GetString();
                                if (!string.IsNullOrEmpty(id))
                                    orderedIds.Add(id);
                            }
                        }
                        foreach (var listEl in lists2)
                        {
                            var nm = listEl.TryGetProperty("name", out var nn) ? nn.GetString() : null;
                            if (string.IsNullOrEmpty(nm)) continue;
                            if (canonicalNames.Any(c => string.Equals(c, nm, StringComparison.OrdinalIgnoreCase)))
                                continue;
                            if (listEl.TryGetProperty("id", out var idProp2))
                            {
                                var id2 = idProp2.GetString();
                                if (!string.IsNullOrEmpty(id2))
                                    orderedIds.Add(id2);
                            }
                        }
                        for (var i = 0; i < orderedIds.Count; i++)
                        {
                            var pos = (i + 1) * 65536.0;
                            var putUrl = $"https://api.trello.com/1/lists/{orderedIds[i]}?pos={pos}&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                            var putRes = await _httpClient.PutAsync(putUrl, null);
                            if (!putRes.IsSuccessStatusCode)
                                _logger.LogWarning("EnsureNextEmptySprint: failed to set list position at index {Index} on board {BoardId}: {Status}.", i, boardId, putRes.StatusCode);
                        }
                        _logger.LogInformation("EnsureNextEmptySprint: reordered {Count} lists to canonical order (Sprint 1..7, Bugs last) on board {BoardId}.", orderedIds.Count, boardId);
                    }
                }
                return newListId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EnsureNextEmptySprint: error creating next sprint {NextSprint} on board {BoardId}.", nextSprintNumber, boardId);
                return null;
            }
        }
    }
}
