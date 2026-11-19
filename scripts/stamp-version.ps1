# Stamps one version number into every place that carries it.
#
# NullClipper has three version sources: VERSION (human/README), Clipper.csproj
# (the Windows binary) and extension/manifest.json (the store upload). They had
# drifted to 1.0.1 / 1.1.0 / 1.0.1 while the latest release was v1.1.2, and the
# Chrome Web Store rejects an upload whose manifest version it has already seen.
#
# Usage:
#   powershell -File scripts/stamp-version.ps1 -Version 1.1.3
#   powershell -File scripts/stamp-version.ps1 -Check    # verify only, exit 1 on drift
#
# The release workflow runs this with the tag's version before building, so the
# release tag is the single source of truth.

[CmdletBinding()]
param(
    [string]$Version,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$manifestPath = Join-Path $root 'extension/manifest.json'
$csprojPath = Join-Path $root 'Clipper.csproj'
$versionPath = Join-Path $root 'VERSION'

# Plain IndexOf arithmetic instead of regex: the patterns need quotes in them,
# and quoting inside the Windows PowerShell parser was more fragile than the
# regex was worth here.
function Get-JsonValue {
    param([string]$Text, [string]$Key)
    $needle = '"' + $Key + '"'
    $keyAt = $Text.IndexOf($needle)
    if ($keyAt -lt 0) { return $null }
    $openAt = $Text.IndexOf('"', $keyAt + $needle.Length)
    if ($openAt -lt 0) { return $null }
    $closeAt = $Text.IndexOf('"', $openAt + 1)
    if ($closeAt -lt 0) { return $null }
    return $Text.Substring($openAt + 1, $closeAt - $openAt - 1)
}

function Set-JsonValue {
    param([string]$Text, [string]$Key, [string]$NewValue)
    $needle = '"' + $Key + '"'
    $keyAt = $Text.IndexOf($needle)
    if ($keyAt -lt 0) { throw "No $Key key in extension/manifest.json." }
    $openAt = $Text.IndexOf('"', $keyAt + $needle.Length)
    $closeAt = $Text.IndexOf('"', $openAt + 1)
    return $Text.Substring(0, $openAt + 1) + $NewValue + $Text.Substring($closeAt)
}

function Get-TagValue {
    param([string]$Text, [string]$OpenTag, [string]$CloseTag)
    $openAt = $Text.IndexOf($OpenTag)
    if ($openAt -lt 0) { return $null }
    $valueAt = $openAt + $OpenTag.Length
    $closeAt = $Text.IndexOf($CloseTag, $valueAt)
    if ($closeAt -lt 0) { return $null }
    return $Text.Substring($valueAt, $closeAt - $valueAt)
}

function Set-TagValue {
    param([string]$Text, [string]$OpenTag, [string]$CloseTag, [string]$NewValue)
    $openAt = $Text.IndexOf($OpenTag)
    if ($openAt -lt 0) { throw "No $OpenTag element in Clipper.csproj." }
    $valueAt = $openAt + $OpenTag.Length
    $closeAt = $Text.IndexOf($CloseTag, $valueAt)
    return $Text.Substring(0, $valueAt) + $NewValue + $Text.Substring($closeAt)
}

$csprojText = Get-Content -Raw -LiteralPath $csprojPath
$manifestText = Get-Content -Raw -LiteralPath $manifestPath
$fileVersion = (Get-Content -Raw -LiteralPath $versionPath).Trim()
$csprojVersion = Get-TagValue -Text $csprojText -OpenTag '<Version>' -CloseTag '</Version>'
$manifestVersion = Get-JsonValue -Text $manifestText -Key 'version'

if ($Check) {
    $sources = @(
        [pscustomobject]@{ Source = 'VERSION'; Value = $fileVersion },
        [pscustomobject]@{ Source = 'Clipper.csproj'; Value = $csprojVersion },
        [pscustomobject]@{ Source = 'extension/manifest.json'; Value = $manifestVersion }
    )
    $sources | Format-Table -AutoSize | Out-String | Write-Host

    $unique = @($sources | ForEach-Object { $_.Value } | Select-Object -Unique)
    if ($unique.Count -ne 1) {
        Write-Host ("Version drift: VERSION={0} csproj={1} manifest={2}." -f $fileVersion, $csprojVersion, $manifestVersion)
        Write-Host 'Run: powershell -File scripts/stamp-version.ps1 -Version <x.y.z>'
        exit 1
    }
    Write-Host ("Versions agree: {0}" -f $fileVersion)
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw 'Pass -Version <x.y.z>, or -Check to verify without writing.'
}

$Version = $Version.Trim().TrimStart('v')
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    throw ("Version '{0}' is not x.y.z - Chrome and the .NET tooling both want three parts." -f $Version)
}

function Write-Utf8NoBom {
    param([string]$Path, [string]$Text)
    # Windows PowerShell's `-Encoding utf8` writes a BOM. A BOM in
    # extension/manifest.json is not something to bet a store upload on.
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $utf8)
}

Write-Utf8NoBom -Path $versionPath -Text $Version
Write-Utf8NoBom -Path $csprojPath -Text (Set-TagValue -Text $csprojText -OpenTag '<Version>' -CloseTag '</Version>' -NewValue $Version)
Write-Utf8NoBom -Path $manifestPath -Text (Set-JsonValue -Text $manifestText -Key 'version' -NewValue $Version)

Write-Host ("Stamped version {0} into VERSION, Clipper.csproj and extension/manifest.json." -f $Version)
