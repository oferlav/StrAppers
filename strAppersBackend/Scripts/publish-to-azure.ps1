# Publish to Azure (Production) Script
# This script publishes the backend to Azure App Service

param(
    [string]$ResourceGroup = "Strappers_gr",
    [string]$AppServiceName = "skill-in-backend"
)

Write-Host "[START] Publishing to Azure (Production)..." -ForegroundColor Green
Write-Host ""

# Change to script directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptPath ".."
Set-Location $projectPath

# Azure CLI path
$azCliPath = "C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"

# Check if Azure CLI is available
if (-not (Test-Path $azCliPath)) {
    Write-Host "[ERROR] Azure CLI not found at: $azCliPath" -ForegroundColor Red
    Write-Host "Please install Azure CLI or update the path in this script." -ForegroundColor Yellow
    exit 1
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
    Write-Host "Step 3: Publishing to local folder..." -ForegroundColor Yellow
    $publishPath = Join-Path $projectPath "publish-prod"
    
    # Remove old publish folder
    if (Test-Path $publishPath) {
        Remove-Item -Path $publishPath -Recurse -Force
    }
    
    dotnet publish -c Release -o $publishPath
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed!"
    }

    Write-Host ""
    Write-Host "Step 4: Creating deployment zip..." -ForegroundColor Yellow
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
    Write-Host "Step 5: Deploying to Azure App Service..." -ForegroundColor Yellow
    Write-Host "   Resource Group: $ResourceGroup" -ForegroundColor Cyan
    Write-Host "   App Service: $AppServiceName" -ForegroundColor Cyan
    
    & $azCliPath webapp deploy --resource-group $ResourceGroup --name $AppServiceName --src-path $zipPath --type zip
    if ($LASTEXITCODE -ne 0) {
        throw "Azure deployment failed!"
    }

    Write-Host ""
    Write-Host "[OK] Successfully deployed to Azure!" -ForegroundColor Green
    # Get the full domain name for the App Service (redirect stderr to filter warnings)
    $appServiceInfo = & $azCliPath webapp show --resource-group $ResourceGroup --name $AppServiceName --query "{defaultHostName:defaultHostName}" -o json 2>$null
    if ($LASTEXITCODE -eq 0 -and $appServiceInfo) {
        try {
            $appInfo = $appServiceInfo | ConvertFrom-Json -ErrorAction Stop
            if ($appInfo -and $appInfo.defaultHostName) {
                Write-Host "Your API is now available at: https://$($appInfo.defaultHostName)/swagger" -ForegroundColor Cyan
            } else {
                Write-Host "Your API is now available at: https://$AppServiceName.azurewebsites.net/swagger" -ForegroundColor Cyan
            }
        } catch {
            Write-Host "Your API is now available at: https://$AppServiceName.azurewebsites.net/swagger" -ForegroundColor Cyan
        }
    } else {
        Write-Host "Your API is now available at: https://$AppServiceName.azurewebsites.net/swagger" -ForegroundColor Cyan
    }
    Write-Host ""
    
    # Cleanup
    Write-Host "Cleaning up temporary files..." -ForegroundColor Yellow
    Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue
}
catch {
    Write-Host ""
    Write-Host "[ERROR] Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    exit 1
}

