<#
.SYNOPSIS
    Kills any running Hft.Runner processes to release file locks.
.DESCRIPTION
    Institutional standard for build remediation. Ensures no orphaned HFT processes
    are holding DLL locks before a rebuild.
#>

$ProcessName = "Hft.Runner"
$processes = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue

if ($processes) {
    Write-Host "[INSTITUTIONAL CLEANUP] Found $($processes.Count) running $ProcessName process(es). Terminating..." -ForegroundColor Yellow
    foreach ($p in $processes) {
        try {
            Stop-Process -Id $p.Id -Force -ErrorAction Stop
            Write-Host "  - Terminated PID: $($p.Id)" -ForegroundColor Green
        }
        catch {
            Write-Error "Failed to terminate PID: $($p.Id). You may need elevated permissions."
        }
    }
}
else {
    Write-Host "[INSTITUTIONAL CLEANUP] No $ProcessName processes running. Environment is clean." -ForegroundColor Gray
}
