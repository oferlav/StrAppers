using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Add CORS service
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "AllowFrontend",
                      policy =>
                      {
                          // Allow requests from the specific frontend domain and localhost for development
                          policy.WithOrigins("https://preview--skill-in-ce9dcf39.base44.app", 
                                            "http://localhost:9001",
                                            "https://localhost:9001")
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
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
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

// Add Kickoff service
builder.Services.AddScoped<IKickoffService, KickoffService>();

// Add session support for GitHub OAuth
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add HttpContextAccessor for logging filters
builder.Services.AddHttpContextAccessor();

// Configure Trello settings
builder.Services.Configure<TrelloConfig>(builder.Configuration.GetSection("Trello"));

// Configure Business Logic settings
builder.Services.Configure<BusinessLogicConfig>(builder.Configuration.GetSection("BusinessLogicConfig"));

// Configure Google Workspace settings
builder.Services.Configure<GoogleWorkspaceConfig>(builder.Configuration.GetSection("GoogleWorkspace"));

// Configure SMTP settings
builder.Services.Configure<SmtpConfig>(builder.Configuration.GetSection("Smtp"));

// Configure Kickoff settings
builder.Services.Configure<KickoffConfig>(builder.Configuration.GetSection("KickoffConfig"));

// Configure Engagement Rules settings
builder.Services.Configure<EngagementRulesConfig>(builder.Configuration.GetSection("EngagementRules"));

// Configure Prompt settings
builder.Services.Configure<PromptConfig>(builder.Configuration.GetSection("PromptConfig"));

// Configure SystemDesign AI Agent settings
builder.Services.Configure<SystemDesignAIAgentConfig>(builder.Configuration.GetSection("SystemDesignAIAgent"));

// Configure AI settings
builder.Services.Configure<AIConfig>(builder.Configuration.GetSection("AIConfig"));

// Add HttpClientFactory for Slack API calls, OpenAI API calls, Trello API calls, and Microsoft Graph API calls
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<AIService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10); // Increase timeout to 10 minutes for AI calls
});

// Configure GetChat log suppression
var disableGetChatLogs = builder.Configuration.GetValue<bool>("Logging:DisableGetChatLogs", true);

if (disableGetChatLogs)
{
    // Suppress Info/Warning logs for framework categories when GetChat logs are disabled
    // This is done via appsettings.json, but we also set it here programmatically
    builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.AspNetCore.Routing", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.Warning);
}

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

// Note: GetChat log suppression is now handled via log level configuration in appsettings.json
// The middleware approach didn't work reliably because log filters don't have access to HttpContext at runtime

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