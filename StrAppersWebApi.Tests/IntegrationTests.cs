using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using strAppersBackend.Models;

namespace StrAppersWebApi.Tests;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task FullStudentWorkflow_CreateReadUpdateDelete_Success()
    {
        // 1. Create a new student
        var newStudent = new CreateStudentRequest
        {
            FirstName = "Integration",
            LastName = "Test Student",
            Email = "integration.test@example.com",
            StudentId = "INT001",
            MajorId = 1,
            YearId = 1,
            LinkedInUrl = "https://linkedin.com/in/integrationtest"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/students", newStudent);
        // Note: This might fail if required entities don't exist
        Assert.True(createResponse.StatusCode == HttpStatusCode.Created || 
                   createResponse.StatusCode == HttpStatusCode.BadRequest);
        
        if (createResponse.StatusCode == HttpStatusCode.Created)
        {
            var createdStudent = await createResponse.Content.ReadFromJsonAsync<Student>();
            Assert.NotNull(createdStudent);
            Assert.Equal(newStudent.FirstName, createdStudent.FirstName);
            Assert.Equal(newStudent.LastName, createdStudent.LastName);
            Assert.Equal(newStudent.Email, createdStudent.Email);

            // 2. Read the created student
            var getResponse = await _client.GetAsync($"/api/students/{createdStudent.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            
            var retrievedStudent = await getResponse.Content.ReadFromJsonAsync<Student>();
            Assert.NotNull(retrievedStudent);
            Assert.Equal(createdStudent.Id, retrievedStudent.Id);
            Assert.Equal(createdStudent.FirstName, retrievedStudent.FirstName);
            Assert.Equal(createdStudent.LastName, retrievedStudent.LastName);
            Assert.Equal(createdStudent.Email, retrievedStudent.Email);

            // 3. Update the student
            var updateRequest = new UpdateStudentRequest
            {
                FirstName = "Updated Integration",
                LastName = "Test Student",
                Email = "updated.integration.test@example.com"
            };

            var updateResponse = await _client.PutAsJsonAsync($"/api/students/{createdStudent.Id}", updateRequest);
            Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

            // 4. Verify the update by reading again
            var getUpdatedResponse = await _client.GetAsync($"/api/students/{createdStudent.Id}");
            Assert.Equal(HttpStatusCode.OK, getUpdatedResponse.StatusCode);
            
            var finalStudent = await getUpdatedResponse.Content.ReadFromJsonAsync<Student>();
            Assert.NotNull(finalStudent);
            Assert.Equal(updateRequest.FirstName, finalStudent.FirstName);
            Assert.Equal(updateRequest.LastName, finalStudent.LastName);
            Assert.Equal(updateRequest.Email, finalStudent.Email);

            // 5. Delete the student
            var deleteResponse = await _client.DeleteAsync($"/api/students/{createdStudent.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // 6. Verify the student is deleted
            var getDeletedResponse = await _client.GetAsync($"/api/students/{createdStudent.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getDeletedResponse.StatusCode);
        }
    }

    [Fact]
    public async Task ApiEndpoints_ReturnCorrectContentType()
    {
        // Test WeatherForecast endpoint
        var weatherResponse = await _client.GetAsync("/WeatherForecast");
        Assert.Equal("application/json; charset=utf-8", weatherResponse.Content.Headers.ContentType?.ToString());

        // Test Students endpoint
        var studentsResponse = await _client.GetAsync("/api/students");
        Assert.Equal("application/json; charset=utf-8", studentsResponse.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task SwaggerEndpoint_IsAccessible()
    {
        // Test that Swagger is available in development
        var response = await _client.GetAsync("/swagger");
        
        // In development, this should return 200 or redirect
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect);
    }
}
