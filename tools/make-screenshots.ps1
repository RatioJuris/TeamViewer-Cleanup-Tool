# Recreates docs/screenshots.
#
# Only use this on a computer WITHOUT TeamViewer. It creates harmless dummy traces in your own
# user profile (HKCU registry, AppData, Desktop), captures the window, removes the dummies with the
# tool itself, and then verifies that nothing is left.
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot
$exe = Join-Path $root "src\TeamViewerCleanup\bin\Release\net48\TeamViewerCleanupTool.exe"
$shots = Join-Path $root "docs\screenshots"
if (-not (Test-Path $exe)) { throw "Build first: dotnet build src\TeamViewerCleanup -c Release" }
New-Item -ItemType Directory -Force $shots | Out-Null

$scan = Start-Process $exe -ArgumentList "--scan" -Wait -PassThru -WindowStyle Hidden
if ($scan.ExitCode -ne 0) { throw "TeamViewer traces already exist on this computer. Run this script only where TeamViewer is absent." }

$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TeamViewer"
$desktop = [Environment]::GetFolderPath("DesktopDirectory")
$dummies = @("HKCU:\Software\TeamViewer", $uninstallKey, "$env:APPDATA\TeamViewer", "$env:LOCALAPPDATA\TeamViewer", "$desktop\TeamViewer.lnk")

function Shot([string]$Name) {
    Start-Process $exe -ArgumentList "--screenshot", (Join-Path $shots "$Name.png") -Wait -WindowStyle Hidden
    Write-Host "Captured $Name.png"
}

try {
    New-Item -Path "HKCU:\Software\TeamViewer" -Force | Out-Null
    New-ItemProperty $runKey -Name TeamViewer -Value '"C:\Program Files\TeamViewer\TeamViewer.exe" --minimize' -Force | Out-Null
    New-Item -Path $uninstallKey -Force | Out-Null
    Set-ItemProperty $uninstallKey -Name DisplayName -Value "TeamViewer"
    Set-ItemProperty $uninstallKey -Name DisplayVersion -Value "15.60.3"
    Set-ItemProperty $uninstallKey -Name UninstallString -Value '"C:\Program Files\TeamViewer\uninstall.exe"'
    New-Item -ItemType Directory -Force "$env:APPDATA\TeamViewer\Logs", "$env:LOCALAPPDATA\TeamViewer" | Out-Null
    Set-Content "$desktop\TeamViewer.lnk" ""

    Shot "installed"
    Remove-Item $uninstallKey -Recurse
    Shot "leftovers"

    $clean = Start-Process $exe -ArgumentList "--clean", "--yes" -Wait -PassThru -WindowStyle Hidden
    if ($clean.ExitCode -ne 0) { throw "Cleaning the dummies failed (exit code $($clean.ExitCode))." }
    Shot "clean"
}
finally {
    # Belt and braces: make sure no dummy is left behind, whatever happened above.
    foreach ($path in $dummies) { if (Test-Path $path) { Remove-Item $path -Recurse -Force } }
    Remove-ItemProperty $runKey -Name TeamViewer -ErrorAction SilentlyContinue
}

$left = @($dummies | Where-Object { Test-Path $_ })
if ($left.Count -gt 0 -or (Get-ItemProperty $runKey -Name TeamViewer -ErrorAction SilentlyContinue)) { throw "Dummies remain: $left" }
Write-Host "All dummy traces removed."
