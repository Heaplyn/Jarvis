# Developer: heaplyn
# Date: 2026-08-09
# Summary: Downloads and installs FlareSolverr Windows x64 binary into the local flaresolverr/ directory.

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$targetDir = Join-Path $scriptDir "flaresolverr"
$zipFile = Join-Path $scriptDir "flaresolverr.zip"
$url = "https://github.com/FlareSolverr/FlareSolverr/releases/download/v3.3.21/flaresolverr_windows_x64.zip"

Write-Host "⬇️ Downloading FlareSolverr v3.3.21..." -ForegroundColor Cyan
Invoke-WebRequest -Uri $url -OutFile $zipFile

Write-Host "📦 Extracting archive..." -ForegroundColor Cyan
if (!(Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir | Out-Null
}

Expand-Archive -Path $zipFile -DestinationPath $scriptDir -Force

$extractedDir = Join-Path $scriptDir "flaresolverr_windows_x64"
if (Test-Path $extractedDir) {
    Write-Host "🚚 Configuring installation paths..." -ForegroundColor Cyan
    Copy-Item -Path "$extractedDir\*" -Destination $targetDir -Recurse -Force
    Remove-Item -Path $extractedDir -Recurse -Force
}

Write-Host "🧹 Cleaning up temp files..." -ForegroundColor Cyan
Remove-Item -Path $zipFile -Force

Write-Host "FlareSolverr installed successfully in: $targetDir" -ForegroundColor Green

