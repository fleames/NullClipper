# Render CWS-sized 24-bit PNGs (no alpha, no DPI metadata).
# Usage: powershell -File store/screenshots/render.ps1

$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$repo = Split-Path (Split-Path $here -Parent) -Parent
$chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
$encode = Join-Path $here 'encode.py'
if (-not (Test-Path $chrome)) {
  throw "Chrome not found at $chrome"
}

function Save-StorePng {
  param(
    [string]$HtmlName,
    [string]$OutName,
    [int]$Width,
    [int]$Height
  )
  $url = "http://127.0.0.1:8765/store/screenshots/src/$HtmlName`?t=$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
  $raw = Join-Path $env:TEMP ("nullclipper-" + [guid]::NewGuid().ToString('N') + '.png')
  $out = Join-Path $here $OutName
  $profile = Join-Path $env:TEMP ('nc-chrome-' + [guid]::NewGuid().ToString('N'))
  $chromeArgs = @(
    '--headless=new',
    '--disable-gpu',
    '--hide-scrollbars',
    '--disable-cache',
    '--disk-cache-size=0',
    "--user-data-dir=$profile",
    '--force-device-scale-factor=1',
    "--window-size=$Width,$Height",
    "--screenshot=$raw",
    $url
  )
  & $chrome @chromeArgs | Out-Null
  if (-not (Test-Path $raw)) {
    throw "Chrome did not write $raw"
  }
  try {
    python $encode $raw $out $Width $Height
    if ($LASTEXITCODE -ne 0) { throw "encode.py failed for $OutName" }
  } finally {
    Remove-Item $raw -Force -ErrorAction SilentlyContinue
    Remove-Item $profile -Recurse -Force -ErrorAction SilentlyContinue
  }
}

$server = Start-Process -FilePath 'python' -ArgumentList @('-m', 'http.server', '8765', '--bind', '127.0.0.1') -WorkingDirectory $repo -WindowStyle Hidden -PassThru
try {
  $ready = $false
  foreach ($i in 1..20) {
    try {
      $probe = Invoke-WebRequest -Uri 'http://127.0.0.1:8765/store/screenshots/src/promo.html' -UseBasicParsing -TimeoutSec 2
      if ($probe.StatusCode -eq 200) { $ready = $true; break }
    } catch {
      Start-Sleep -Milliseconds 150
    }
  }
  if (-not ($ready)) { throw 'Local screenshot server did not start on port 8765.' }

  Save-StorePng -HtmlName 'popup.html' -OutName 'screenshot-1.png' -Width 1280 -Height 800
  Save-StorePng -HtmlName 'overlay.html' -OutName 'screenshot-2.png' -Width 1280 -Height 800
  Save-StorePng -HtmlName 'settings.html' -OutName 'screenshot-3.png' -Width 1280 -Height 800
  Save-StorePng -HtmlName 'promo.html' -OutName 'promo-small.png' -Width 440 -Height 280
  Save-StorePng -HtmlName 'promo-marquee.html' -OutName 'promo-marquee.png' -Width 1400 -Height 560
  Save-StorePng -HtmlName 'promo-opera.html' -OutName 'promo-opera-300x188.png' -Width 300 -Height 188
} finally {
  if ($server -and -not $server.HasExited) {
    Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
  }
}
