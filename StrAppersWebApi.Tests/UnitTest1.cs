using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using strAppersBackend.Models;
using strAppersBackend;

namespace StrAppersWebApi.Tests;

public class WeatherForecastControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public WeatherForecastControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetWeatherForecast_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/WeatherForecast");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWeatherForecast_ReturnsValidJson()
    {
        // Act
        var response = await _client.GetAsync("/WeatherForecast");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotEmpty(content);
        
        // Verify it's valid JSON
        var forecasts = JsonSerializer.Deserialize<WeatherForecast[]>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        Assert.NotNull(forecasts);
        Assert.Equal(5, forecasts.Length);
    }

    [Fact]
    public async Task GetWeatherForecast_ReturnsForecastsWithValidData()
    {
        // Act
        var response = await _client.GetAsync("/WeatherForecast");
        var forecasts = await response.Content.ReadFromJsonAsync<WeatherForecast[]>();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotNull(forecasts);
        Assert.Equal(5, forecasts.Length);

        foreach (var forecast in forecasts)
        {
            Assert.NotNull(forecast.Summary);
            Assert.NotEmpty(forecast.Summary);
            Assert.True(forecast.TemperatureC >= -20 && forecast.TemperatureC <= 55);
            Assert.True(forecast.TemperatureF > 0); // Should be positive for reasonable temperatures
        }
    }
}