
$ErrorActionPreference = "Stop"

$baseDir = "c:\hft_platform"
$sourceDir = Join-Path $baseDir "Hft.Runner\bin\Debug\net8.0"
$shadowBase = Join-Path $baseDir "Hft.Runner\shadow_deploy"

# Ensure clean build first? Optional.
# dotnet build $baseDir\Hft.Runner -c Debug


$timestamp = (Get-Date).Ticks
$deployDir = Join-Path $shadowBase $timestamp

Write-Host "Creating shadow deployment at $deployDir" -ForegroundColor Cyan
New-Item -ItemType Directory -Path $deployDir -Force | Out-Null

# Copy files
Copy-Item -Path "$sourceDir\*" -Destination $deployDir -Recurse -Force

Write-Host "Launching Runner from shadow copy..." -ForegroundColor Green
$exe = Join-Path $deployDir "Hft.Runner.exe"

if (-not (Test-Path $exe)) {
    Write-Error "Runner executable not found at $exe"
}

# Start process
Start-Process -FilePath $exe -WorkingDirectory $deployDir -NoNewWindow -Wait
