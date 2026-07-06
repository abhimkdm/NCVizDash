#Requires -Version 5.1
<#
.SYNOPSIS
  Creates the VSTO ClickOnce test certificate used by Publish-ExcelAddIn.ps1.

.DESCRIPTION
  Generates NCVizDash.ExcelAddIn_TemporaryKey.pfx and updates the project file
  with SignManifests + ManifestCertificateThumbprint (same as Visual Studio
  "Create Test Certificate" on the Signing tab).
#>
[CmdletBinding()]
param(
    [string] $Password = 'ncvizdash'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $repoRoot 'src\NCVizDash.ExcelAddIn'
$projectFile = Join-Path $projectDir 'NCVizDash.ExcelAddIn.csproj'
$pfxPath = Join-Path $projectDir 'NCVizDash.ExcelAddIn_TemporaryKey.pfx'
$pfxName = 'NCVizDash.ExcelAddIn_TemporaryKey.pfx'

if (Test-Path $pfxPath) {
    Write-Host "Certificate already exists: $pfxPath" -ForegroundColor Yellow
    exit 0
}

Write-Host 'Creating VSTO ClickOnce test certificate...' -ForegroundColor Cyan

$cert = New-SelfSignedCertificate `
    -Type CodeSigning `
    -Subject 'CN=NC VizDash Dev' `
    -KeyUsage DigitalSignature `
    -FriendlyName 'NC VizDash Dev' `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -NotAfter (Get-Date).AddYears(5)

$securePwd = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePwd | Out-Null

$thumbprint = $cert.Thumbprint
Write-Host "  Thumbprint: $thumbprint"
Write-Host "  PFX file  : $pfxPath"

[xml]$proj = Get-Content $projectFile
$ns = New-Object System.Xml.XmlNamespaceManager($proj.NameTable)
$ns.AddNamespace('ms', 'http://schemas.microsoft.com/developer/msbuild/2003')

$root = $proj.Project
if (-not $root) { throw "Could not parse $projectFile" }

# SignManifests + thumbprint (first PropertyGroup without Condition, or add new group).
$signGroup = $proj.CreateElement('PropertyGroup')
$signManifests = $proj.CreateElement('SignManifests')
$signManifests.InnerText = 'true'
$thumb = $proj.CreateElement('ManifestCertificateThumbprint')
$thumb.InnerText = $thumbprint
$signGroup.AppendChild($signManifests) | Out-Null
$signGroup.AppendChild($thumb) | Out-Null

# Insert after first PropertyGroup.
$firstPg = $root.PropertyGroup | Select-Object -First 1
if ($firstPg) {
    [void]$root.InsertAfter($signGroup, $firstPg)
} else {
    [void]$root.AppendChild($signGroup)
}

# Add pfx as None item if missing.
$pfxItem = $root.ItemGroup.None | Where-Object { $_.Include -eq $pfxName }
if (-not $pfxItem) {
    $itemGroup = $proj.CreateElement('ItemGroup')
    $none = $proj.CreateElement('None')
    $none.SetAttribute('Include', $pfxName)
    $itemGroup.AppendChild($none) | Out-Null
    [void]$root.AppendChild($itemGroup)
}

$proj.Save($projectFile)

Write-Host ''
Write-Host 'Done. You can now run:' -ForegroundColor Green
Write-Host '  .\scripts\Publish-ExcelAddIn.ps1 -Version 1.2.0.0'
