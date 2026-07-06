#Requires -Version 5.1
<#
.SYNOPSIS
  Builds and publishes the NC VizDash Excel VSTO add-in (setup.exe + .vsto).

.DESCRIPTION
  Output folder (default): <repo>\publish\
  Deliverables:
    - setup.exe          — prerequisite bootstrapper + add-in installer
    - NCVizDash.ExcelAddIn.vsto — deployment manifest (double-click to install)
    - Application Files\ — versioned add-in binaries

.PARAMETER Configuration
  MSBuild configuration (Release recommended).

.PARAMETER Version
  Four-part application version, e.g. 1.0.0.0

.PARAMETER OutputDir
  Folder for publish output. Use a path without spaces if MSBuild property parsing fails.

.PARAMETER SignManifests
  Sign ClickOnce manifests and setup.exe. Required for VSTO publish — create a test
  certificate first in Visual Studio (project → Properties → Signing → Create Test Certificate).

.EXAMPLE
  .\scripts\Publish-ExcelAddIn.ps1

.EXAMPLE
  .\scripts\Publish-ExcelAddIn.ps1 -Version 1.2.0.0 -OutputDir D:\dist\NCVizDash
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $Version = '1.0.0.0',

    [string] $OutputDir = '',

    [bool] $SignManifests = $true
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\NCVizDash.ExcelAddIn\NCVizDash.ExcelAddIn.csproj'

if (-not (Test-Path $project)) {
    throw "Project not found: $project"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    # ClickOnce/VSTO local installs are unreliable when the publish path contains spaces.
    if ($repoRoot -match '\s') {
        $OutputDir = 'C:\NCVizDash\publish'
        Write-Host 'Repo path contains spaces; publishing to C:\NCVizDash\publish to avoid VSTO load failures.' -ForegroundColor Yellow
    }
    else {
        $OutputDir = Join-Path $repoRoot 'publish'
    }
}

$msbuild = @(
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $msbuild) {
    throw 'MSBuild not found. Install Visual Studio with the Office/SharePoint development workload.'
}

# Avoid a trailing backslash before the closing quote (breaks MSBuild on paths with spaces).
$publishDir = $OutputDir.TrimEnd('\', '/')

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$publishProperty = if ($publishDir -eq (Join-Path $repoRoot 'publish')) {
    # Stable relative path from the add-in project to repo\publish.
    '..\..\publish'
} else {
    $publishDir
}

$signValue = if ($SignManifests) { 'true' } else { 'false' }

$pfx = Join-Path (Split-Path $project -Parent) 'NCVizDash.ExcelAddIn_TemporaryKey.pfx'
if ($SignManifests -and -not (Test-Path $pfx)) {
    Write-Host 'Signing certificate not found.' -ForegroundColor Yellow
    Write-Host ''
    Write-Host 'Quick fix (PowerShell, run once):' -ForegroundColor Cyan
    Write-Host '  .\scripts\Create-PublishCertificate.ps1'
    Write-Host ''
    Write-Host 'Or in Visual Studio:' -ForegroundColor Cyan
    Write-Host '  NCVizDash.ExcelAddIn - Properties - Signing - Create Test Certificate'
    Write-Host ''
    throw "Expected certificate file: $pfx"
}

Write-Host "Publishing NC VizDash Excel add-in..." -ForegroundColor Cyan
Write-Host "  Configuration : $Configuration"
Write-Host "  Version       : $Version"
Write-Host "  Output        : $publishDir"
Write-Host "  Sign manifests: $signValue"
Write-Host ""

& $msbuild $project `
    /t:VstoPublish `
    /p:Configuration=$Configuration `
    /p:Platform=x64 `
    "/p:PublishDir=$publishProperty" `
    "/p:PublishUrl=$publishProperty" `
    /p:ApplicationVersion=$Version `
    /p:BootstrapperEnabled=true `
    /p:GenerateManifests=true `
    /p:SignManifests=$signValue `
    /p:IsWebBootstrapper=false `
    /p:PublishWizardCompleted=true `
    /v:minimal

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed (exit code $LASTEXITCODE)."
}

$setupExe = Join-Path $publishDir 'setup.exe'
$vsto = Join-Path $publishDir 'NCVizDash.ExcelAddIn.vsto'

Write-Host ''
Write-Host 'Publish succeeded.' -ForegroundColor Green
if (Test-Path $setupExe) {
    Write-Host "  setup.exe : $setupExe"
}
if (Test-Path $vsto) {
    Write-Host "  manifest  : $vsto"
}
Write-Host ''
Write-Host 'Distribute the entire publish folder. End users run setup.exe (or the .vsto file).'
Write-Host 'Prerequisites: Excel 64-bit (required), VSTO Runtime, .NET Framework 4.8, WebView2 Runtime.'
Write-Host 'Install from a path without spaces. Uninstall older NC VizDash versions before reinstalling.'
