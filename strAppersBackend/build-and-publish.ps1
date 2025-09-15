# Build and Publish Script for StrAppers API
Write-Host "🚀 Building and publishing to IIS..." -ForegroundColor Green
Write-Host ""

# Change to script directory
Set-Location $PSScriptRoot

try {
    Write-Host "Step 1: Building project..." -ForegroundColor Yellow
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed!"
    }

    Write-Host ""
    Write-Host "Step 2: Publishing to IIS..." -ForegroundColor Yellow
    dotnet publish -c Release -o C:\inetpub\wwwroot\StrAppersAPI
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed!"
    }

    Write-Host ""
    Write-Host "Step 3: Restarting IIS..." -ForegroundColor Yellow
    iisreset
    if ($LASTEXITCODE -ne 0) {
        throw "IIS restart failed!"
    }

    Write-Host ""
    Write-Host "✅ Build and publish completed successfully!" -ForegroundColor Green
    Write-Host "Your API is now available at: http://localhost:8001/swagger" -ForegroundColor Cyan
    Write-Host ""
}
catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Read-Host "Press Enter to continue"
    exit 1
}

Read-Host "Press Enter to continue"

