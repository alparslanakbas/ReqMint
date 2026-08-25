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

    [Parameter(Mandatory)]
    [ValidatePattern('^[1-9][0-9]{0,4}(\.[0-9]{1,5}){2}\.0$')]
    [string] $ExpectedVersion,

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

Add-Type -AssemblyName System.IO.Compression.FileSystem

$bundleArchive = [System.IO.Compression.ZipFile]::OpenRead($resolvedBundlePath)
try {
    $bundleManifestEntry = $bundleArchive.GetEntry('AppxMetadata/AppxBundleManifest.xml')
    if ($null -eq $bundleManifestEntry) {
        throw 'The MSIXBundle does not contain AppxMetadata/AppxBundleManifest.xml.'
    }

    $bundleManifestReader = [System.IO.StreamReader]::new($bundleManifestEntry.Open())
    try {
        [xml] $bundleManifest = $bundleManifestReader.ReadToEnd()
    }
    finally {
        $bundleManifestReader.Dispose()
    }

    $bundleIdentity = $bundleManifest.DocumentElement.SelectSingleNode(
        '*[local-name()="Identity"]')
    if ($null -eq $bundleIdentity) {
        throw 'The MSIXBundle manifest does not contain an Identity element.'
    }
    if ($bundleIdentity.GetAttribute('Name') -ne $IdentityName -or
        $bundleIdentity.GetAttribute('Publisher') -ne $Publisher -or
        $bundleIdentity.GetAttribute('Version') -ne $ExpectedVersion) {
        throw "The MSIXBundle identity or version does not match the Partner Center candidate."
    }

    $packageNodes = @($bundleManifest.DocumentElement.SelectNodes(
        '*[local-name()="Packages"]/*[local-name()="Package"]'))
    if ($packageNodes.Count -ne 2) {
        throw "Expected exactly two application packages in the MSIXBundle, but found $($packageNodes.Count)."
    }

    $architectures = @($packageNodes | ForEach-Object { $_.GetAttribute('Architecture') } | Sort-Object)
    if (($architectures -join ',') -ne 'arm64,x64') {
        throw "The MSIXBundle must contain exactly arm64 and x64 packages, but found: $($architectures -join ', ')."
    }

    foreach ($packageNode in $packageNodes) {
        $packageFileName = $packageNode.GetAttribute('FileName')
        $packageArchitecture = $packageNode.GetAttribute('Architecture')
        $packageVersion = $packageNode.GetAttribute('Version')
        if ($packageVersion -ne $ExpectedVersion) {
            throw "Package $packageFileName has version $packageVersion; expected $ExpectedVersion."
        }

        $packageEntry = $bundleArchive.GetEntry($packageFileName)
        if ($null -eq $packageEntry) {
            throw "The MSIXBundle references a missing package: $packageFileName."
        }

        $packageBuffer = [System.IO.MemoryStream]::new()
        $packageEntryStream = $packageEntry.Open()
        try {
            $packageEntryStream.CopyTo($packageBuffer)
        }
        finally {
            $packageEntryStream.Dispose()
        }
        $packageBuffer.Position = 0

        $packageArchive = [System.IO.Compression.ZipArchive]::new(
            $packageBuffer,
            [System.IO.Compression.ZipArchiveMode]::Read,
            $false)
        try {
            $packageManifestEntry = $packageArchive.GetEntry('AppxManifest.xml')
            if ($null -eq $packageManifestEntry) {
                throw "Package $packageFileName does not contain AppxManifest.xml."
            }

            $packageManifestReader = [System.IO.StreamReader]::new($packageManifestEntry.Open())
            try {
                [xml] $packageManifest = $packageManifestReader.ReadToEnd()
            }
            finally {
                $packageManifestReader.Dispose()
            }

            $packageIdentity = $packageManifest.DocumentElement.SelectSingleNode(
                '*[local-name()="Identity"]')
            $publisherDisplayNameNode = $packageManifest.DocumentElement.SelectSingleNode(
                '*[local-name()="Properties"]/*[local-name()="PublisherDisplayName"]')
            $runFullTrustNode = $packageManifest.DocumentElement.SelectSingleNode(
                '*[local-name()="Capabilities"]/*[local-name()="Capability" and @Name="runFullTrust"]')
            $desktopFamilyNode = $packageManifest.DocumentElement.SelectSingleNode(
                '*[local-name()="Dependencies"]/*[local-name()="TargetDeviceFamily" and @Name="Windows.Desktop"]')

            if ($null -eq $packageIdentity -or
                $packageIdentity.GetAttribute('Name') -ne $IdentityName -or
                $packageIdentity.GetAttribute('Publisher') -ne $Publisher -or
                $packageIdentity.GetAttribute('Version') -ne $ExpectedVersion -or
                $packageIdentity.GetAttribute('ProcessorArchitecture') -ne $packageArchitecture) {
                throw "Package $packageFileName identity metadata does not match the bundle candidate."
            }
            if ($null -eq $publisherDisplayNameNode -or
                $publisherDisplayNameNode.InnerText -ne $PublisherDisplayName) {
                throw "Package $packageFileName publisher display name does not match Partner Center."
            }
            if ($null -eq $runFullTrustNode) {
                throw "Package $packageFileName does not declare the required runFullTrust capability."
            }
            if ($null -eq $desktopFamilyNode) {
                throw "Package $packageFileName does not target Windows.Desktop."
            }
        }
        finally {
            $packageArchive.Dispose()
            $packageBuffer.Dispose()
        }
    }
}
finally {
    $bundleArchive.Dispose()
}

Write-Host "Validated MSIXBundle version $ExpectedVersion, identity, architectures, desktop target, publisher, and runFullTrust declaration."

& (Join-Path $PSScriptRoot 'Test-WindowsStoreScreenshots.ps1') `
    -ScreenshotRoot $ScreenshotRoot

$bundleHash = (Get-FileHash -LiteralPath $resolvedBundlePath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Private preview preflight passed for bundle SHA-256 $bundleHash."
Write-Host 'Partner Center submission, private-audience accounts, WACK results, and Store installation still require operator verification.'
