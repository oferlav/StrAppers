using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using strAppersBackend.Logging;
using strAppersBackend.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Large multipart uploads (resource file attachments)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 30 * 1024 * 1024;
});

var apiBaseForCors = builder.Configuration["ApiBaseUrl"];
Uri? apiBaseUriForCors = null;
if (!string.IsNullOrWhiteSpace(apiBaseForCors) && Uri.TryCreate(apiBaseForCors.Trim(), UriKind.Absolute, out var tmpApiUri))
{
    apiBaseUriForCors = tmpApiUri;
}

// Azure App Service: wire ILogger to filesystem diagnostics (portal Log stream, LogFiles\Application).
// WEBSITE_INSTANCE_ID is set on App Service workers; skip locally.
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")))
{
    builder.Logging.AddAzureWebAppDiagnostics();
}

// Add services to the container.

// Add CORS service
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "AllowFrontend",
                      policy =>
                      {
                          // Allow requests from GitHub Pages (*.github.io), specific frontend domains, and localhost
                          policy.SetIsOriginAllowed(origin =>
                          {
                              if (string.IsNullOrEmpty(origin)) return false;
                              
                              var uri = new Uri(origin);

                              // Same host as ApiBaseUrl (e.g. Swagger UI / fetch from the App Service URL)
                              if (apiBaseUriForCors != null &&
                                  uri.Host.Equals(apiBaseUriForCors.Host, StringComparison.OrdinalIgnoreCase))
                                  return true;
                              
                              // Allow GitHub Pages (*.github.io) - for generated frontend projects
                              if (uri.Host.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase))
                                  return true;

                              // Azure Static Web Apps (e.g. *.4.azurestaticapps.net)
                              if (uri.Host.EndsWith(".azurestaticapps.net", StringComparison.OrdinalIgnoreCase))
                                  return true;
                              
                              // Allow specific frontend domains
                              var allowedOrigins = new[]
                              {
                                  "preview--skill-in-ce9dcf39.base44.app",
                                  "skill-in.com",
                                  "localhost",
                                  "127.0.0.1",
                                  "20.126.90.3"
                              };
                              
                              if (allowedOrigins.Any(allowed => uri.Host.Equals(allowed, StringComparison.OrdinalIgnoreCase) || 
                                                               uri.Host.EndsWith($".{allowed}", StringComparison.OrdinalIgnoreCase)))
                                  return true;
                              
                              return false;
                          })
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
builder.Services.AddSwaggerGen(c =>
{
    c.SchemaFilter<GapAnalysisRequestSchemaFilter>();
});

// Add Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add database initialization service
// builder.Services.AddScoped<DatabaseInitializationService>();
// builder.Services.AddScoped<SlackService>(); // SLACK TEMPORARILY DISABLED

// Add AI services
builder.Services.AddScoped<IAIService, AIService>();
builder.Services.AddScoped<IDesignDocumentService, DesignDocumentService>();
builder.Services.AddScoped<IChatCompletionService, ChatCompletionService>();

// Add Trello services
builder.Services.AddScoped<ITrelloService, TrelloService>();
builder.Services.AddScoped<ITrelloSprintMergeService, TrelloSprintMergeService>();
builder.Services.AddScoped<IStudentTeamBuilderService, StudentTeamBuilderService>();

// Add Microsoft Graph services
builder.Services.AddScoped<IMicrosoftGraphService, MicrosoftGraphService>();

// Add Google Workspace services
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();
builder.Services.AddScoped<IGmailService, GmailService>();

// Add SMTP email service
builder.Services.AddScoped<ISmtpEmailService, SmtpEmailService>();

// Azure Blob Storage (optional — uploads return 503 if AzureStorage:ConnectionString is unset)
builder.Services.Configure<AzureBlobStorageOptions>(
    builder.Configuration.GetSection(AzureBlobStorageOptions.SectionName));
builder.Services.AddSingleton<IAzureBlobStorageService, AzureBlobStorageService>();

// Shared SSL callback: accept all certificate errors for GitHub (UntrustedRoot, corporate proxy, VM restart)
// so GitHub API calls work reliably in restricted networks
static bool GitHubSslValidation(HttpRequestMessage message, System.Security.Cryptography.X509Certificates.X509Certificate2? cert,
    System.Security.Cryptography.X509Certificates.X509Chain? chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
{
    if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None) return true;
    if (message.RequestUri != null &&
        (message.RequestUri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase) ||
         message.RequestUri.Host.Contains("api.github.com", StringComparison.OrdinalIgnoreCase)))
        return true; // Accept UntrustedRoot / corporate proxy / chain errors for GitHub
    return false;
}

// Add GitHub validation service with HttpClient configuration
builder.Services.AddHttpClient<GitHubService>(client => client.Timeout = TimeSpan.FromMinutes(5))
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = GitHubSslValidation;
        return handler;
    });

// Register GitHubService interface (AddHttpClient already registered GitHubService with configured HttpClient)
builder.Services.AddScoped<IGitHubService>(sp => sp.GetRequiredService<GitHubService>());

// Named client "GitHub" for MentorController and others that call GitHub API directly (same SSL handling)
builder.Services.AddHttpClient("GitHub", client => client.Timeout = TimeSpan.FromMinutes(5))
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = GitHubSslValidation;
        return handler;
    });

// Configure HttpClient for DeploymentController with same SSL handling for GitHub
builder.Services.AddHttpClient("DeploymentController", client => client.Timeout = TimeSpan.FromMinutes(5))
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = GitHubSslValidation;
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

// Google OAuth (login) — token + userinfo HTTP calls
builder.Services.AddHttpClient("GoogleOAuth", client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
});

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

// Configure Deployments settings
builder.Services.Configure<DeploymentsConfig>(builder.Configuration.GetSection("DeploymentsConfig"));
builder.Services.Configure<TestingConfig>(builder.Configuration.GetSection("Testing"));

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

var suppressBoardChatPollHostingDiagnostics = builder.Configuration.GetValue("Logging:SuppressBoardChatPollHostingDiagnostics", true);

if (disableGetChatLogs)
{
    // Framework noise reduction when GetChat polls (controller logs already gated in BoardsController).
    // Hosting "Request starting/finished" for GET /api/Boards/use/chat is stripped by BoardChatPollHostingLogSuppression instead of silencing all routes.
    if (!suppressBoardChatPollHostingDiagnostics)
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

    builder.Logging.AddFilter("Microsoft.AspNetCore.Routing", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.Warning);
}

if (suppressBoardChatPollHostingDiagnostics)
    BoardChatPollHostingLogSuppression.WrapLoggerProviders(builder.Services);

// Default HttpClient handlers log every outbound request URI at Information — Trello (and others) put key/token in the query string.
if (!builder.Configuration.GetValue("Logging:LogHttpClientOutboundUrls", false))
    builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

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

// Add global exception handler middleware
// This will catch unhandled exceptions and send them to the runtime error endpoint
app.UseMiddleware<strAppersBackend.Middleware.GlobalExceptionHandlerMiddleware>();

// Note: GetChat log suppression is now handled via log level configuration in appsettings.json
// The middleware approach didn't work reliably because log filters don't have access to HttpContext at runtime

app.UseAuthorization();
app.UseSession();

// Serve email assets (e.g. logo.png) at /assets/* for fallback logo URL in emails
var assetsPath = Path.Combine(app.Environment.ContentRootPath, "Assets");
if (System.IO.Directory.Exists(assetsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(assetsPath),
        RequestPath = "/assets"
    });
}

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