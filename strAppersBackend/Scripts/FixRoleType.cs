using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Scripts;

/// <summary>
/// Console application to fix Role.Type column NULL values
/// Run this with: dotnet run --project Scripts/FixRoleType.cs
/// </summary>
public class FixRoleType
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🔧 Fixing Role.Type column NULL values...");
        
        // Create DbContext options (you may need to adjust the connection string)
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=StrAppersDB;Username=postgres;Password=your_password");
        
        using var context = new ApplicationDbContext(optionsBuilder.Options);
        
        try
        {
            // Get all roles with NULL Type values
            var rolesWithNullType = await context.Roles
                .Where(r => r.Type == 0) // Assuming 0 means NULL in this context
                .ToListAsync();
            
            Console.WriteLine($"Found {rolesWithNullType.Count} roles with NULL Type values");
            
            if (rolesWithNullType.Count == 0)
            {
                Console.WriteLine("✅ All roles already have Type values!");
                return;
            }
            
            // Update roles based on their names
            foreach (var role in rolesWithNullType)
            {
                int newType = role.Name.ToLower() switch
                {
                    var name when name.Contains("project manager") => 4, // Leadership
                    var name when name.Contains("frontend developer") => 1, // Developer
                    var name when name.Contains("backend developer") => 1, // Developer
                    var name when name.Contains("full stack developer") => 1, // Developer
                    var name when name.Contains("ui/ux designer") => 3, // UI/UX Designer
                    var name when name.Contains("quality assurance") => 2, // Junior Developer
                    var name when name.Contains("team lead") => 4, // Leadership
                    var name when name.Contains("research assistant") => 2, // Junior Developer
                    var name when name.Contains("documentation specialist") => 2, // Junior Developer
                    _ => 2 // Default to Junior Developer
                };
                
                role.Type = newType;
                Console.WriteLine($"Updated {role.Name} -> Type {newType}");
            }
            
            // Save changes
            await context.SaveChangesAsync();
            
            Console.WriteLine("✅ Successfully updated all roles with Type values!");
            
            // Verify the update
            var updatedRoles = await context.Roles
                .Select(r => new { r.Id, r.Name, r.Type })
                .OrderBy(r => r.Id)
                .ToListAsync();
            
            Console.WriteLine("\n📋 Updated roles:");
            foreach (var role in updatedRoles)
            {
                Console.WriteLine($"  ID: {role.Id}, Name: {role.Name}, Type: {role.Type}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine("Please check your database connection and try again.");
        }
    }
}


