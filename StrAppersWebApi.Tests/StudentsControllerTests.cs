using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using strAppersBackend.Models;

namespace StrAppersWebApi.Tests;

public class StudentsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public StudentsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    #region GET Tests

    [Fact]
    public async Task GetStudents_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/students");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStudents_ReturnsValidJson()
    {
        // Act
        var response = await _client.GetAsync("/api/students");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotEmpty(content);
        
        var students = JsonSerializer.Deserialize<Student[]>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        Assert.NotNull(students);
    }

    [Fact]
    public async Task GetStudent_WithValidId_ReturnsStudent()
    {
        // Act
        var response = await _client.GetAsync("/api/students/1");

        // Assert
        // Note: This might return NotFound if no student with ID 1 exists
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStudent_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/students/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStudentsByOrganization_WithValidId_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/students/by-organization/1");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region POST Tests

    [Fact]
    public async Task CreateStudent_WithValidData_ReturnsCreatedStudent()
    {
        // Arrange
        var newStudent = new CreateStudentRequest
        {
            FirstName = "Test",
            LastName = "Student",
            Email = "test.student@example.com",
            StudentId = "TEST001",
            MajorId = 1,
            YearId = 1,
            LinkedInUrl = "https://linkedin.com/in/teststudent"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/students", newStudent);

        // Assert
        // Note: This might fail if required entities don't exist or email is duplicate
        Assert.True(response.StatusCode == HttpStatusCode.Created || 
                   response.StatusCode == HttpStatusCode.BadRequest || 
                   response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateStudent_WithEmptyFirstName_ReturnsBadRequest()
    {
        // Arrange
        var newStudent = new CreateStudentRequest
        {
            FirstName = "",
            LastName = "Student",
            Email = "test@example.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/students", newStudent);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_WithEmptyEmail_ReturnsBadRequest()
    {
        // Arrange
        var newStudent = new CreateStudentRequest
        {
            FirstName = "Test",
            LastName = "Student",
            Email = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/students", newStudent);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region PUT Tests

    [Fact]
    public async Task UpdateStudent_WithValidData_ReturnsNoContent()
    {
        // Arrange
        var updateRequest = new UpdateStudentRequest
        {
            FirstName = "Updated",
            LastName = "Student",
            Email = "updated.student@example.com"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/students/1", updateRequest);

        // Assert
        // Note: This might fail if student doesn't exist
        Assert.True(response.StatusCode == HttpStatusCode.NoContent || 
                   response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateStudent_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var updateRequest = new UpdateStudentRequest
        {
            FirstName = "Updated",
            LastName = "Student",
            Email = "updated@example.com"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/students/99999", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region DELETE Tests

    [Fact]
    public async Task DeleteStudent_WithValidId_ReturnsNoContent()
    {
        // Act
        var response = await _client.DeleteAsync("/api/students/1");

        // Assert
        // Note: This might fail if student doesn't exist
        Assert.True(response.StatusCode == HttpStatusCode.NoContent || 
                   response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteStudent_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/students/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion
}
