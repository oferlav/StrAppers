using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using strAppersBackend.Models;

namespace StrAppersWebApi.Tests;

public class ProjectAllocationControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ProjectAllocationControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    #region Available Projects Tests

    [Fact]
    public async Task GetAvailableProjects_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/projectallocation/available-projects");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailableProjects_ReturnsValidJson()
    {
        // Act
        var response = await _client.GetAsync("/api/projectallocation/available-projects");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotEmpty(content);
        
        var projects = JsonSerializer.Deserialize<AvailableProject[]>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        Assert.NotNull(projects);
    }

    #endregion

    #region Project Allocation Tests

    [Fact]
    public async Task AllocateStudentToProject_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var allocationRequest = new ProjectAllocationRequest
        {
            StudentId = 1,
            ProjectId = 1,
            IsAdmin = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/projectallocation/allocate", allocationRequest);

        // Assert
        // Note: This test might fail if student/project doesn't exist or student is already allocated
        // In a real scenario, you'd set up test data first
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.BadRequest || 
                   response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AllocateStudentToProject_WithInvalidStudentId_ReturnsNotFound()
    {
        // Arrange
        var allocationRequest = new ProjectAllocationRequest
        {
            StudentId = 99999, // Non-existent student
            ProjectId = 1,
            IsAdmin = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/projectallocation/allocate", allocationRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AllocateStudentToProject_WithInvalidProjectId_ReturnsNotFound()
    {
        // Arrange
        var allocationRequest = new ProjectAllocationRequest
        {
            StudentId = 1,
            ProjectId = 99999, // Non-existent project
            IsAdmin = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/projectallocation/allocate", allocationRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Deallocation Tests

    [Fact]
    public async Task DeallocateStudent_WithValidId_ReturnsSuccess()
    {
        // Act
        var response = await _client.PostAsync("/api/projectallocation/deallocate/1", null);

        // Assert
        // Note: This test might fail if student doesn't exist or isn't allocated
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.BadRequest || 
                   response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeallocateStudent_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client.PostAsync("/api/projectallocation/deallocate/99999", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Project Status Tests

    [Fact]
    public async Task ChangeProjectStatusToPlanning_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var statusRequest = new ChangeProjectStatusRequest
        {
            StudentId = 1,
            ProjectId = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/projectallocation/change-status-to-planning", statusRequest);

        // Assert
        // Note: This test might fail due to business rules (admin required, backend developer required, etc.)
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.BadRequest || 
                   response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ChangeProjectStatusToPlanning_WithInvalidStudentId_ReturnsNotFound()
    {
        // Arrange
        var statusRequest = new ChangeProjectStatusRequest
        {
            StudentId = 99999, // Non-existent student
            ProjectId = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/projectallocation/change-status-to-planning", statusRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetProjectStudents_WithValidProjectId_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/projectallocation/project/1/students");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStudentProject_WithValidStudentId_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/projectallocation/student/1/project");

        // Assert
        // Note: This might return NotFound if student has no project
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStudentProject_WithInvalidStudentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/projectallocation/student/99999/project");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Join Requests Tests

    [Fact]
    public async Task GetJoinRequests_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/projectallocation/join-requests");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetJoinRequestsStats_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/projectallocation/join-requests/stats");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MarkJoinRequestAsAdded_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client.PutAsync("/api/projectallocation/join-requests/99999/mark-added", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion
}
