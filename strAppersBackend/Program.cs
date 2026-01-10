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
                                            "https://localhost:9001",
                                            "http://localhost:5000", // Backend localhost
                                            "https://localhost:5000", // Backend localhost HTTPS
                                            "http://20.126.90.3:9001", // Production frontend IP
                                            "https://20.126.90.3:9001") // Production frontend IP HTTPS
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials(); // Allow credentials for CORS requests
                      });
    
    // Add a more permissive policy for development/testing
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy(name: "AllowAll",
                          policy =>
                          {
                              policy.AllowAnyOrigin()
                                    .AllowAnyHeader()
                                    .AllowAnyMethod();
                          });
    }
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

// Add GitHub validation service with HttpClient configuration
// Configure HttpClient for GitHubService with SSL certificate validation handler
// This handles certificate chain validation issues that can occur after VM restarts
builder.Services.AddHttpClient<GitHubService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    
    // Handle certificate chain validation issues that can occur after VM restarts
    // when intermediate certificates might not be fully loaded in the certificate store
    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
    {
        // If no errors, accept the certificate
        if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
        {
            return true;
        }
        
        // For GitHub API, we can be more lenient with chain errors after VM restart
        // This is safe because we're connecting to api.github.com (known trusted domain)
        if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors)
        {
            // Check if we're connecting to GitHub
            if (message.RequestUri != null && 
                (message.RequestUri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase) || 
                 message.RequestUri.Host.Contains("api.github.com", StringComparison.OrdinalIgnoreCase)))
            {
                // Accept chain errors for GitHub - this is often due to intermediate certs
                // not being in the store immediately after VM restart
                return true;
            }
        }
        
        // Reject other errors (name mismatches, etc.)
        return false;
    };
    
    return handler;
});

// Register GitHubService interface (AddHttpClient already registered GitHubService with configured HttpClient)
builder.Services.AddScoped<IGitHubService>(sp => sp.GetRequiredService<GitHubService>());

// Configure HttpClient for DeploymentController with SSL certificate validation handler
// This handles certificate chain validation issues when connecting to GitHub API
builder.Services.AddHttpClient("DeploymentController", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    
    // Handle certificate chain validation issues that can occur after VM restarts
    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
    {
        // If no errors, accept the certificate
        if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
        {
            return true;
        }
        
        // For GitHub API, we can be more lenient with chain errors after VM restart
        if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors)
        {
            // Check if we're connecting to GitHub
            if (message.RequestUri != null && 
                (message.RequestUri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase) || 
                 message.RequestUri.Host.Contains("api.github.com", StringComparison.OrdinalIgnoreCase)))
            {
                // Accept chain errors for GitHub - this is often due to intermediate certs
                // not being in the store immediately after VM restart
                return true;
            }
        }
        
        // Reject other errors (name mismatches, etc.)
        return false;
    };
    
    return handler;
});

// Add Mentor Intent Detection service
builder.Services.AddScoped<IMentorIntentService, MentorIntentService>();

// Add Code Review Agent service
builder.Services.AddScoped<ICodeReviewAgent, CodeReviewAgent>();

// Add Mentor Intent Detection service
builder.Services.AddScoped<IMentorIntentService, MentorIntentService>();

// Add Code Review Agent service
builder.Services.AddScoped<ICodeReviewAgent, CodeReviewAgent>();

// Add Kickoff service
builder.Services.AddScoped<IKickoffService, KickoffService>();

// Add Password Hasher service
builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();

// Add Affinda service
builder.Services.AddScoped<IAffindaService, AffindaService>();

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
builder.Services.AddHttpClient<AffindaService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // Resume parsing can take time
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

// Apply CORS middleware - use more permissive policy in development
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
}
else
{
    app.UseCors("AllowFrontend");
}

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