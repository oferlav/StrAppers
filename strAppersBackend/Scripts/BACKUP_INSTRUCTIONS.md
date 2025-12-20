# Database Backup Instructions

## Problem: pg_dump not found

If you're getting an error that `pg_dump` is not found, you need to install PostgreSQL client tools.

## Solutions

### Option 1: Install PostgreSQL Client Tools (Recommended)

1. Download PostgreSQL from: https://www.postgresql.org/download/windows/
2. During installation, make sure to install "Command Line Tools"
3. Add PostgreSQL bin directory to your PATH:
   - Find your PostgreSQL installation (usually `C:\Program Files\PostgreSQL\16\bin` or similar)
   - Add it to your system PATH environment variable
   - Or use the script with `-PostgreSQLPath` parameter

### Option 2: Use the Script with Custom Path

If PostgreSQL is installed but not in PATH, run:

```powershell
.\Scripts\backup-dev-db.ps1 -PostgreSQLPath "C:\Program Files\PostgreSQL\16\bin"
```

Replace `16` with your PostgreSQL version number.

### Option 3: Find Your PostgreSQL Installation

Run the helper script to locate PostgreSQL:

```powershell
.\Scripts\find-postgresql.ps1
```

### Option 4: Download Portable PostgreSQL Client Tools

1. Download a portable PostgreSQL client from: https://www.enterprisedb.com/download-postgresql-binaries
2. Extract it to a folder (e.g., `C:\PostgreSQL\bin`)
3. Use the script with `-PostgreSQLPath` parameter pointing to the bin folder

### Option 5: Add PostgreSQL to PATH (Permanent Solution)

Run this in PowerShell (replace version number as needed):

```powershell
$postgresPath = "C:\Program Files\PostgreSQL\16\bin"
$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($currentPath -notlike "*$postgresPath*") {
    [Environment]::SetEnvironmentVariable("Path", "$currentPath;$postgresPath", "User")
    Write-Host "Added PostgreSQL to PATH. Please restart your terminal."
}
```

## Quick Backup Script

If you just need a quick backup and have PostgreSQL in PATH:

```powershell
.\Scripts\quick-backup-dev-db.ps1
```

## Restore Backup

To restore a backup:

```powershell
.\Scripts\restore-dev-db.ps1 -BackupFile "Backups\StrAppersDB_Dev_Backup_20240101_120000.sql"
```

## Verify Database Connection

To test if you can connect to the database:

```powershell
# If psql is available
psql -h localhost -p 5432 -U postgres -d StrAppersDB_Dev -c "SELECT version();"
```

## Troubleshooting

- **"pg_dump not found"**: Install PostgreSQL client tools (see Option 1)
- **"Access denied"**: Check database password in `appsettings.Development.json`
- **"Connection refused"**: Make sure PostgreSQL service is running
- **"Database does not exist"**: Verify database name matches `StrAppersDB_Dev`




