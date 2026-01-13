#!/usr/bin/env pwsh
# Grandmaster Stop Runner Script
# Safely terminates all HFT Runner processes and releases file locks
# Used in CI pipelines to prevent file lock issues

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  HFT Platform - Stop Runner (Safety)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Kill Hft.Runner by name
Write-Host "[1/3] Killing Hft.Runner processes by name..." -ForegroundColor Yellow
$processes = Get-Process -Name "Hft.Runner" -ErrorAction SilentlyContinue
if ($processes) {
    $count = ($processes | Measure-Object).Count
    $processes | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "    Stopped $count Hft.Runner process(es)" -ForegroundColor Green
} else {
    Write-Host "    No Hft.Runner processes found" -ForegroundColor Gray
}

# Step 2: Kill any processes with module in hft_platform output directories
Write-Host "[2/3] Killing processes with hft_platform module..." -ForegroundColor Yellow
$lockedProcesses = Get-Process | Where-Object {
    try {
        $_.MainModule -and $_.MainModule.FileName -like "*\hft_platform\Hft.Runner\bin\Release\*"
    } catch {
        $false
    }
}
if ($lockedProcesses) {
    $count = ($lockedProcesses | Measure-Object).Count
    $lockedProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "    Stopped $count locked process(es)" -ForegroundColor Green
} else {
    Write-Host "    No locked processes found" -ForegroundColor Gray
}

# Step 3: Clean PID file and temporary locks
Write-Host "[3/3] Cleaning lock files..." -ForegroundColor Yellow
$pids = @(".runner.pid", "runner.pid")
foreach ($pidFile in $pids) {
    if (Test-Path $pidFile) {
        try {
            $content = Get-Content $pidFile -ErrorAction SilentlyContinue
            if ($content -and $content -match '\d+') {
                $pid = [int]$content
                $proc = Get-Process -Id $pid -ErrorAction SilentlyContinue
                if ($proc) {
                    $proc | Stop-Process -Force -ErrorAction SilentlyContinue
                    Write-Host "    Killed process $pid from $pidFile" -ForegroundColor Green
                }
            }
            Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
            Write-Host "    Removed $pidFile" -ForegroundColor Green
        } catch {
            Write-Host "    Could not clean $pidFile" -ForegroundColor Gray
        }
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  All HFT processes stopped safely" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

