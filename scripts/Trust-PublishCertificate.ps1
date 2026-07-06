#Requires -Version 5.1
<#
.SYNOPSIS
  Adds the NC VizDash dev publish certificate to Trusted Publishers (current user).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$pfxPath = Join-Path $PSScriptRoot '..\src\NCVizDash.ExcelAddIn\NCVizDash.ExcelAddIn_TemporaryKey.pfx'
if (-not (Test-Path $pfxPath)) {
    throw "Certificate not found. Run .\scripts\Create-PublishCertificate.ps1 first."
}

$pwd = ConvertTo-SecureString -String 'ncvizdash' -Force -AsPlainText
Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation 'Cert:\CurrentUser\TrustedPublisher' -Password $pwd | Out-Null

Write-Host 'Dev certificate added to Trusted Publishers (Current User).' -ForegroundColor Green
Write-Host 'Retry installing publish\NCVizDash.ExcelAddIn.vsto or setup.exe.'
