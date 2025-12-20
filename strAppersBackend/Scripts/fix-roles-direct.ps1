# Fix Roles Data on Production Database - Direct SQL Execution
# This bypasses migrations and runs SQL directly

param(
    [Parameter(Mandatory=$true)]
    [string]$ConnectionString
)

Write-Host "[START] Fixing Roles data on Production (Direct SQL)..." -ForegroundColor Green
Write-Host ""

# Change to script directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptPath ".."
Set-Location $projectPath

try {
    Write-Host "Step 1: Parsing connection string..." -ForegroundColor Yellow
    
    # Parse connection string
    $host = ""
    $database = ""
    $username = ""
    $password = ""
    
    $ConnectionString -split ';' | ForEach-Object {
        $pair = $_ -split '=', 2
        if ($pair.Length -eq 2) {
            $key = $pair[0].Trim()
            $value = $pair[1].Trim()
            switch ($key) {
                "Host" { $host = $value }
                "Database" { $database = $value }
                "Username" { $username = $value }
                "Password" { $password = $value }
            }
        }
    }
    
    if (-not $host -or -not $database -or -not $username) {
        throw "Invalid connection string format. Expected: Host=...;Database=...;Username=...;Password=..."
    }
    
    Write-Host "  Host: $host" -ForegroundColor Cyan
    Write-Host "  Database: $database" -ForegroundColor Cyan
    Write-Host "  Username: $username" -ForegroundColor Cyan
    
    Write-Host ""
    Write-Host "Step 2: Creating SQL file..." -ForegroundColor Yellow
    
    # SQL to sync Roles data
    $sql = @"
INSERT INTO "Roles" ("Id", "Name", "Description", "Category", "Type", "IsActive", "CreatedAt", "UpdatedAt")
VALUES 
    (1, 'Product Manager', 'Leads product planning and execution', 'Leadership', 0, true, TIMESTAMP '2025-08-04 20:54:12.981445+03', NULL),
    (2, 'Frontend Developer', 'Develops user interface and user experience', 'Technical', 2, true, TIMESTAMP '2025-08-04 20:54:12.981445+03', NULL),
    (3, 'Backend Developer', 'Develops server-side logic and database integration', 'Technical', 2, true, TIMESTAMP '2025-08-04 20:54:12.981445+03', NULL),
    (4, 'UI/UX Designer', 'Designs user interface and user experience', 'Technical', 3, true, TIMESTAMP '2025-08-04 20:54:12.981446+03', NULL),
    (5, 'Quality Assurance', 'Tests software and ensures quality standards', 'Technical', 0, false, TIMESTAMP '2025-08-04 20:54:12.981446+03', NULL),
    (6, 'Full Stack Developer', 'Develop backend + UI', 'Leadership', 1, true, TIMESTAMP '2025-08-04 20:54:12.981446+03', NULL),
    (7, 'Marketing', 'Conducts research and Market analysis. Responsible for Media', 'Academic', 0, true, TIMESTAMP '2025-08-04 20:54:12.981446+03', NULL),
    (8, 'Documentation Specialist', 'Creates and maintains project documentation', 'Administrative', 0, false, TIMESTAMP '2025-08-04 20:54:12.981447+03', NULL)
ON CONFLICT ("Id") DO UPDATE SET
    "Name" = EXCLUDED."Name",
    "Description" = EXCLUDED."Description",
    "Category" = EXCLUDED."Category",
    "Type" = EXCLUDED."Type",
    "IsActive" = EXCLUDED."IsActive",
    "CreatedAt" = EXCLUDED."CreatedAt",
    "UpdatedAt" = EXCLUDED."UpdatedAt";
"@
    
    $sqlFile = Join-Path $scriptPath "fix-roles-temp.sql"
    $sql | Out-File -FilePath $sqlFile -Encoding UTF8 -NoNewline
    
    Write-Host "  SQL file created: $sqlFile" -ForegroundColor Cyan
    
    Write-Host ""
    Write-Host "Step 3: Executing SQL using psql..." -ForegroundColor Yellow
    Write-Host "  (If psql is not found, you'll need to install PostgreSQL client tools)" -ForegroundColor Yellow
    Write-Host ""
    
    # Set PGPASSWORD environment variable
    $env:PGPASSWORD = $password
    
    # Run psql command
    $psqlPath = "psql"
    
    # Try to find psql in common locations
    $commonPaths = @(
        "C:\Program Files\PostgreSQL\*\bin\psql.exe",
        "C:\Program Files (x86)\PostgreSQL\*\bin\psql.exe"
    )
    
    foreach ($path in $commonPaths) {
        $found = Get-ChildItem -Path $path -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) {
            $psqlPath = $found.FullName
            break
        }
    }
    
    Write-Host "  Using: $psqlPath" -ForegroundColor Cyan
    Write-Host ""
    
    $psqlArgs = @(
        "-h", $host,
        "-d", $database,
        "-U", $username,
        "-f", $sqlFile
    )
    
    & $psqlPath $psqlArgs
    
    $exitCode = $LASTEXITCODE
    
    # Clean up
    Remove-Item -Path $sqlFile -Force -ErrorAction SilentlyContinue
    $env:PGPASSWORD = $null
    
    if ($exitCode -ne 0) {
        throw "SQL execution failed with exit code $exitCode"
    }
    
    Write-Host ""
    Write-Host "[OK] Successfully synced Roles data on production!" -ForegroundColor Green
    Write-Host "Production Roles now match dev database exactly." -ForegroundColor Cyan
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "[ERROR] Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    
    if ($_.Exception.Message -like "*psql*" -or $_.Exception.Message -like "*not found*") {
        Write-Host "⚠️  psql not found. Alternative options:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Option 1: Install PostgreSQL client tools" -ForegroundColor Cyan
        Write-Host "   Download from: https://www.postgresql.org/download/windows/" -ForegroundColor White
        Write-Host ""
        Write-Host "Option 2: Use the SQL file directly" -ForegroundColor Cyan
        Write-Host "   File: Scripts\fix-roles.sql" -ForegroundColor White
        Write-Host "   Run manually in your database tool (pgAdmin, DBeaver, etc.)" -ForegroundColor White
        Write-Host ""
        Write-Host "Option 3: Use C# DbContext (requires building project)" -ForegroundColor Cyan
        Write-Host "   See: Scripts\FixRolesProd.cs" -ForegroundColor White
        Write-Host ""
    }
    
    $env:PGPASSWORD = $null
    exit 1
}




