<#
.SYNOPSIS
    Build, deploy, and optionally run xContextGuard.
.DESCRIPTION
    Builds xContextGuard in Release mode (default) and copies the compiled
    binaries to %LOCALAPPDATA%\xPrograms\xContextGuard.
#>
[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [switch]$Clean,
    [switch]$Run
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\xContextGuard\xContextGuard.csproj"
$deployDir = (Join-Path $env:LOCALAPPDATA "xPrograms\xContextGuard")

if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Error "The 'dotnet' CLI was not found in PATH."
    exit 1
}

if ($Clean) {
    Write-Host "Cleaning bin/obj..." -ForegroundColor Yellow
    foreach ($dir in "bin", "obj") {
        $p = Join-Path $repoRoot "src\xContextGuard\$dir"
        if (Test-Path $p) { Remove-Item -Path $p -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

Write-Host "Building xContextGuard ($Configuration)..." -ForegroundColor Cyan
& dotnet build $projectPath -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

$outDir = Join-Path $repoRoot "src\xContextGuard\bin\$Configuration\net10.0-windows"
if (-not (Test-Path (Join-Path $outDir "xContextGuard.exe"))) {
    Write-Error "Build output not found in $outDir"
    exit 1
}

if ($Configuration -eq "Release") {
    Get-Process -Name "xContextGuard" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 300

    New-Item -ItemType Directory -Path $deployDir -Force | Out-Null
    Copy-Item -Path "$outDir\*" -Destination $deployDir -Recurse -Force
    Write-Host "Deployed Release binaries to $deployDir" -ForegroundColor Green
}

if ($Run) {
    $exeToRun = if ($Configuration -eq "Release") { Join-Path $deployDir "xContextGuard.exe" } else { Join-Path $outDir "xContextGuard.exe" }
    Start-Process -FilePath $exeToRun
}

