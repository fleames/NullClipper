# Render CWS-sized 24-bit PNGs from store/screenshots/src (1280x800 and 440x280).
# Usage: powershell -File store/screenshots/render.ps1

$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$repo = Split-Path (Split-Path $here -Parent) -Parent
$chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
if (-not (Test-Path $chrome)) {
  throw "Chrome not found at $chrome"
}

Add-Type -AssemblyName System.Drawing

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

  $srcImg = [System.Drawing.Image]::FromFile($raw)
  try {
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
      $g = [System.Drawing.Graphics]::FromImage($bmp)
      $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $g.Clear([System.Drawing.Color]::FromArgb(11, 15, 20))
      $g.DrawImage($srcImg, 0, 0, $Width, $Height)
      $g.Dispose()
      $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
      $bmp.Dispose()
    }
  } finally {
    $srcImg.Dispose()
    Remove-Item $raw -Force -ErrorAction SilentlyContinue
    Remove-Item $profile -Recurse -Force -ErrorAction SilentlyContinue
  }

  $check = [System.Drawing.Image]::FromFile($out)
  try {
    Write-Host "Wrote $out ($($check.Width)x$($check.Height) $($check.PixelFormat))"
  } finally {
    $check.Dispose()
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
  if (-not $ready) { throw 'Local screenshot server did not start on port 8765.' }

  Save-StorePng -HtmlName 'popup.html' -OutName 'popup.png' -Width 1280 -Height 800
  Save-StorePng -HtmlName 'overlay.html' -OutName 'overlay.png' -Width 1280 -Height 800
  Save-StorePng -HtmlName 'settings.html' -OutName 'settings.png' -Width 1280 -Height 800
  Save-StorePng -HtmlName 'promo.html' -OutName 'promo-440x280.png' -Width 440 -Height 280
} finally {
  if ($server -and -not $server.HasExited) {
    Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
  }
}
