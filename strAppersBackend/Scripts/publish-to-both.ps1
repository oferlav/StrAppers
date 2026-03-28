# Publish to Both IIS and Azure Script
# This script publishes the backend to both local IIS (dev) and Azure (production).
# Azure deployment is delegated to publish-to-azure.ps1 (single source of truth).

param(
    [string]$ResourceGroup = "Strappers_gr",
    [string]$AppServiceName = "skill-in-backend"
)

Write-Host "[START] Publishing to Both IIS (Dev) and Azure (Prod)..." -ForegroundColor Green
Write-Host ""

# Change to script directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptPath ".."
Set-Location $projectPath

$azureScript = Join-Path $scriptPath "publish-to-azure.ps1"
$azCliPath = "C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"

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
    Write-Host "===============================================================" -ForegroundColor Cyan
    Write-Host "PART 1: Publishing to IIS (Development)" -ForegroundColor Cyan
    Write-Host "===============================================================" -ForegroundColor Cyan
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
        throw "IIS publish failed!"
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
    Write-Host "   Dev API: http://localhost:9001/swagger" -ForegroundColor Cyan
    Write-Host ""

    Write-Host ""
    Write-Host "===============================================================" -ForegroundColor Cyan
    Write-Host "PART 2: Publishing to Azure (Production)" -ForegroundColor Cyan
    Write-Host "   (running publish-to-azure.ps1)" -ForegroundColor DarkCyan
    Write-Host "===============================================================" -ForegroundColor Cyan
    Write-Host ""

    if (-not (Test-Path $azureScript)) {
        throw "Azure script not found: $azureScript"
    }

    if (-not (Test-Path $azCliPath)) {
        Write-Host "[WARNING] Azure CLI not found. Skipping Azure deployment." -ForegroundColor Yellow
        Write-Host "   Azure CLI path: $azCliPath" -ForegroundColor Yellow
        Write-Host ""
        exit 0
    }

    & $azureScript -ResourceGroup $ResourceGroup -AppServiceName $AppServiceName -SkipCleanBuild
    if ($LASTEXITCODE -ne 0) {
        throw "Azure deployment failed (publish-to-azure.ps1 exit code $LASTEXITCODE)."
    }

    # publish-to-azure.ps1 may resolve a different resource group internally; refresh for the summary query.
    $rgResolved = & $azCliPath webapp list --query "[?name=='$AppServiceName'].resourceGroup | [0]" -o tsv 2>$null
    if ($LASTEXITCODE -eq 0 -and $rgResolved) {
        $ResourceGroup = $rgResolved.Trim()
    }

    Write-Host ""
    Write-Host "===============================================================" -ForegroundColor Green
    Write-Host "[OK] SUCCESS! Published to both environments:" -ForegroundColor Green
    Write-Host "   - Development (IIS): http://localhost:9001/swagger" -ForegroundColor Cyan
    $appServiceInfo = & $azCliPath webapp show --resource-group $ResourceGroup --name $AppServiceName --query "{defaultHostName:defaultHostName}" -o json 2>$null
    if ($LASTEXITCODE -eq 0 -and $appServiceInfo) {
        try {
            $appInfo = $appServiceInfo | ConvertFrom-Json -ErrorAction Stop
            if ($appInfo -and $appInfo.defaultHostName) {
                Write-Host "   - Production (Azure): https://$($appInfo.defaultHostName)/swagger" -ForegroundColor Cyan
            } else {
                Write-Host "   - Production (Azure): https://$AppServiceName.azurewebsites.net/swagger" -ForegroundColor Cyan
            }
        } catch {
            Write-Host "   - Production (Azure): https://$AppServiceName.azurewebsites.net/swagger" -ForegroundColor Cyan
        }
    } else {
        Write-Host "   - Production (Azure): https://$AppServiceName.azurewebsites.net/swagger" -ForegroundColor Cyan
    }
    Write-Host "===============================================================" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "[ERROR] Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    exit 1
}
