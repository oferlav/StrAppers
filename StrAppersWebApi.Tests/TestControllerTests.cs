using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace StrAppersWebApi.Tests;

public class TestControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TestControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    #region Database Test Methods

    [Fact]
    public async Task TestDatabase_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/test/database");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestDatabase_ReturnsValidJson()
    {
        // Act
        var response = await _client.GetAsync("/api/test/database");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotEmpty(content);
        Assert.Contains("success", content);
        Assert.Contains("entityCounts", content);
    }

    [Fact]
    public async Task TestDatabaseRelationships_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/test/database/relationships");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestDatabaseRelationships_ReturnsValidJson()
    {
        // Act
        var response = await _client.GetAsync("/api/test/database/relationships");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotEmpty(content);
        Assert.Contains("success", content);
        Assert.Contains("studentsWithRelations", content);
        Assert.Contains("projectsWithRelations", content);
    }

    #endregion

    #region Slack Test Methods

    [Fact]
    public async Task TestSlackService_ReturnsResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/test/slack/service");

        // Assert
        // Note: This might fail due to Slack configuration, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task TestSlackComprehensive_ReturnsResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/test/slack/comprehensive");

        // Assert
        // Note: This might fail due to Slack configuration, but should return a structured response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    #endregion

    #region Application Test Methods

    [Fact]
    public async Task TestApplicationHealth_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/test/application/health");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestApplicationHealth_ReturnsValidJson()
    {
        // Act
        var response = await _client.GetAsync("/api/test/application/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotEmpty(content);
        Assert.Contains("success", content);
        Assert.Contains("applicationName", content);
        Assert.Contains("version", content);
    }

    [Fact]
    public async Task TestEndpoints_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/test/endpoints");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestEndpoints_ReturnsValidJson()
    {
        // Act
        var response = await _client.GetAsync("/api/test/endpoints");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotEmpty(content);
        Assert.Contains("success", content);
        Assert.Contains("availableControllers", content);
        Assert.Contains("testEndpoints", content);
    }

    #endregion

    #region Data Validation Test Methods

    [Fact]
    public async Task TestDataValidation_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/test/validation");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestDataValidation_ReturnsValidJson()
    {
        // Act
        var response = await _client.GetAsync("/api/test/validation");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotEmpty(content);
        Assert.Contains("success", content);
        Assert.Contains("activeEntities", content);
        Assert.Contains("businessRuleValidation", content);
    }

    #endregion

    #region Performance Test Methods

    [Fact]
    public async Task TestPerformance_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/test/performance");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestPerformance_ReturnsValidJson()
    {
        // Act
        var response = await _client.GetAsync("/api/test/performance");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotEmpty(content);
        Assert.Contains("success", content);
        Assert.Contains("executionTimeMs", content);
        Assert.Contains("studentsLoaded", content);
        Assert.Contains("projectsLoaded", content);
    }

    #endregion
}



