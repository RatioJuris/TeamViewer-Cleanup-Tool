# Builds every release file into .\dist:
#   TeamViewerCleanupTool-<version>-portable.exe   run directly, nothing to install
#   TeamViewerCleanupTool-<version>-setup.exe      EXE installer (Inno Setup)
#   TeamViewerCleanupTool-<version>-x64.msi        MSI installer (WiX Toolset v5)
#   SHA256SUMS.txt                                 checksums
#
# Requirements: .NET SDK 8 or later, Inno Setup 6 (ISCC.exe).
param(
    [string]$Version = "",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not $Version) {
    $Version = ([xml](Get-Content "src\TeamViewerCleanup\TeamViewerCleanup.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}
Write-Host "Building TeamViewer Cleanup Tool $Version" -ForegroundColor Cyan

function Invoke-Step([string]$Name, [scriptblock]$Action) {
    Write-Host "`n== $Name" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit code $LASTEXITCODE)" }
}

$dist = Join-Path $PSScriptRoot "dist"
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory $dist | Out-Null

if (-not $SkipTests) {
    Invoke-Step "Tests" { dotnet test TeamViewerCleanup.sln -c Release --nologo }
}

Invoke-Step "App" { dotnet build src\TeamViewerCleanup -c Release --nologo -p:Version=$Version }
$appDir = (Resolve-Path "src\TeamViewerCleanup\bin\Release\net48").Path
Copy-Item "$appDir\TeamViewerCleanupTool.exe" "$dist\TeamViewerCleanupTool-$Version-portable.exe"

# The MSI licence page needs RTF.
$rtfPath = Join-Path $PSScriptRoot "installer\msi\obj\License.rtf"
New-Item -ItemType Directory -Force (Split-Path $rtfPath) | Out-Null
$escaped = (Get-Content LICENSE -Raw) -replace '\\', '\\' -replace '\{', '\{' -replace '\}', '\}' -replace "`r?`n", "\par`r`n"
Set-Content $rtfPath -Encoding Ascii -Value ("{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\fs18 " + $escaped + "}")

Invoke-Step "MSI installer" {
    dotnet build installer\msi\TeamViewerCleanupTool.Msi.wixproj -c Release --nologo `
        -p:Version=$Version -p:AppDir="$appDir" -p:LicenseRtf="$rtfPath"
}
$msi = Get-ChildItem "installer\msi\bin" -Recurse -Filter "TeamViewerCleanupTool-$Version-x64.msi" | Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $msi) { throw "The MSI was not produced." }
Copy-Item $msi.FullName $dist

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 (ISCC.exe) was not found. Install it from https://jrsoftware.org/isinfo.php" }

Invoke-Step "EXE installer" {
    & $iscc /Q "/DAppVersion=$Version" "/DAppDir=$appDir" "/DRepoRoot=$PSScriptRoot" "/DOutDir=$dist" installer\inno\TeamViewerCleanupTool.iss
}

Write-Host "`n== Checksums" -ForegroundColor Cyan
$sums = Get-ChildItem $dist -File | Sort-Object Name | ForEach-Object {
    "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
Set-Content "$dist\SHA256SUMS.txt" -Value $sums -Encoding Ascii

Get-ChildItem $dist | Format-Table Name, @{ Name = "Size"; Expression = { "{0:N0} KB" -f ($_.Length / 1KB) } } -AutoSize
