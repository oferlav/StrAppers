# Quick Backup Development Database Script
# Simplified version - just creates a plain SQL backup

$dbHost = "localhost"
$dbPort = "5432"
$dbName = "StrAppersDB_Dev"
$dbUser = "postgres"
$dbPassword = "sa"

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptPath ".."
$backupsDir = Join-Path $projectPath "Backups"

if (-not (Test-Path $backupsDir)) {
    New-Item -ItemType Directory -Path $backupsDir -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = Join-Path $backupsDir "StrAppersDB_Dev_Backup_$timestamp.sql"

$env:PGPASSWORD = $dbPassword
pg_dump -h $dbHost -p $dbPort -U $dbUser -d $dbName -F p -f $backupFile
$env:PGPASSWORD = $null

if (Test-Path $backupFile) {
    $size = [math]::Round((Get-Item $backupFile).Length / 1MB, 2)
    Write-Host "Backup created: $backupFile ($size MB)" -ForegroundColor Green
} else {
    Write-Host "Backup failed!" -ForegroundColor Red
    exit 1
}




