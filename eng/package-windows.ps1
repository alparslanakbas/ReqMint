[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')]
    [string] $Architecture = 'x64',

    [ValidatePattern('^[1-9][0-9]{0,4}(\.[0-9]{1,5}){2}\.0$')]
    [string] $Version = '1.0.0.0',

    [ValidatePattern('^[A-Za-z0-9.-]{3,50}$')]
    [string] $IdentityName = 'ReqMint.Development',

    [ValidateNotNullOrEmpty()]
    [string] $Publisher = 'CN=ReqMint Development',

    [ValidateNotNullOrEmpty()]
    [string] $PublisherDisplayName = 'ReqMint',

    [string] $OutputDirectory,

    [switch] $LayoutOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$isWindowsPlatform = $PSVersionTable.PSEdition -ne 'Core' -or $IsWindows
if (-not $isWindowsPlatform) {
    throw 'MSIX packages must be prepared on Windows.'
}

$versionParts = $Version.Split('.') | ForEach-Object { [int]$_ }
if ($versionParts | Where-Object { $_ -gt 65535 }) {
    throw 'Every MSIX version component must be between 0 and 65535.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'src\ReqMint.App\ReqMint.App.csproj'
$manifestTemplatePath = Join-Path $repositoryRoot 'packaging\windows\AppxManifest.xml.in'
$assetGeneratorPath = Join-Path $PSScriptRoot 'New-WindowsPackageAssets.ps1'
$packageToolsPath = Join-Path $PSScriptRoot 'WindowsPackageTools.ps1'
. $packageToolsPath

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\packages\windows'
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$workingDirectory = Join-Path $repositoryRoot "artifacts\msix\$Architecture"
$layoutDirectory = Join-Path $workingDirectory 'layout'
$publishDirectory = Join-Path $workingDirectory 'publish'
$dotnetArtifactsDirectory = Join-Path $workingDirectory 'dotnet'
$assetsDirectory = Join-Path $layoutDirectory 'Assets'

if (Test-Path -LiteralPath $workingDirectory) {
    Remove-Item -LiteralPath $workingDirectory -Recurse -Force
}

[System.IO.Directory]::CreateDirectory($layoutDirectory) | Out-Null
[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null

$runtimeIdentifier = "win-$Architecture"
$publishArguments = @(
    'publish',
    $projectPath,
    '--configuration', 'Release',
    '--runtime', $runtimeIdentifier,
    '--self-contained', 'true',
    '--output', $publishDirectory,
    '--artifacts-path', $dotnetArtifactsDirectory,
    '-p:PublishSingleFile=false',
    '-p:DebugSymbols=false',
    '-p:DebugType=None',
    '-p:UseAppHost=true'
)

& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Copy-Item -Path (Join-Path $publishDirectory '*') -Destination $layoutDirectory -Recurse -Force
& $assetGeneratorPath -OutputDirectory $assetsDirectory

$manifest = [System.IO.File]::ReadAllText($manifestTemplatePath)
$manifest = $manifest.Replace('{{IDENTITY_NAME}}', [System.Security.SecurityElement]::Escape($IdentityName))
$manifest = $manifest.Replace('{{PUBLISHER}}', [System.Security.SecurityElement]::Escape($Publisher))
$manifest = $manifest.Replace('{{PUBLISHER_DISPLAY_NAME}}', [System.Security.SecurityElement]::Escape($PublisherDisplayName))
$manifest = $manifest.Replace('{{VERSION}}', $Version)
$manifest = $manifest.Replace('{{ARCHITECTURE}}', $Architecture)

if ($manifest.Contains('{{')) {
    throw 'The generated manifest still contains an unresolved token.'
}

$manifestPath = Join-Path $layoutDirectory 'AppxManifest.xml'
$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($manifestPath, $manifest, $utf8WithoutBom)
[xml](Get-Content -LiteralPath $manifestPath -Raw) | Out-Null

if ($LayoutOnly) {
    Write-Host "Prepared MSIX layout: $layoutDirectory"
    return
}

$makeAppxPath = Get-ReqMintMakeAppxPath

$packagePath = Join-Path $resolvedOutputDirectory "ReqMint_$Version`_$Architecture.msix"
& $makeAppxPath pack /d $layoutDirectory /p $packagePath /o
if ($LASTEXITCODE -ne 0) {
    throw "MakeAppx failed with exit code $LASTEXITCODE."
}

Write-Host "Created unsigned Microsoft Store package: $packagePath"
