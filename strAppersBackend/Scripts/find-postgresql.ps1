# Find PostgreSQL Installation Script
# This script helps locate PostgreSQL installation on your system

Write-Host "Searching for PostgreSQL installation..." -ForegroundColor Green
Write-Host ""

$found = $false

# Check PATH
Write-Host "Checking PATH environment variable..." -ForegroundColor Yellow
$pgDumpInPath = Get-Command pg_dump -ErrorAction SilentlyContinue
if ($pgDumpInPath) {
    Write-Host "  [FOUND] pg_dump is in PATH: $($pgDumpInPath.Source)" -ForegroundColor Green
    $found = $true
} else {
    Write-Host "  [NOT FOUND] pg_dump not in PATH" -ForegroundColor Red
}

Write-Host ""

# Check common installation paths
Write-Host "Checking common installation paths..." -ForegroundColor Yellow
$searchPaths = @(
    "C:\Program Files\PostgreSQL",
    "C:\Program Files (x86)\PostgreSQL",
    "C:\PostgreSQL",
    "$env:ProgramFiles\PostgreSQL",
    "$env:ProgramFiles(x86)\PostgreSQL"
)

foreach ($basePath in $searchPaths) {
    if (Test-Path $basePath) {
        Write-Host "  Checking: $basePath" -ForegroundColor Cyan
        $versions = Get-ChildItem -Path $basePath -Directory -ErrorAction SilentlyContinue
        foreach ($version in $versions) {
            $binPath = Join-Path $version.FullName "bin"
            $pgDumpPath = Join-Path $binPath "pg_dump.exe"
            if (Test-Path $pgDumpPath) {
                Write-Host "    [FOUND] $pgDumpPath" -ForegroundColor Green
                Write-Host "    Use this path: $binPath" -ForegroundColor Yellow
                $found = $true
            }
        }
    }
}

Write-Host ""

# Check registry
Write-Host "Checking Windows Registry..." -ForegroundColor Yellow
try {
    $regPaths = @(
        "HKLM:\SOFTWARE\PostgreSQL",
        "HKLM:\SOFTWARE\WOW6432Node\PostgreSQL"
    )
    
    foreach ($regPath in $regPaths) {
        if (Test-Path $regPath) {
            $installs = Get-ChildItem -Path $regPath -ErrorAction SilentlyContinue
            foreach ($install in $installs) {
                $binPath = Join-Path $install.GetValue("Base Directory") "bin"
                $pgDumpPath = Join-Path $binPath "pg_dump.exe"
                if (Test-Path $pgDumpPath) {
                    Write-Host "  [FOUND] $pgDumpPath" -ForegroundColor Green
                    Write-Host "  Use this path: $binPath" -ForegroundColor Yellow
                    $found = $true
                }
            }
        }
    }
} catch {
    Write-Host "  Could not check registry: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""

if (-not $found) {
    Write-Host "[NOT FOUND] PostgreSQL client tools not found on this system." -ForegroundColor Red
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "1. Install PostgreSQL from: https://www.postgresql.org/download/windows/" -ForegroundColor White
    Write-Host "2. Install only PostgreSQL client tools (smaller download)" -ForegroundColor White
    Write-Host "3. If PostgreSQL is installed, add the bin directory to your PATH:" -ForegroundColor White
    Write-Host "   [Environment]::SetEnvironmentVariable('Path', [Environment]::GetEnvironmentVariable('Path', 'User') + ';C:\Program Files\PostgreSQL\16\bin', 'User')" -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. Use the backup script with -PostgreSQLPath parameter:" -ForegroundColor White
    Write-Host "   .\backup-dev-db.ps1 -PostgreSQLPath `"C:\Path\To\PostgreSQL\bin`"" -ForegroundColor Gray
} else {
    Write-Host "[SUCCESS] PostgreSQL found! You can now run the backup script." -ForegroundColor Green
}




