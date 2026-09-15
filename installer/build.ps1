# Build NullClipper-Setup.exe with Inno Setup (ISCC).
# GitHub Actions installs Inno via Chocolatey. Locally, install Inno Setup 6 and re-run.

param(
  [string]$Version = ''
)

$ErrorActionPreference = 'Stop'
$installerDir = $PSScriptRoot
$repoRoot = Split-Path $installerDir -Parent
$iss = Join-Path $installerDir 'NullClipper.iss'
$publishDir = Join-Path $repoRoot 'dist\win-x64'

if (-not (Test-Path (Join-Path $publishDir 'NullClipper.exe'))) {
  throw "Publish the app first (dist/win-x64/NullClipper.exe is missing). From the repo root:`n  dotnet publish Clipper.csproj -c Release -r win-x64 --self-contained false -o dist/win-x64"
}

$pf86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
$candidates = @(
  $(if ($pf86) { Join-Path $pf86 'Inno Setup 6\ISCC.exe' }),
  $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe' }),
  $(if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe' })
) | Where-Object { $_ }
$fromPath = Get-Command iscc, ISCC -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
if ($fromPath) {
  $candidates = @($fromPath) + $candidates
}
$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
  Write-Warning 'ISCC.exe not found. Install Inno Setup 6 (free) or let the GitHub Release workflow build NullClipper-Setup.exe.'
  Write-Host 'Portable fallback: zip dist/win-x64 and run NullClipper.exe.'
  exit 2
}

$args = @()
if ($Version) {
  $args += "/DMyAppVersion=$Version"
}
$args += $iss
& $iscc @args
if ($LASTEXITCODE -ne 0) {
  throw "ISCC failed with exit code $LASTEXITCODE"
}

$setup = Join-Path $repoRoot 'dist\NullClipper-Setup.exe'
if (-not (Test-Path $setup)) {
  throw "Expected $setup"
}
Write-Host "Wrote $setup"
