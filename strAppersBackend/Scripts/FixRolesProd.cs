using Microsoft.EntityFrameworkCore;
using Npgsql;
using strAppersBackend.Data;

namespace strAppersBackend.Scripts;

/// <summary>
/// Console application to fix Roles data on production to match dev
/// Run this with: dotnet run --project Scripts/FixRolesProd.cs
/// Or: dotnet Scripts/FixRolesProd.cs --connection "Host=...;Database=...;Username=...;Password=..."
/// </summary>
public class FixRolesProd
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🔧 Fixing Roles data on Production to match Dev...");
        Console.WriteLine("");
        
        // Get connection string from command line argument or environment variable
        string? connectionString = null;
        
        if (args.Length > 0 && args[0].StartsWith("--connection"))
        {
            if (args[0].Contains("="))
            {
                connectionString = args[0].Split('=', 2)[1];
            }
            else if (args.Length > 1)
            {
                connectionString = args[1];
            }
        }
        
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        }
        
        if (string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("❌ ERROR: Connection string not provided!");
            Console.WriteLine("");
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project Scripts/FixRolesProd.cs -- --connection \"Host=...;Database=...;Username=...;Password=...\"");
            Console.WriteLine("");
            Console.WriteLine("Or set environment variable:");
            Console.WriteLine("  $env:ConnectionStrings__DefaultConnection = \"Host=...;Database=...;Username=...;Password=...\"");
            Console.WriteLine("  dotnet run --project Scripts/FixRolesProd.cs");
            Environment.Exit(1);
            return;
        }
        
        try
        {
            // Create DbContext options
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);
            
            using var context = new ApplicationDbContext(optionsBuilder.Options);
            
            Console.WriteLine("✓ Connected to database");
            Console.WriteLine("");
            Console.WriteLine("Running SQL to sync Roles data...");
            Console.WriteLine("");
            
            // SQL to sync Roles data
            var sql = @"
INSERT INTO ""Roles"" (""Id"", ""Name"", ""Description"", ""Category"", ""Type"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
VALUES 
    (1, 'Product Manager', 'Leads product planning and execution', 'Leadership', 0, true, TIMESTAMP '2025-08-04 20:54:12.981445+03', NULL),
    (2, 'Frontend Developer', 'Develops user interface and user experience', 'Technical', 2, true, TIMESTAMP '2025-08-04 20:54:12.981445+03', NULL),
    (3, 'Backend Developer', 'Develops server-side logic and database integration', 'Technical', 2, true, TIMESTAMP '2025-08-04 20:54:12.981445+03', NULL),
    (4, 'UI/UX Designer', 'Designs user interface and user experience', 'Technical', 3, true, TIMESTAMP '2025-08-04 20:54:12.981446+03', NULL),
    (5, 'Quality Assurance', 'Tests software and ensures quality standards', 'Technical', 0, false, TIMESTAMP '2025-08-04 20:54:12.981446+03', NULL),
    (6, 'Full Stack Developer', 'Develop backend + UI', 'Leadership', 1, true, TIMESTAMP '2025-08-04 20:54:12.981446+03', NULL),
    (7, 'Marketing', 'Conducts research and Market analysis. Responsible for Media', 'Academic', 0, true, TIMESTAMP '2025-08-04 20:54:12.981446+03', NULL),
    (8, 'Documentation Specialist', 'Creates and maintains project documentation', 'Administrative', 0, false, TIMESTAMP '2025-08-04 20:54:12.981447+03', NULL)
ON CONFLICT (""Id"") DO UPDATE SET
    ""Name"" = EXCLUDED.""Name"",
    ""Description"" = EXCLUDED.""Description"",
    ""Category"" = EXCLUDED.""Category"",
    ""Type"" = EXCLUDED.""Type"",
    ""IsActive"" = EXCLUDED.""IsActive"",
    ""CreatedAt"" = EXCLUDED.""CreatedAt"",
    ""UpdatedAt"" = EXCLUDED.""UpdatedAt"";
";
            
            // Execute SQL directly
            await context.Database.ExecuteSqlRawAsync(sql);
            
            Console.WriteLine("✅ Successfully synced Roles data!");
            Console.WriteLine("");
            Console.WriteLine("Production Roles now match dev database exactly:");
            Console.WriteLine("  1. Product Manager (Leadership, Type 0)");
            Console.WriteLine("  2. Frontend Developer (Technical, Type 2)");
            Console.WriteLine("  3. Backend Developer (Technical, Type 2)");
            Console.WriteLine("  4. UI/UX Designer (Technical, Type 3)");
            Console.WriteLine("  5. Quality Assurance (Technical, Type 0, IsActive: false)");
            Console.WriteLine("  6. Full Stack Developer (Leadership, Type 1)");
            Console.WriteLine("  7. Marketing (Academic, Type 0)");
            Console.WriteLine("  8. Documentation Specialist (Administrative, Type 0, IsActive: false)");
            Console.WriteLine("");
        }
        catch (Exception ex)
        {
            Console.WriteLine("");
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine("");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                Console.WriteLine("");
            }
            Environment.Exit(1);
        }
    }
}




