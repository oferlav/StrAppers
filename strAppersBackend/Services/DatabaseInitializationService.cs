using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;

namespace strAppersBackend.Services;

public class DatabaseInitializationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseInitializationService> _logger;

    public DatabaseInitializationService(ApplicationDbContext context, ILogger<DatabaseInitializationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Ensure database is created
            await _context.Database.EnsureCreatedAsync();
            _logger.LogInformation("Database ensured created successfully");

            // Check if we have any students (to avoid re-seeding)
            var studentCount = await _context.Students.CountAsync();
            if (studentCount == 0)
            {
                _logger.LogInformation("No students found, database appears to be empty");
            }
            else
            {
                _logger.LogInformation("Found {StudentCount} students in database", studentCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing database");
            throw;
        }
    }
}
