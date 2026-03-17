# Wrapper: runs deploy.ps1 -Target Prod (run as Administrator)
& (Join-Path $PSScriptRoot "deploy.ps1") -Target Prod
