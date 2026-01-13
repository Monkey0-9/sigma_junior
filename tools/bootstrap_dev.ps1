#!/usr/bin/env pwsh
# Grandmaster Developer Bootstrap Script
# Ensures clean state and builds the HFT platform

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  HFT Platform - Grandmaster Bootstrap" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Kill any running Hft.Runner processes
Write-Host "[1/5] Stopping any running Hft.Runner processes..." -ForegroundColor Yellow
$p = Get-Process -Name "Hft.Runner" -ErrorAction SilentlyContinue
if ($p) {
    $p | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "    Stopped Hft.Runner process" -ForegroundColor Green
}
Get-Process | Where-Object {
    $_.MainModule -and $_.MainModule.FileName -like "*\hft_platform\Hft.Runner\bin\Release\*"
} | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Host "    Process cleanup complete" -ForegroundColor Green

# Step 2: Clean stale lock files
Write-Host "[2/5] Cleaning stale lock files..." -ForegroundColor Yellow
$lockFiles = Get-ChildItem -Path ".\Hft.Runner\bin\Release\net8.0" -ErrorAction SilentlyContinue
if ($lockFiles) {
    Remove-Item -Path ".\Hft.Runner\bin\Release\net8.0\*" -Force -Recurse -ErrorAction SilentlyContinue
    Write-Host "    Cleaned bin/Release directory" -ForegroundColor Green
}

# Remove PID file
if (Test-Path ".runner.pid") {
    Remove-Item ".runner.pid" -Force
    Write-Host "    Removed .runner.pid" -ForegroundColor Green
}

# Step 3: Ensure data directories exist
Write-Host "[3/5] Ensuring data directories exist..." -ForegroundColor Yellow
$baseDir = Get-Location
$dataDirs = @("data/audit", "data/logs", "data/replay", "logs")
foreach ($dir in $dataDirs) {
    $fullPath = Join-Path $baseDir $dir
    if (-not (Test-Path $fullPath)) {
        New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
        Write-Host "    Created $dir" -ForegroundColor Green
    } else {
        Write-Host "    $dir already exists" -ForegroundColor Gray
    }
}

# Step 4: Clean and build
Write-Host "[4/5] Building HFT Platform..." -ForegroundColor Yellow
dotnet clean --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "    Clean completed with warnings" -ForegroundColor Gray
}

$buildResult = dotnet build `
    --no-restore `
    -c Release `
    --nologo `
    -p:TreatWarningsAsErrors=true `
    -v q

if ($buildResult -match "error" -or $buildResult -match "Error") {
    Write-Host "    Build FAILED - see errors above" -ForegroundColor Red
    exit 1
}
Write-Host "    Build succeeded" -ForegroundColor Green

# Step 5: Run with simulation mode
Write-Host "[5/5] Starting HFT Runner in simulation mode..." -ForegroundColor Yellow
Write-Host ""
Write-Host "    Configuration:" -ForegroundColor Cyan
Write-Host "      - Metrics Port: 9180 (unprivileged)" -ForegroundColor Gray
Write-Host "      - UDP Port: 5005" -ForegroundColor Gray
Write-Host "      - Mode: Simulation" -ForegroundColor Gray
Write-Host ""
Write-Host "    Press Ctrl+C to stop" -ForegroundColor Cyan
Write-Host ""

# Run the application
dotnet run --no-build -c Release -- --udp-port 5005 --metrics-port 9180

