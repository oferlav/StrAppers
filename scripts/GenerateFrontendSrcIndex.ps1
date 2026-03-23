$root = Join-Path $PSScriptRoot '..\strAppersFrontend\src' | Resolve-Path
$jsx = Get-ChildItem -Path $root -Recurse -Filter '*.jsx' -File | Sort-Object FullName
$js  = Get-ChildItem -Path $root -Recurse -Filter '*.js' -File | Sort-Object FullName
$rootLen = $root.Path.Length + 1

$lines = @()
$lines += '# Frontend `src/` inventory (auto-generated)'
$lines += ''
$lines += '**Purpose:** On-disk listing of `strAppersFrontend/src` so agents/docs match your full tree. Cursor''s built-in file search sometimes returns only a subset of files.'
$lines += ''
$lines += 'Regenerate:'
$lines += '```powershell'
$lines += '.\scripts\GenerateFrontendSrcIndex.ps1'
$lines += '```'
$lines += ''
$lines += "## Counts"
$lines += "- **.jsx:** $($jsx.Count)"
$lines += "- **.js:** $($js.Count)"
$lines += ''
$lines += '## All .jsx files (relative to `strAppersFrontend/src/`)'
$lines += ''
foreach ($f in $jsx) {
  $rel = $f.FullName.Substring($rootLen).Replace('\', '/')
  $lines += "- $rel"
}
$lines += ''
$lines += '## All .js files (relative to `strAppersFrontend/src/`)'
$lines += ''
foreach ($f in $js) {
  $rel = $f.FullName.Substring($rootLen).Replace('\', '/')
  $lines += "- $rel"
}

$dest = (Join-Path (Split-Path $PSScriptRoot -Parent) 'docs\FRONTEND_SRC_FILE_INDEX.md')
Set-Content -Path $dest -Value ($lines -join "`n") -Encoding UTF8
Write-Host "Wrote $($jsx.Count) jsx + $($js.Count) js to $dest"
