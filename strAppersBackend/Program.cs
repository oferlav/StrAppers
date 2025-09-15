using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReadCommentHandling = JsonCommentHandling.Skip;
        options.JsonSerializerOptions.AllowTrailingCommas = true;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.WriteIndented = true;
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add database initialization service
builder.Services.AddScoped<DatabaseInitializationService>();
builder.Services.AddScoped<SlackService>();

// Add AI services
builder.Services.AddScoped<IAIService, AIService>();
builder.Services.AddScoped<IDesignDocumentService, DesignDocumentService>();

// Add HttpClientFactory for Slack API calls and OpenAI API calls
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Enable Swagger in production for IIS
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Disable HTTPS redirection completely for IIS
// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Optional: Initialize database on startup (uncomment if needed)
// using (var scope = app.Services.CreateScope())
// {
//     var dbInitService = scope.ServiceProvider.GetRequiredService<DatabaseInitializationService>();
//     await dbInitService.InitializeAsync();
// }

app.Run();

// Make Program class accessible for testing
public partial class Program { }