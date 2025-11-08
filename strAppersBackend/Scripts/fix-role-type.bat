@echo off
echo Fixing Role.Type column NULL values...

REM Set your PostgreSQL connection details here
set PGHOST=localhost
set PGPORT=5432
set PGDATABASE=StrAppersDB
set PGUSER=postgres
set PGPASSWORD=your_password

echo Connecting to PostgreSQL database...
psql -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -f "Database/fix_role_type_column.sql"

if %ERRORLEVEL% EQU 0 (
    echo ✅ Role.Type column fixed successfully!
    echo All existing roles now have proper Type values.
) else (
    echo ❌ Error fixing Role.Type column.
    echo Please check your PostgreSQL connection settings and run the SQL script manually.
    echo SQL Script location: Database/fix_role_type_column.sql
)

pause






