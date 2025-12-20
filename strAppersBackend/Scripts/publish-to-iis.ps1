# Publish to IIS (Development) Script
# This script publishes the backend to local IIS

Write-Host "[START] Publishing to IIS (Development)..." -ForegroundColor Green
Write-Host ""

# Change to script directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptPath ".."
Set-Location $projectPath

try {
    Write-Host "Step 1: Cleaning project..." -ForegroundColor Yellow
    dotnet clean -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Clean failed!"
    }

    Write-Host ""
    Write-Host "Step 2: Building project..." -ForegroundColor Yellow
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed!"
    }

    Write-Host ""
    Write-Host "Step 3: Publishing to IIS directory..." -ForegroundColor Yellow
    $iisPath = "C:\inetpub\wwwroot\StrAppersAPI"
    
    # Create directory if it doesn't exist
    if (-not (Test-Path $iisPath)) {
        New-Item -ItemType Directory -Path $iisPath -Force | Out-Null
        Write-Host "Created IIS directory: $iisPath" -ForegroundColor Cyan
    }
    
    dotnet publish -c Release -o $iisPath
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed!"
    }
    
    # Create logs directory in the original location (for dev)
    $logsPath = "C:\StrAppers\strAppersBackend\logs"
    if (-not (Test-Path $logsPath)) {
        New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
        Write-Host "Created logs directory: $logsPath" -ForegroundColor Cyan
    }
    
    # Update web.config in IIS directory to use absolute path for dev logs
    $webConfigPath = Join-Path $iisPath "web.config"
    if (Test-Path $webConfigPath) {
        $webConfigContent = Get-Content $webConfigPath -Raw
        # Replace relative path with absolute path for dev
        $webConfigContent = $webConfigContent -replace 'stdoutLogFile="\.\\logs\\stdout"', 'stdoutLogFile="C:\StrAppers\strAppersBackend\logs\stdout"'
        Set-Content -Path $webConfigPath -Value $webConfigContent -NoNewline
        Write-Host "Updated web.config to use dev logs path: $logsPath" -ForegroundColor Cyan
    }

    # Remove appsettings.Development.json if it was copied (we only use appsettings.json and appsettings.Production.json)
    $devConfigPath = Join-Path $iisPath "appsettings.Development.json"
    if (Test-Path $devConfigPath) {
        Remove-Item -Path $devConfigPath -Force -ErrorAction SilentlyContinue
        Write-Host "Removed appsettings.Development.json (not used)" -ForegroundColor Cyan
    }

    Write-Host ""
    Write-Host "Step 4: Restarting IIS..." -ForegroundColor Yellow
    iisreset
    if ($LASTEXITCODE -ne 0) {
        throw "IIS restart failed!"
    }

    Write-Host ""
    Write-Host "[OK] Successfully published to IIS!" -ForegroundColor Green
    Write-Host "Your API is now available at: http://localhost:9001/swagger" -ForegroundColor Cyan
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "[ERROR] Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    exit 1
}

