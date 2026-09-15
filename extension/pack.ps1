# Pack NullClipper as a store-ready MV3 zip (manifest.json at the zip root).
# Usage:  powershell -File extension/pack.ps1
# Output: dist/NullClipper-extension.zip

$ErrorActionPreference = 'Stop'

$extensionRoot = $PSScriptRoot
$repoRoot = Split-Path $extensionRoot -Parent
$distDir = Join-Path $repoRoot 'dist'
$zipPath = Join-Path $distDir 'NullClipper-extension.zip'
$stage = Join-Path ([System.IO.Path]::GetTempPath()) ('nullclipper-ext-' + [guid]::NewGuid().ToString('N'))

$excludeNames = @(
  'README.md',
  'pack.ps1',
  '.DS_Store',
  'Thumbs.db'
)

New-Item -ItemType Directory -Force -Path $distDir | Out-Null
if (Test-Path $zipPath) {
  Remove-Item $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $stage | Out-Null
try {
  Get-ChildItem -Path $extensionRoot -Force | Where-Object {
    $excludeNames -notcontains $_.Name
  } | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination (Join-Path $stage $_.Name) -Recurse -Force
  }

  $manifest = Join-Path $stage 'manifest.json'
  if (-not (Test-Path $manifest)) {
    throw "manifest.json missing from staged zip contents: $stage"
  }

  Add-Type -AssemblyName System.IO.Compression
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $zip = [System.IO.Compression.ZipFile]::Open($zipPath, 'Create')
  try {
    Get-ChildItem -Path $stage -Recurse -File | ForEach-Object {
      $relative = $_.FullName.Substring($stage.Length).TrimStart('\', '/')
      $entryName = $relative.Replace('\', '/')
      [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $zip,
        $_.FullName,
        $entryName,
        [System.IO.Compression.CompressionLevel]::Optimal
      )
    }
  } finally {
    $zip.Dispose()
  }

  $probe = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
  try {
    $names = $probe.Entries | ForEach-Object { $_.FullName }
    if ($names -notcontains 'manifest.json') {
      throw 'Zip is invalid: manifest.json must be at the root (not inside a subfolder).'
    }
  } finally {
    $probe.Dispose()
  }

  $item = Get-Item $zipPath
  Write-Host "Wrote $($item.FullName) ($([math]::Round($item.Length / 1KB, 1)) KB)"
} finally {
  Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
