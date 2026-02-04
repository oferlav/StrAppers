using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace StudentTeamBuilderService;

public class Program
{
    public static void Main(string[] args)
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(logDir, "StudentTeamBuilderService-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true)
            .CreateLogger();

        var builder = Host.CreateApplicationBuilder(args);
        
        // Determine which config file to use based on command-line argument
        // Dev: appsettings.json only. Prod: appsettings.Prod.json only.
        string configFile = "appsettings.json"; // Default to Dev (base appsettings)
        
        if (args.Length > 0 && args[0].Equals("Prod", StringComparison.OrdinalIgnoreCase))
        {
            configFile = "appsettings.Prod.json";
        }
        else if (args.Length > 0 && args[0].Equals("Dev", StringComparison.OrdinalIgnoreCase))
        {
            configFile = "appsettings.json";
        }
        else
        {
            // If no argument, try to auto-detect from executable path or default to Dev
            var exePath = AppContext.BaseDirectory;
            if (exePath.Contains("publish-prod", StringComparison.OrdinalIgnoreCase))
            {
                configFile = "appsettings.Prod.json";
            }
            else
            {
                configFile = "appsettings.json";
            }
        }
        
        // Load only the specific config file (no environment variable dependency)
        var configPath = Path.Combine(builder.Environment.ContentRootPath, configFile);
        builder.Configuration.Sources.Clear();
        builder.Configuration
            .SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile(configFile, optional: false, reloadOnChange: true)
            .AddCommandLine(args);
        
        // Store config file path for logging
        builder.Configuration["ConfigFilePath"] = configPath;
        
        builder.Logging.ClearProviders();
        builder.Logging.AddEventLog();
        builder.Services.AddSerilog(Log.Logger, dispose: true);
        builder.Services.AddHostedService<Worker>();
        builder.Services.AddHttpClient();

        builder.Services.Configure<KickoffConfig>(builder.Configuration.GetSection("KickoffConfig"));
        builder.Services.Configure<ProjectCriteriaConfig>(builder.Configuration.GetSection("ProjectCriteriaConfig"));

        // Enable Windows Service
        var serviceNameFinal = builder.Configuration.GetValue<string>("Service:Name") ?? "StrAppers Student Team Builder";
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = serviceNameFinal;
        });

        try
        {
            var host = builder.Build();
            host.Run();
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

public class KickoffConfig
{
    public int MinimumStudents { get; set; } = 2;
    public bool RequireAdmin { get; set; } = true;
    public bool RequireUIUXDesigner { get; set; } = true;
    public bool RequireProductManager { get; set; } = false;
    public bool RequireDeveloperRule { get; set; } = true;
    public int MaxPendingTime { get; set; } = 96;
}

public class ProjectCriteriaConfig
{
    public double PopularProjectsRate { get; set; } = 0.2;
    public int NewProjectsMaxDays { get; set; } = 30;
}

