using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Add CORS service
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "AllowFrontend",
                      policy =>
                      {
                          // Allow requests from the specific frontend domain
                          policy.WithOrigins("https://preview--skill-in-ce9dcf39.base44.app")
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                      });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReadCommentHandling = JsonCommentHandling.Skip;
        options.JsonSerializerOptions.AllowTrailingCommas = true;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add database initialization service
// builder.Services.AddScoped<DatabaseInitializationService>();
// builder.Services.AddScoped<SlackService>(); // SLACK TEMPORARILY DISABLED

// Add AI services
builder.Services.AddScoped<IAIService, AIService>();
builder.Services.AddScoped<IDesignDocumentService, DesignDocumentService>();

// Add Trello services
builder.Services.AddScoped<ITrelloService, TrelloService>();

// Add Microsoft Graph services
builder.Services.AddScoped<IMicrosoftGraphService, MicrosoftGraphService>();

// Add Google Workspace services
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();
builder.Services.AddScoped<IGmailService, GmailService>();

// Add SMTP email service
builder.Services.AddScoped<ISmtpEmailService, SmtpEmailService>();

// Add GitHub validation service
builder.Services.AddScoped<IGitHubService, GitHubService>();

// Add session support for GitHub OAuth
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Configure Trello settings
builder.Services.Configure<TrelloConfig>(builder.Configuration.GetSection("Trello"));

// Configure Business Logic settings
builder.Services.Configure<BusinessLogicConfig>(builder.Configuration.GetSection("BusinessLogicConfig"));

// Configure Google Workspace settings
builder.Services.Configure<GoogleWorkspaceConfig>(builder.Configuration.GetSection("GoogleWorkspace"));

// Configure SMTP settings
builder.Services.Configure<SmtpConfig>(builder.Configuration.GetSection("Smtp"));

// Add HttpClientFactory for Slack API calls, OpenAI API calls, Trello API calls, and Microsoft Graph API calls
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

// Apply CORS middleware
app.UseCors("AllowFrontend");

app.UseAuthorization();
app.UseSession();

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