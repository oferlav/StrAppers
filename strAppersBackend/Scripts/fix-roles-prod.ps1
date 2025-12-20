# Fix Roles Data on Production Database
# This script uses a C# console app to sync Roles data (bypasses migrations)

param(
    [Parameter(Mandatory=$true)]
    [string]$ConnectionString
)

Write-Host "[START] Fixing Roles data on Production..." -ForegroundColor Green
Write-Host ""

# Change to script directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptPath ".."
Set-Location $projectPath

try {
    Write-Host "Step 1: Setting connection string..." -ForegroundColor Yellow
    $env:ConnectionStrings__DefaultConnection = $ConnectionString
    
    Write-Host ""
    Write-Host "Step 2: Running C# script to sync Roles data..." -ForegroundColor Yellow
    
    # Compile and run the C# script
    dotnet run --project strAppersBackend.csproj -- --connection $ConnectionString --no-build 2>&1 | ForEach-Object {
        if ($_ -match "ERROR|Error|error|Exception|Failed|failed") {
            Write-Host $_ -ForegroundColor Red
        } else {
            Write-Host $_
        }
    }
    
    # Alternative: Use the FixRolesProd.cs directly
    # First, let's try a simpler approach - compile and run inline
    Write-Host ""
    Write-Host "Running SQL directly via DbContext..." -ForegroundColor Yellow
    
    # Create a temporary C# file that we can compile and run
    $tempScript = @"
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? "$ConnectionString";
var optionsBuilder = new DbContextOptionsBuilder<strAppersBackend.Data.ApplicationDbContext>();
optionsBuilder.UseNpgsql(connectionString);

using var context = new strAppersBackend.Data.ApplicationDbContext(optionsBuilder.Options);

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
"";

await context.Database.ExecuteSqlRawAsync(sql);
Console.WriteLine("✅ Successfully synced Roles data!");
"@

    # Use dotnet-script if available, otherwise provide manual instructions
    Write-Host ""
    Write-Host "⚠️  Note: This script requires running the C# code directly." -ForegroundColor Yellow
    Write-Host "   Please use one of these options:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Option 1: Run the C# script directly:" -ForegroundColor Cyan
    Write-Host "   cd strAppersBackend" -ForegroundColor White
    Write-Host "   `$env:ConnectionStrings__DefaultConnection = `"$ConnectionString`"" -ForegroundColor White
    Write-Host "   dotnet run --project Scripts/FixRolesProd.cs" -ForegroundColor White
    Write-Host ""
    Write-Host "Option 2: Use the SQL file (fix-roles.sql) with psql:" -ForegroundColor Cyan
    Write-Host "   psql -h YOUR_HOST -d YOUR_DB -U YOUR_USER -f Scripts/fix-roles.sql" -ForegroundColor White
    Write-Host ""
    
    exit 0
}
catch {
    Write-Host ""
    Write-Host "[ERROR] Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    exit 1
}

