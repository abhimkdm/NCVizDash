#Requires -Version 5.1
<#
.SYNOPSIS
  Removes stale NC VizDash registration and reinstalls from C:\NCVizDash\publish.

.DESCRIPTION
  NC VizDash is x64-only. This script fails fast if only 32-bit Excel is installed.
#>
[CmdletBinding()]
param(
    [string] $PublishDir = 'C:\NCVizDash\publish'
)

$ErrorActionPreference = 'Stop'

$excel64 = @(
    "${env:ProgramW6432}\Microsoft Office\root\Office16\EXCEL.EXE",
    "${env:ProgramW6432}\Microsoft Office\Office16\EXCEL.EXE"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $excel64) {
    Write-Host '64-bit Excel is not installed.' -ForegroundColor Red
    Write-Host ''
    Write-Host 'This add-in cannot load in 32-bit Excel (HRESULT 0x80131047).' -ForegroundColor Yellow
    Write-Host 'Install Office 64-bit, then run this script again.'
    Write-Host ''
    Write-Host 'In Excel: File -> Account -> About Excel should show "64-bit".'
    exit 2
}

Write-Host "64-bit Excel: $excel64" -ForegroundColor Green

$regKey = 'HKCU:\Software\Microsoft\Office\Excel\Addins\NCVizDash.ExcelAddIn'
if (Test-Path $regKey) {
    $manifest = (Get-ItemProperty $regKey).Manifest
    Write-Host "Removing stale registration: $manifest"
    Remove-Item $regKey -Force
}

$vsto = Join-Path $PublishDir 'NCVizDash.ExcelAddIn.vsto'
$setup = Join-Path $PublishDir 'setup.exe'

if (-not (Test-Path $vsto)) {
    Write-Host "Publish output not found at $PublishDir" -ForegroundColor Red
    Write-Host 'Run: .\scripts\Publish-ExcelAddIn.ps1 -Version 1.2.2.3'
    exit 1
}

Write-Host ''
Write-Host "Installing from $PublishDir ..." -ForegroundColor Cyan
if (Test-Path $setup) {
    Start-Process -FilePath $setup -Wait
}
else {
    Start-Process -FilePath $vsto -Wait
}

Write-Host ''
Write-Host 'Done. Start 64-bit Excel and check for the NC VizDash ribbon tab.' -ForegroundColor Green
