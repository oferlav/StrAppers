# Backup Development Database Script
# This script creates a backup of the StrAppersDB_Dev PostgreSQL database
# 
# Usage:
#   .\backup-dev-db.ps1
#   .\backup-dev-db.ps1 -PostgreSQLPath "C:\Program Files\PostgreSQL\16\bin"

param(
    [Parameter(Mandatory=$false)]
    [string]$PostgreSQLPath = ""
)

Write-Host "[START] Backing up Development Database..." -ForegroundColor Green
Write-Host ""

# Database connection details
$dbHost = "localhost"
$dbPort = "5432"
$dbName = "StrAppersDB_Dev"
$dbUser = "postgres"
$dbPassword = "sa"

# Change to script directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptPath ".."
Set-Location $projectPath

# Create backups directory if it doesn't exist
$backupsDir = Join-Path $projectPath "Backups"
if (-not (Test-Path $backupsDir)) {
    New-Item -ItemType Directory -Path $backupsDir -Force | Out-Null
    Write-Host "Created backups directory: $backupsDir" -ForegroundColor Cyan
}

try {
    # Check if pg_dump is available
    $pgDumpPath = "pg_dump"
    $pgDumpCheck = Get-Command $pgDumpPath -ErrorAction SilentlyContinue
    
    if (-not $pgDumpCheck) {
        # If user provided a path, use it
        if ($PostgreSQLPath -ne "") {
            $customPgDumpPath = Join-Path $PostgreSQLPath "pg_dump.exe"
            if (Test-Path $customPgDumpPath) {
                $pgDumpPath = $customPgDumpPath
                Write-Host "Using provided PostgreSQL path: $pgDumpPath" -ForegroundColor Cyan
            } else {
                throw "pg_dump not found at provided path: $customPgDumpPath"
            }
        } else {
            # Try common PostgreSQL installation paths (expanded list)
            $commonPaths = @(
                "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe",
                "C:\Program Files\PostgreSQL\15\bin\pg_dump.exe",
                "C:\Program Files\PostgreSQL\14\bin\pg_dump.exe",
                "C:\Program Files\PostgreSQL\13\bin\pg_dump.exe",
                "C:\Program Files\PostgreSQL\12\bin\pg_dump.exe",
                "C:\Program Files (x86)\PostgreSQL\16\bin\pg_dump.exe",
                "C:\Program Files (x86)\PostgreSQL\15\bin\pg_dump.exe",
                "C:\Program Files (x86)\PostgreSQL\14\bin\pg_dump.exe",
                "C:\Program Files (x86)\PostgreSQL\13\bin\pg_dump.exe",
                "C:\PostgreSQL\16\bin\pg_dump.exe",
                "C:\PostgreSQL\15\bin\pg_dump.exe",
                "C:\PostgreSQL\14\bin\pg_dump.exe",
                "$env:ProgramFiles\PostgreSQL\*\bin\pg_dump.exe",
                "$env:ProgramFiles(x86)\PostgreSQL\*\bin\pg_dump.exe"
            )
            
            $found = $false
            foreach ($path in $commonPaths) {
                # Handle wildcard paths
                if ($path -like "*\*") {
                    $matchingPaths = Get-ChildItem -Path (Split-Path $path -Parent) -Filter "pg_dump.exe" -Recurse -ErrorAction SilentlyContinue
                    if ($matchingPaths) {
                        $pgDumpPath = $matchingPaths[0].FullName
                        $found = $true
                        Write-Host "Found pg_dump at: $pgDumpPath" -ForegroundColor Cyan
                        break
                    }
                } elseif (Test-Path $path) {
                    $pgDumpPath = $path
                    $found = $true
                    Write-Host "Found pg_dump at: $pgDumpPath" -ForegroundColor Cyan
                    break
                }
            }
            
            if (-not $found) {
                Write-Host ""
                Write-Host "[ERROR] pg_dump not found!" -ForegroundColor Red
                Write-Host ""
                Write-Host "Please do one of the following:" -ForegroundColor Yellow
                Write-Host "1. Install PostgreSQL client tools" -ForegroundColor White
                Write-Host "2. Add PostgreSQL bin directory to your PATH environment variable" -ForegroundColor White
                Write-Host "3. Run the script with -PostgreSQLPath parameter:" -ForegroundColor White
                Write-Host "   .\backup-dev-db.ps1 -PostgreSQLPath `"C:\Program Files\PostgreSQL\16\bin`"" -ForegroundColor Gray
                Write-Host ""
                Write-Host "Common PostgreSQL installation locations:" -ForegroundColor Yellow
                Write-Host "  - C:\Program Files\PostgreSQL\[version]\bin\" -ForegroundColor Gray
                Write-Host "  - C:\Program Files (x86)\PostgreSQL\[version]\bin\" -ForegroundColor Gray
                Write-Host ""
                throw "pg_dump executable not found"
            }
        }
    } else {
        Write-Host "Using pg_dump from PATH: $($pgDumpCheck.Source)" -ForegroundColor Cyan
    }

    Write-Host "Step 1: Checking database connection..." -ForegroundColor Yellow
    
    # Test database connection using psql (if available)
    $psqlPath = "psql"
    $psqlCheck = Get-Command $psqlPath -ErrorAction SilentlyContinue
    if (-not $psqlCheck) {
        $commonPsqlPaths = @(
            "C:\Program Files\PostgreSQL\16\bin\psql.exe",
            "C:\Program Files\PostgreSQL\15\bin\psql.exe",
            "C:\Program Files\PostgreSQL\14\bin\psql.exe"
        )
        foreach ($path in $commonPsqlPaths) {
            if (Test-Path $path) {
                $psqlPath = $path
                break
            }
        }
    }
    
    Write-Host ""
    Write-Host "Step 2: Creating database backup (plain SQL format)..." -ForegroundColor Yellow
    
    # Generate backup filename with timestamp
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupFileName = "StrAppersDB_Dev_Backup_$timestamp.sql"
    $backupFilePath = Join-Path $backupsDir $backupFileName
    
    # Set PGPASSWORD environment variable for pg_dump
    $env:PGPASSWORD = $dbPassword
    
    # Build pg_dump command (plain SQL format - human readable and universally usable)
    $pgDumpArgs = @(
        "-h", $dbHost,
        "-p", $dbPort,
        "-U", $dbUser,
        "-d", $dbName,
        "-F", "p",  # Plain SQL format (human-readable)
        "-b",       # Include blobs
        "-v",       # Verbose
        "-f", $backupFilePath
    )
    
    Write-Host "Running: $pgDumpPath $($pgDumpArgs -join ' ')" -ForegroundColor Gray
    Write-Host ""
    
    # Execute pg_dump
    & $pgDumpPath $pgDumpArgs
    
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump failed with exit code $LASTEXITCODE"
    }
    
    # Clear password from environment
    $env:PGPASSWORD = $null
    
    # Verify backup file was created
    if (-not (Test-Path $backupFilePath)) {
        throw "Backup file was not created: $backupFilePath"
    }
    
    # Get file size
    $fileSize = (Get-Item $backupFilePath).Length
    $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
    
    Write-Host ""
    Write-Host "[OK] Database backup completed successfully!" -ForegroundColor Green
    Write-Host "Backup file: $backupFilePath" -ForegroundColor Cyan
    Write-Host "File size: $fileSizeMB MB" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To restore this backup, use:" -ForegroundColor Yellow
    Write-Host "  psql -h localhost -p 5432 -U postgres -d StrAppersDB_Dev -f `"$backupFilePath`"" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Or using pg_restore (if you need to restore selectively):" -ForegroundColor Yellow
    Write-Host "  pg_restore -h localhost -p 5432 -U postgres -d StrAppersDB_Dev -c `"$backupFilePath`"" -ForegroundColor Gray
    Write-Host ""
    
    # Optional: Create a compressed custom format backup as well
    Write-Host "Step 3: Creating compressed backup (optional)..." -ForegroundColor Yellow
    $compressedBackupFileName = "StrAppersDB_Dev_Backup_$timestamp.custom"
    $compressedBackupFilePath = Join-Path $backupsDir $compressedBackupFileName
    
    $env:PGPASSWORD = $dbPassword
    $pgDumpArgsCompressed = @(
        "-h", $dbHost,
        "-p", $dbPort,
        "-U", $dbUser,
        "-d", $dbName,
        "-F", "c",  # Custom format (compressed, allows selective restore)
        "-b",       # Include blobs
        "-v",       # Verbose
        "-f", $compressedBackupFilePath
    )
    
    & $pgDumpPath $pgDumpArgsCompressed
    
    if ($LASTEXITCODE -eq 0 -and (Test-Path $compressedBackupFilePath)) {
        $compressedFileSize = (Get-Item $compressedBackupFilePath).Length
        $compressedFileSizeMB = [math]::Round($compressedFileSize / 1MB, 2)
        Write-Host "Compressed backup created: $compressedBackupFilePath ($compressedFileSizeMB MB)" -ForegroundColor Cyan
        Write-Host "Compression ratio: $([math]::Round((1 - $compressedFileSize / $fileSize) * 100, 1))% smaller" -ForegroundColor Cyan
    } else {
        Write-Host "Compressed backup skipped or failed (this is optional)" -ForegroundColor Yellow
    }
    
    $env:PGPASSWORD = $null
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "[ERROR] Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    
    # Clear password from environment on error
    $env:PGPASSWORD = $null
    
    exit 1
}

