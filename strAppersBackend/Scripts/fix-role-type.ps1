# PowerShell script to fix Role.Type column NULL values
# This script connects to the PostgreSQL database and runs the SQL fix

param(
    [string]$ConnectionString = "Host=localhost;Database=StrAppersDB;Username=postgres;Password=your_password"
)

Write-Host "Fixing Role.Type column NULL values..." -ForegroundColor Yellow

# Read the SQL script
$sqlScript = Get-Content -Path "Database/fix_role_type_column.sql" -Raw

try {
    # Connect to PostgreSQL and execute the script
    $env:PGPASSWORD = "your_password"  # Set your PostgreSQL password here
    
    # Use psql to execute the SQL script
    psql -h localhost -U postgres -d StrAppersDB -c $sqlScript
    
    Write-Host "✅ Role.Type column fixed successfully!" -ForegroundColor Green
    Write-Host "All existing roles now have proper Type values." -ForegroundColor Green
}
catch {
    Write-Host "❌ Error fixing Role.Type column: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please run the SQL script manually in your PostgreSQL client." -ForegroundColor Yellow
    Write-Host "SQL Script location: Database/fix_role_type_column.sql" -ForegroundColor Yellow
}


