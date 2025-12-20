# Restore Development Database Script
# This script restores a PostgreSQL database backup

param(
    [Parameter(Mandatory=$true)]
    [string]$BackupFile
)

Write-Host "[START] Restoring Development Database..." -ForegroundColor Green
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

try {
    # Check if backup file exists
    if (-not (Test-Path $BackupFile)) {
        throw "Backup file not found: $BackupFile"
    }
    
    Write-Host "Backup file: $BackupFile" -ForegroundColor Cyan
    $fileSize = (Get-Item $BackupFile).Length
    $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
    Write-Host "File size: $fileSizeMB MB" -ForegroundColor Cyan
    Write-Host ""
    
    # Check if psql/pg_restore is available
    $psqlPath = "psql"
    $psqlCheck = Get-Command $psqlPath -ErrorAction SilentlyContinue
    
    if (-not $psqlCheck) {
        # Try common PostgreSQL installation paths
        $commonPaths = @(
            "C:\Program Files\PostgreSQL\16\bin\psql.exe",
            "C:\Program Files\PostgreSQL\15\bin\psql.exe",
            "C:\Program Files\PostgreSQL\14\bin\psql.exe",
            "C:\Program Files\PostgreSQL\13\bin\psql.exe",
            "C:\Program Files (x86)\PostgreSQL\16\bin\psql.exe",
            "C:\Program Files (x86)\PostgreSQL\15\bin\psql.exe"
        )
        
        $found = $false
        foreach ($path in $commonPaths) {
            if (Test-Path $path) {
                $psqlPath = $path
                $found = $true
                Write-Host "Found psql at: $psqlPath" -ForegroundColor Cyan
                break
            }
        }
        
        if (-not $found) {
            throw "psql not found. Please ensure PostgreSQL is installed and psql is in your PATH, or update the script with the correct path."
        }
    }
    
    # Determine backup format based on file extension
    $fileExtension = [System.IO.Path]::GetExtension($BackupFile).ToLower()
    $isCustomFormat = $fileExtension -eq ".custom" -or $fileExtension -eq ".backup"
    
    Write-Host "Step 1: WARNING - This will DROP and recreate the database!" -ForegroundColor Red
    Write-Host "Database: $dbName" -ForegroundColor Yellow
    Write-Host ""
    
    $confirmation = Read-Host "Are you sure you want to proceed? Type 'YES' to continue"
    if ($confirmation -ne "YES") {
        Write-Host "Restore cancelled." -ForegroundColor Yellow
        exit 0
    }
    
    Write-Host ""
    Write-Host "Step 2: Dropping existing database (if exists)..." -ForegroundColor Yellow
    
    # Set PGPASSWORD environment variable
    $env:PGPASSWORD = $dbPassword
    
    # Connect to postgres database to drop the target database
    $dropDbArgs = @(
        "-h", $dbHost,
        "-p", $dbPort,
        "-U", $dbUser,
        "-d", "postgres",
        "-c", "DROP DATABASE IF EXISTS `"$dbName`";"
    )
    
    & $psqlPath $dropDbArgs
    
    Write-Host ""
    Write-Host "Step 3: Creating new database..." -ForegroundColor Yellow
    
    $createDbArgs = @(
        "-h", $dbHost,
        "-p", $dbPort,
        "-U", $dbUser,
        "-d", "postgres",
        "-c", "CREATE DATABASE `"$dbName`";"
    )
    
    & $psqlPath $createDbArgs
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create database"
    }
    
    Write-Host ""
    Write-Host "Step 4: Restoring backup..." -ForegroundColor Yellow
    
    if ($isCustomFormat) {
        # Use pg_restore for custom format
        $pgRestorePath = $psqlPath -replace "psql.exe", "pg_restore.exe"
        
        if (-not (Test-Path $pgRestorePath)) {
            throw "pg_restore not found at: $pgRestorePath"
        }
        
        $restoreArgs = @(
            "-h", $dbHost,
            "-p", $dbPort,
            "-U", $dbUser,
            "-d", $dbName,
            "-v",
            $BackupFile
        )
        
        Write-Host "Using pg_restore (custom format)..." -ForegroundColor Cyan
        & $pgRestorePath $restoreArgs
    } else {
        # Use psql for plain SQL format
        $restoreArgs = @(
            "-h", $dbHost,
            "-p", $dbPort,
            "-U", $dbUser,
            "-d", $dbName,
            "-f", $BackupFile
        )
        
        Write-Host "Using psql (plain SQL format)..." -ForegroundColor Cyan
        & $psqlPath $restoreArgs
    }
    
    if ($LASTEXITCODE -ne 0) {
        throw "Restore failed with exit code $LASTEXITCODE"
    }
    
    # Clear password from environment
    $env:PGPASSWORD = $null
    
    Write-Host ""
    Write-Host "[OK] Database restored successfully!" -ForegroundColor Green
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




