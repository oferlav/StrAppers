@echo off
echo Building and publishing to IIS...
echo.

cd /d "%~dp0"

echo Step 1: Building project...
dotnet build -c Release
if %ERRORLEVEL% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Step 2: Publishing to IIS...
dotnet publish -c Release -o C:\inetpub\wwwroot\StrAppersAPI
if %ERRORLEVEL% neq 0 (
    echo Publish failed!
    pause
    exit /b 1
)

echo.
echo Step 3: Restarting IIS...
iisreset
if %ERRORLEVEL% neq 0 (
    echo IIS restart failed!
    pause
    exit /b 1
)

echo.
echo ✅ Build and publish completed successfully!
echo Your API is now available at: http://localhost:8001/swagger
echo.
pause


