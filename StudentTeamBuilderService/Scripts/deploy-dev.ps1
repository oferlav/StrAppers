# Wrapper: runs deploy.ps1 -Target Dev (run as Administrator)
& (Join-Path $PSScriptRoot "deploy.ps1") -Target Dev
