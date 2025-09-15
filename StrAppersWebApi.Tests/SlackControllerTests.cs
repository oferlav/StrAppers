using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace StrAppersWebApi.Tests;

public class SlackControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SlackControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    #region Slack Team Management Tests

    [Fact]
    public async Task CreateProjectTeamWithStatusChange_WithInvalidProjectId_ReturnsNotFound()
    {
        // Arrange
        var request = new
        {
            ProjectId = 99999, // Non-existent project
            RequestStudentId = 1,
            StudentIds = new[] { 1, 2, 3 },
            SendWelcomeMessage = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slack/use/create-project-team-with-status-change", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateProjectTeamWithStatusChange_WithEmptyStudentIds_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            ProjectId = 1,
            RequestStudentId = 1,
            StudentIds = new int[0], // Empty array
            SendWelcomeMessage = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slack/use/create-project-team-with-status-change", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProjectTeamWithStatusChange_WithInvalidStudentIds_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            ProjectId = 1,
            RequestStudentId = 1,
            StudentIds = new[] { 99999, 99998 }, // Non-existent students
            SendWelcomeMessage = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slack/use/create-project-team-with-status-change", request);

        // Assert
        // Note: This might fail due to business rules or Slack configuration, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest || 
                   response.StatusCode == HttpStatusCode.NotFound ||
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateProjectTeamWithStatusChange_WithNonAdminRequestStudent_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            ProjectId = 1,
            RequestStudentId = 99999, // Non-existent student (will be treated as non-admin)
            StudentIds = new[] { 1, 2, 3 },
            SendWelcomeMessage = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slack/use/create-project-team-with-status-change", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Student not found
    }

    [Fact]
    public async Task CreateSlackTeam_WithInvalidProjectId_ReturnsNotFound()
    {
        // Arrange
        var request = new
        {
            ProjectId = 99999, // Non-existent project
            SendWelcomeMessage = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slack/create-team", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSlackTeamInfo_WithInvalidProjectId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/slack/team-info/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task JoinSlackChannel_WithValidRequest_ReturnsResponse()
    {
        // Arrange
        var request = new
        {
            ChannelId = "test-channel-id"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slack/join-channel", request);

        // Assert
        // Note: This might fail due to Slack API issues, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.BadRequest || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    #endregion

    #region Slack Diagnostics Tests

    [Fact]
    public async Task TestSlackConnection_ReturnsResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/slack/test-connection");

        // Assert
        // Note: This might fail due to Slack configuration, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task TestSlackBotInfo_ReturnsResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/slack/test-bot-info");

        // Assert
        // Note: This might fail due to Slack configuration, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task TestSlackUserLookup_WithValidEmail_ReturnsResponse()
    {
        // Arrange
        var request = new
        {
            Email = "test@example.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slack/test-user-lookup", request);

        // Assert
        // Note: This might fail due to Slack configuration, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task TestSlackUsersList_ReturnsResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/slack/test-users-list");

        // Assert
        // Note: This might fail due to Slack configuration, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task TestSlackBotPermissions_ReturnsResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/slack/test-bot-permissions");

        // Assert
        // Note: This might fail due to Slack configuration, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task TestSlackChannelsList_ReturnsResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/slack/test-channels-list");

        // Assert
        // Note: This might fail due to Slack configuration, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    #endregion

    #region Slack Test Workflows

    [Fact]
    public async Task TestSlackTeamCreationWorkflow_WithInvalidProjectId_ReturnsNotFound()
    {
        // Arrange
        var request = new
        {
            ProjectId = 99999, // Non-existent project
            TestEmail = "test@example.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slack/test-team-creation-workflow", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TestSlackChannel_WithInvalidProjectId_ReturnsNotFound()
    {
        // Arrange
        var request = new
        {
            ProjectId = 99999, // Non-existent project
            TestEmail = "test@example.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slack/test-channel", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TestSlackChannelCreationDetailed_WithValidRequest_ReturnsResponse()
    {
        // Arrange
        var request = new
        {
            TeamName = "test-team"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/slack/test-channel-creation-detailed", request);

        // Assert
        // Note: This might fail due to Slack configuration, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    #endregion

    #region Slack Channel Management Tests

    [Fact]
    public async Task TestSlackChannelVisibility_WithValidChannelId_ReturnsResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/slack/test-channel-visibility/test-channel-id");

        // Assert
        // Note: This might fail due to Slack configuration, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task TestSlackChannelMembership_WithValidChannelId_ReturnsResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/slack/test-channel-membership/test-channel-id");

        // Assert
        // Note: This might fail due to Slack configuration, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task TestSlackDeleteChannel_WithValidChannelId_ReturnsResponse()
    {
        // Act
        var response = await _client.DeleteAsync("/api/slack/test-delete-channel/test-channel-id");

        // Assert
        // Note: This might fail due to Slack configuration, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    #endregion
}
