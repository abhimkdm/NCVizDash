#Requires -Version 5.1
<#
.SYNOPSIS
  Reports whether installed Excel is 32-bit or 64-bit.

.DESCRIPTION
  NC VizDash is built for x64 (amd64). If Excel is 32-bit, VSTO fails with HRESULT 0x80131047
  ("The given assembly name or codebase was invalid") when loading NCVizDash.ExcelAddIn.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$candidates = @(
    "${env:ProgramW6432}\Microsoft Office\root\Office16\EXCEL.EXE",
    "${env:ProgramW6432}\Microsoft Office\Office16\EXCEL.EXE",
    "${env:ProgramFiles(x86)}\Microsoft Office\root\Office16\EXCEL.EXE",
    "${env:ProgramFiles(x86)}\Microsoft Office\Office16\EXCEL.EXE"
) | Where-Object { Test-Path $_ }

if (-not $candidates) {
    Write-Host 'Excel not found in standard Office install locations.' -ForegroundColor Red
    exit 1
}

foreach ($excel in $candidates) {
    $bitness = if ($excel -like '*Program Files (x86)*') { '32-bit' } else { '64-bit' }
    $ok = $bitness -eq '64-bit'
    $color = if ($ok) { 'Green' } else { 'Red' }
    Write-Host "$bitness Excel: $excel" -ForegroundColor $color
}

$has64 = $candidates | Where-Object { $_ -notlike '*Program Files (x86)*' }
if (-not $has64) {
    Write-Host ''
    Write-Host 'NC VizDash requires 64-bit Excel. Install Office 64-bit or use the 64-bit Excel shortcut.' -ForegroundColor Red
    exit 2
}

Write-Host ''
Write-Host '64-bit Excel is available. NC VizDash can load in that host.' -ForegroundColor Green
