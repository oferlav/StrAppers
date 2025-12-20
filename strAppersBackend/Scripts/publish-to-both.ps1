# Publish to Both IIS and Azure Script
# This script publishes the backend to both local IIS (dev) and Azure (production)

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

# Azure CLI path
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
    Write-Host "===============================================================" -ForegroundColor Cyan
    Write-Host ""

    # Check if Azure CLI is available
    if (-not (Test-Path $azCliPath)) {
        Write-Host "[WARNING] Azure CLI not found. Skipping Azure deployment." -ForegroundColor Yellow
        Write-Host "   Azure CLI path: $azCliPath" -ForegroundColor Yellow
        Write-Host ""
        exit 0
    }

    # Try to find the App Service and its resource group
    Write-Host "Searching for App Service: $AppServiceName..." -ForegroundColor Yellow
    # Redirect stderr to null to filter out warnings, then capture stdout
    $appServiceListOutput = & $azCliPath webapp list --query "[?name=='$AppServiceName'].{Name:name, ResourceGroup:resourceGroup}" -o json 2>$null
    $appServiceListExitCode = $LASTEXITCODE

    if ($appServiceListExitCode -eq 0 -and $appServiceListOutput) {
        # Try to parse JSON, but handle errors gracefully
        try {
            $appServices = $appServiceListOutput | ConvertFrom-Json -ErrorAction Stop
            if ($appServices -and (@($appServices).Count -gt 0)) {
                $foundResourceGroup = $appServices[0].ResourceGroup
                Write-Host "[CHECK] Found App Service in resource group: $foundResourceGroup" -ForegroundColor Green
                $ResourceGroup = $foundResourceGroup
            } else {
                Write-Host "[WARNING] App Service '$AppServiceName' not found in search results." -ForegroundColor Yellow
                Write-Host "   Using default resource group: $ResourceGroup" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "[WARNING] Could not parse App Service search results. Using default resource group: $ResourceGroup" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[WARNING] Could not search for App Service. Using provided/default resource group: $ResourceGroup" -ForegroundColor Yellow
    }
    
    # Verify resource group exists (optional check)
    Write-Host "Verifying Azure App Service..." -ForegroundColor Yellow
    # Redirect stderr to null to filter out warnings
    $appServiceCheck = & $azCliPath webapp show --resource-group $ResourceGroup --name $AppServiceName --query "{name:name, state:state}" -o json 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $appServiceCheck) {
        Write-Host "[ERROR] App Service not found in resource group '$ResourceGroup'" -ForegroundColor Red
        Write-Host ""
        Write-Host "Listing all App Services to help you find the correct one:" -ForegroundColor Yellow
        $allApps = & $azCliPath webapp list --query "[].{Name:name, ResourceGroup:resourceGroup}" -o table 2>$null
        Write-Host $allApps
        exit 1
    } else {
        try {
            $appInfo = $appServiceCheck | ConvertFrom-Json -ErrorAction Stop
            Write-Host "[CHECK] App Service verified: $($appInfo.name) (State: $($appInfo.state))" -ForegroundColor Green
        } catch {
            Write-Host "[WARNING] Could not parse verification response, but continuing..." -ForegroundColor Yellow
        }
    }

    Write-Host "Step 5: Publishing to local folder for Azure..." -ForegroundColor Yellow
    $publishPath = Join-Path $projectPath "publish-prod"
    
    # Remove old publish folder
    if (Test-Path $publishPath) {
        Remove-Item -Path $publishPath -Recurse -Force
    }
    
    dotnet publish -c Release -o $publishPath
    if ($LASTEXITCODE -ne 0) {
        throw "Azure publish failed!"
    }

    Write-Host ""
    Write-Host "Step 6: Creating deployment zip..." -ForegroundColor Yellow
    $zipPath = Join-Path $projectPath "publish-prod.zip"
    
    # Remove old zip if exists
    if (Test-Path $zipPath) {
        Remove-Item -Path $zipPath -Force
    }
    
    Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force
    if (-not (Test-Path $zipPath)) {
        throw "Failed to create zip file!"
    }
    
    $zipSize = (Get-Item $zipPath).Length / 1MB
    $zipSizeRounded = [math]::Round($zipSize, 2)
    $sizeText = "$zipSizeRounded MB"
    Write-Host "Created zip file: $zipPath ($sizeText)" -ForegroundColor Cyan

    Write-Host ""
    Write-Host "Step 7: Deploying to Azure App Service..." -ForegroundColor Yellow
    Write-Host "   Resource Group: $ResourceGroup" -ForegroundColor Cyan
    Write-Host "   App Service: $AppServiceName" -ForegroundColor Cyan
    
    & $azCliPath webapp deploy --resource-group $ResourceGroup --name $AppServiceName --src-path $zipPath --type zip
    if ($LASTEXITCODE -ne 0) {
        throw "Azure deployment failed!"
    }

    Write-Host ""
    Write-Host "[OK] Successfully deployed to Azure!" -ForegroundColor Green
    Write-Host "   Prod API: https://$AppServiceName.azurewebsites.net/swagger" -ForegroundColor Cyan
    Write-Host ""
    
    # Cleanup
    Write-Host "Cleaning up temporary files..." -ForegroundColor Yellow
    Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host "===============================================================" -ForegroundColor Green
    Write-Host "[OK] SUCCESS! Published to both environments:" -ForegroundColor Green
    Write-Host "   - Development (IIS): http://localhost:9001/swagger" -ForegroundColor Cyan
    # Get the full domain name for the App Service (redirect stderr to filter warnings)
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
