@echo off
echo Quick Build and Publish to IIS
echo ================================
echo.

cd /d "%~dp0"

echo 1. Stopping IIS...
iisreset /stop

echo 2. Building project...
dotnet build -c Release

echo 3. Publishing to IIS...
dotnet publish -c Release -o C:\inetpub\wwwroot\StrAppersAPI

echo 4. Starting IIS...
iisreset /start

echo.
echo ✅ Done! Your API is available at: http://localhost:8001/swagger
echo.
pause

