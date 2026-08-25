[CmdletBinding()]
param(
    [string] $ScreenshotRoot = (Join-Path $PSScriptRoot '..\packaging\windows\store-listing\screenshots'),

    [ValidateSet('en-US', 'tr-TR', 'ar-SA')]
    [string[]] $Locales = @('en-US', 'tr-TR')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$isWindowsPlatform = $PSVersionTable.PSEdition -ne 'Core' -or $IsWindows
if (-not $isWindowsPlatform) {
    throw 'Microsoft Store screenshot validation can only run on Windows.'
}

Add-Type -AssemblyName System.Drawing

$expectedFiles = @(
    '01-request-builder.png',
    '02-collections-environments.png',
    '03-collection-runner.png',
    '04-git-workflow.png',
    '05-settings-support.png'
)
$maximumFileSize = 50MB
$minimumWidth = 1366
$minimumHeight = 768
$resolvedScreenshotRoot = [System.IO.Path]::GetFullPath($ScreenshotRoot)
$expectedLocales = @($Locales | Select-Object -Unique)

if ($expectedLocales.Count -ne $Locales.Count) {
    throw 'Screenshot locales must not contain duplicate values.'
}

foreach ($locale in $expectedLocales) {
    $localeDirectory = Join-Path $resolvedScreenshotRoot $locale
    if (-not (Test-Path -LiteralPath $localeDirectory -PathType Container)) {
        throw "Missing Microsoft Store screenshot directory: $localeDirectory"
    }

    $screenshots = @(Get-ChildItem -LiteralPath $localeDirectory -Filter '*.png' -File)
    if ($screenshots.Count -lt 4 -or $screenshots.Count -gt 10) {
        throw "$locale must contain between 4 and 10 PNG screenshots; found $($screenshots.Count)."
    }

    foreach ($expectedFile in $expectedFiles) {
        $path = Join-Path $localeDirectory $expectedFile
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing required $locale screenshot: $expectedFile"
        }

        $file = Get-Item -LiteralPath $path
        if ($file.Length -gt $maximumFileSize) {
            throw "$locale screenshot exceeds the 50 MB Store limit: $expectedFile"
        }

        $image = [System.Drawing.Image]::FromFile($file.FullName)
        try {
            if ($image.Width -lt $minimumWidth -or $image.Height -lt $minimumHeight) {
                throw "$locale screenshot must be at least 1366 x 768: $expectedFile is $($image.Width) x $($image.Height)."
            }
        }
        finally {
            $image.Dispose()
        }
    }
}

Write-Host "Validated Microsoft Store screenshots for $($expectedLocales -join ', ') in $resolvedScreenshotRoot"
