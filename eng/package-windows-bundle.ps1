[CmdletBinding()]
param(
    [ValidatePattern('^[1-9][0-9]{0,4}(\.[0-9]{1,5}){2}\.0$')]
    [string] $Version = '1.0.1.0',

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9.-]{3,50}$')]
    [string] $IdentityName,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $Publisher,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $PublisherDisplayName,

    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$isWindowsPlatform = $PSVersionTable.PSEdition -ne 'Core' -or $IsWindows
if (-not $isWindowsPlatform) {
    throw 'MSIX bundles must be prepared on Windows.'
}

if ($IdentityName -eq 'ReqMint.Development' -or $Publisher -eq 'CN=ReqMint Development') {
    throw 'A Store bundle requires the exact package identity and publisher values from Microsoft Partner Center.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageScriptPath = Join-Path $PSScriptRoot 'package-windows.ps1'
$packageToolsPath = Join-Path $PSScriptRoot 'WindowsPackageTools.ps1'
. $packageToolsPath

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\packages\windows-store'
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$bundleWorkingDirectory = Join-Path $repositoryRoot 'artifacts\msix\bundle'
$packageDirectory = Join-Path $bundleWorkingDirectory 'packages'

if (Test-Path -LiteralPath $bundleWorkingDirectory) {
    Remove-Item -LiteralPath $bundleWorkingDirectory -Recurse -Force
}

[System.IO.Directory]::CreateDirectory($packageDirectory) | Out-Null
[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null

foreach ($architecture in @('x64', 'arm64')) {
    & $packageScriptPath `
        -Version $Version `
        -Architecture $architecture `
        -IdentityName $IdentityName `
        -Publisher $Publisher `
        -PublisherDisplayName $PublisherDisplayName `
        -OutputDirectory $packageDirectory

    if ($LASTEXITCODE -ne 0) {
        throw "Creating the $architecture MSIX failed with exit code $LASTEXITCODE."
    }
}

$packages = @(Get-ChildItem -LiteralPath $packageDirectory -Filter '*.msix' -File)
if ($packages.Count -ne 2) {
    throw "Expected exactly two architecture packages, but found $($packages.Count)."
}

foreach ($architecture in @('x64', 'arm64')) {
    $expectedPackage = Join-Path $packageDirectory "ReqMint_$Version`_$architecture.msix"
    if (-not (Test-Path -LiteralPath $expectedPackage -PathType Leaf)) {
        throw "The expected $architecture package was not created: $expectedPackage"
    }
}

$makeAppxPath = Get-ReqMintMakeAppxPath
$bundlePath = Join-Path $resolvedOutputDirectory "ReqMint_$Version.msixbundle"
& $makeAppxPath bundle /d $packageDirectory /p $bundlePath /bv $Version /o
if ($LASTEXITCODE -ne 0) {
    throw "MakeAppx bundle failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $bundlePath -PathType Leaf)) {
    throw "MakeAppx did not create the expected bundle: $bundlePath"
}

Write-Host "Created unsigned Microsoft Store bundle: $bundlePath"
