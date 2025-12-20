# Deploy StudentTeamBuilderService to Dev/Test Environment
# This script publishes and deploys the service to the test environment

Write-Host "[START] Deploying StudentTeamBuilderService to Dev/Test..." -ForegroundColor Green
Write-Host ""

# Change to script directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptPath ".."
Set-Location $projectPath

$serviceName = "StrAppersStudentTeamBuilderDev"
$publishPath = Join-Path $projectPath "publish-dev"
$deployPath = "C:\Services\StudentTeamBuilderService-Dev"

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
    Write-Host "Step 3: Stopping Windows Service (if running)..." -ForegroundColor Yellow
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($service -and $service.Status -eq 'Running') {
        Stop-Service -Name $serviceName -Force
        Write-Host "Waiting for service to fully stop and release file handles..." -ForegroundColor Cyan
        Start-Sleep -Seconds 5
        
        # Wait up to 10 seconds for service to stop
        $timeout = 10
        $elapsed = 0
        while ($elapsed -lt $timeout) {
            $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
            if ($service.Status -ne 'Running') {
                break
            }
            Start-Sleep -Seconds 1
            $elapsed++
        }
        Write-Host "Service stopped successfully" -ForegroundColor Cyan
    } else {
        Write-Host "Service is not running or does not exist" -ForegroundColor Cyan
    }

    Write-Host ""
    Write-Host "Step 4: Publishing to local folder..." -ForegroundColor Yellow
    
    # Remove old publish folder (excluding logs if they're locked)
    if (Test-Path $publishPath) {
        try {
            # Try to remove everything except logs first
            $logsPath = Join-Path $publishPath "logs"
            if (Test-Path $logsPath) {
                Get-ChildItem -Path $publishPath -Exclude "logs" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
            } else {
                Remove-Item -Path $publishPath -Recurse -Force -ErrorAction Stop
            }
        } catch {
            Write-Host "Warning: Could not fully remove publish folder. Some files may be locked. Continuing..." -ForegroundColor Yellow
            # Try to remove specific locked files individually
            Get-ChildItem -Path $publishPath -File | Where-Object { $_.Extension -ne ".log" } | Remove-Item -Force -ErrorAction SilentlyContinue
        }
    }
    
    dotnet publish -c Release -o $publishPath
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed!"
    }

    Write-Host ""
    Write-Host "Step 5: Copying files to deployment location..." -ForegroundColor Yellow
    
    # Create deployment directory if it doesn't exist
    if (-not (Test-Path $deployPath)) {
        New-Item -ItemType Directory -Path $deployPath -Force | Out-Null
        Write-Host "Created deployment directory: $deployPath" -ForegroundColor Cyan
    }
    
    # Copy all files from publish folder to deployment location
    Copy-Item -Path "$publishPath\*" -Destination $deployPath -Recurse -Force
    Write-Host "Files copied successfully" -ForegroundColor Cyan

    Write-Host ""
    Write-Host "Step 6: Installing/Updating Windows Service..." -ForegroundColor Yellow
    
    # Check if service exists
    $serviceExists = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    
    if (-not $serviceExists) {
        # Install the service
        $exePath = Join-Path $deployPath "StudentTeamBuilderService.exe"
        sc.exe create $serviceName binPath= "`"$exePath`" Dev" start= auto
        if ($LASTEXITCODE -ne 0) {
            throw "Service installation failed!"
        }
        Write-Host "Service installed successfully" -ForegroundColor Cyan
    } else {
        Write-Host "Service already exists, skipping installation" -ForegroundColor Cyan
    }

    Write-Host ""
    Write-Host "Step 7: Starting Windows Service..." -ForegroundColor Yellow
    Start-Service -Name $serviceName
    if ($LASTEXITCODE -ne 0) {
        throw "Service start failed!"
    }
    
    Start-Sleep -Seconds 2
    $serviceStatus = (Get-Service -Name $serviceName).Status
    Write-Host "Service status: $serviceStatus" -ForegroundColor Cyan

    Write-Host ""
    Write-Host "[OK] Successfully deployed StudentTeamBuilderService to Dev/Test!" -ForegroundColor Green
    Write-Host "   Service Name: $serviceName" -ForegroundColor Cyan
    Write-Host "   Deployment Path: $deployPath" -ForegroundColor Cyan
    Write-Host "   Logs Path: $deployPath\logs" -ForegroundColor Cyan
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "[ERROR] Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    exit 1
}

