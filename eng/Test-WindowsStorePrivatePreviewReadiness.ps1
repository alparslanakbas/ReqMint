[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $BundlePath,

    [Parameter(Mandatory)]
    [string] $IdentityName,

    [Parameter(Mandatory)]
    [string] $Publisher,

    [Parameter(Mandatory)]
    [string] $PublisherDisplayName,

    [string] $ScreenshotRoot = (Join-Path $PSScriptRoot '..\packaging\windows\store-listing\screenshots'),

    [switch] $WebsiteAnonymousAccessVerified
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'Test-WindowsStoreIdentity.ps1') `
    -IdentityName $IdentityName `
    -Publisher $Publisher `
    -PublisherDisplayName $PublisherDisplayName

if (-not $WebsiteAnonymousAccessVerified) {
    throw 'Private preview readiness requires signed-out verification of website, privacy, and support URLs.'
}

$resolvedBundlePath = [System.IO.Path]::GetFullPath($BundlePath)
if (-not (Test-Path -LiteralPath $resolvedBundlePath -PathType Leaf)) {
    throw "Microsoft Store bundle was not found: $resolvedBundlePath"
}
if ([System.IO.Path]::GetExtension($resolvedBundlePath) -ne '.msixbundle') {
    throw 'Private preview requires the x64 and ARM64 .msixbundle artifact.'
}

& (Join-Path $PSScriptRoot 'Test-WindowsStoreScreenshots.ps1') `
    -ScreenshotRoot $ScreenshotRoot

$bundleHash = (Get-FileHash -LiteralPath $resolvedBundlePath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Private preview preflight passed for bundle SHA-256 $bundleHash."
Write-Host 'Partner Center submission, private-audience accounts, WACK results, and Store installation still require operator verification.'
