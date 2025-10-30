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
        builder.Logging.ClearProviders();
        builder.Logging.AddEventLog();
        builder.Services.AddSerilog(Log.Logger, dispose: true);
        builder.Services.AddHostedService<Worker>();
        builder.Services.AddHttpClient();

        builder.Services.Configure<KickoffConfig>(builder.Configuration.GetSection("KickoffConfig"));

        // Enable Windows Service
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "StrAppers Student Team Builder";
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
    public bool RequireDeveloperRule { get; set; } = true;
    public int MaxPendingTime { get; set; } = 96;
}

