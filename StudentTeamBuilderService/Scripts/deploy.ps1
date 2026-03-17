# Deploy StudentTeamBuilderService — one script for Dev and Prod.
# Usage: .\deploy.ps1 -Target Dev   or   .\deploy.ps1 -Target Prod
# Run as Administrator.

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Dev", "Prod")]
    [string]$Target
)

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = [System.IO.Path]::GetFullPath((Join-Path $scriptPath ".."))
Set-Location $projectPath

$isDev = ($Target -eq "Dev")
$serviceName = if ($isDev) { "StrAppersStudentTeamBuilderDev" } else { "StrAppersStudentTeamBuilder" }
$deployFolder = if ($isDev) { "publish-dev" } else { "publish-prod" }
$arg = if ($isDev) { "Dev" } else { "Prod" }

$projectFile = Join-Path $projectPath "StudentTeamBuilderService.csproj"
$deployPath = [System.IO.Path]::GetFullPath((Join-Path $projectPath $deployFolder))
$tempPublish = [System.IO.Path]::GetFullPath((Join-Path $projectPath "_out\$Target"))

Write-Host "[START] Deploying to $Target..." -ForegroundColor Green

# Clean + build
dotnet clean $projectFile -c Release -nologo -v q
dotnet build $projectFile -c Release -nologo -v m
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

# Stop service
$svc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq 'Running') {
    Stop-Service -Name $serviceName -Force
    Start-Sleep -Seconds 5
}

# Publish to temp (SDK can put files wherever; we'll find the exe)
if (Test-Path $tempPublish) { Remove-Item $tempPublish -Recurse -Force }
$null = New-Item -ItemType Directory -Path $tempPublish -Force
dotnet publish $projectFile -c Release -o $tempPublish -nologo -v m
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

# Find folder that contains the exe (root or one subdir)
$exeName = "StudentTeamBuilderService.exe"
$sourceDir = $null
if (Test-Path (Join-Path $tempPublish $exeName)) { $sourceDir = $tempPublish }
else {
    Get-ChildItem -Path $tempPublish -Directory | ForEach-Object {
        if (Test-Path (Join-Path $_.FullName $exeName)) { $sourceDir = $_.FullName }
    }
}
if (-not $sourceDir) { throw "Publish did not produce $exeName under $tempPublish" }

# Replace deploy folder: remove it fully, recreate, then copy (skip publish-dev/publish-prod so we never copy those junk folders)
if (Test-Path $deployPath) {
    Remove-Item $deployPath -Recurse -Force
    Start-Sleep -Milliseconds 300
}
$null = New-Item -ItemType Directory -Path $deployPath -Force
Get-ChildItem -Path $sourceDir -Force -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -ne "publish-dev" -and $_.Name -ne "publish-prod"
} | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $deployPath -Recurse -Force
}
# Remove any nested publish-dev/publish-prod that might still exist
Get-ChildItem -Path $deployPath -Directory -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -eq "publish-dev" -or $_.Name -eq "publish-prod"
} | ForEach-Object {
    Write-Host "Removing nested folder: $($_.Name)" -ForegroundColor Yellow
    Remove-Item $_.FullName -Recurse -Force
}

# Cleanup: keep only the config for this target; remove the other appsettings
if ($isDev) {
    @("appsettings.Prod.json", "appsettings.Production.json", "appsettings.Dev.json") | ForEach-Object {
        $f = Join-Path $deployPath $_
        if (Test-Path $f) { Remove-Item $f -Force; Write-Host "Removed $_ from deploy folder" -ForegroundColor Yellow }
    }
} else {
    @("appsettings.json", "appsettings.Production.json", "appsettings.Dev.json") | ForEach-Object {
        $f = Join-Path $deployPath $_
        if (Test-Path $f) { Remove-Item $f -Force; Write-Host "Removed $_ from deploy folder" -ForegroundColor Yellow }
    }
}

$exePath = Join-Path $deployPath $exeName
if (-not (Test-Path $exePath)) { throw "Exe missing at $exePath" }

# Reinstall service
$svc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($svc) {
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}
sc.exe create $serviceName binPath= "`"$exePath`" $arg" start= auto
if ($LASTEXITCODE -ne 0) { throw "Service install failed. Run as Administrator." }

# Start
Start-Service -Name $serviceName -ErrorAction Stop
Start-Sleep -Seconds 2
Write-Host "[OK] $Target deployed. Path: $deployPath  Status: $((Get-Service $serviceName).Status)" -ForegroundColor Green
