using Xunit;
using strAppersBackend.Models;
using strAppersBackend;

namespace StrAppersWebApi.Tests;

public class ModelTests
{
    [Fact]
    public void WeatherForecast_TemperatureF_CalculatesCorrectly()
    {
        // Arrange
        var forecast = new WeatherForecast
        {
            TemperatureC = 0
        };

        // Act & Assert
        Assert.Equal(32, forecast.TemperatureF);
    }

    [Fact]
    public void WeatherForecast_TemperatureF_WithPositiveCelsius_CalculatesCorrectly()
    {
        // Arrange
        var forecast = new WeatherForecast
        {
            TemperatureC = 25
        };

        // Act & Assert
        Assert.Equal(76, forecast.TemperatureF); // 32 + (int)(25 / 0.5556) = 32 + 44 = 76
    }

    [Fact]
    public void WeatherForecast_TemperatureF_WithNegativeCelsius_CalculatesCorrectly()
    {
        // Arrange
        var forecast = new WeatherForecast
        {
            TemperatureC = -10
        };

        // Act & Assert
        Assert.Equal(15, forecast.TemperatureF); // 32 + (int)(-10 / 0.5556) = 32 + (-17) = 15
    }

    [Fact]
    public void Student_DefaultValues_AreSetCorrectly()
    {
        // Act
        var student = new Student();

        // Assert
        Assert.Equal(0, student.Id);
        Assert.Equal(string.Empty, student.FirstName);
        Assert.Equal(string.Empty, student.LastName);
        Assert.Equal(string.Empty, student.Email);
        Assert.True(student.CreatedAt <= DateTime.UtcNow);
        Assert.True(student.CreatedAt >= DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void CreateStudentRequest_DefaultValues_AreSetCorrectly()
    {
        // Act
        var request = new CreateStudentRequest();

        // Assert
        Assert.Equal(string.Empty, request.FirstName);
        Assert.Equal(string.Empty, request.LastName);
        Assert.Equal(string.Empty, request.Email);
    }

    [Fact]
    public void UpdateStudentRequest_DefaultValues_AreSetCorrectly()
    {
        // Act
        var request = new UpdateStudentRequest();

        // Assert
        Assert.Equal(string.Empty, request.FirstName);
        Assert.Equal(string.Empty, request.LastName);
        Assert.Equal(string.Empty, request.Email);
    }

    [Theory]
    [InlineData("John", "Doe", "john.doe@example.com")]
    [InlineData("Jane", "Smith", "jane.smith@company.com")]
    [InlineData("Test", "User", "test.user@domain.org")]
    public void Student_WithValidData_SetsPropertiesCorrectly(string firstName, string lastName, string email)
    {
        // Arrange
        var id = 1;
        var createdAt = DateTime.UtcNow;

        // Act
        var student = new Student
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            CreatedAt = createdAt
        };

        // Assert
        Assert.Equal(id, student.Id);
        Assert.Equal(firstName, student.FirstName);
        Assert.Equal(lastName, student.LastName);
        Assert.Equal(email, student.Email);
        Assert.Equal(createdAt, student.CreatedAt);
    }

    [Theory]
    [InlineData("John", "Doe", "john.doe@example.com")]
    [InlineData("Jane", "Smith", "jane.smith@company.com")]
    public void CreateStudentRequest_WithValidData_SetsPropertiesCorrectly(string firstName, string lastName, string email)
    {
        // Act
        var request = new CreateStudentRequest
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email
        };

        // Assert
        Assert.Equal(firstName, request.FirstName);
        Assert.Equal(lastName, request.LastName);
        Assert.Equal(email, request.Email);
    }

    [Theory]
    [InlineData("Updated", "Name", "updated.name@example.com")]
    [InlineData("New", "Name", "new.name@domain.com")]
    public void UpdateStudentRequest_WithValidData_SetsPropertiesCorrectly(string firstName, string lastName, string email)
    {
        // Act
        var request = new UpdateStudentRequest
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email
        };

        // Assert
        Assert.Equal(firstName, request.FirstName);
        Assert.Equal(lastName, request.LastName);
        Assert.Equal(email, request.Email);
    }
}
