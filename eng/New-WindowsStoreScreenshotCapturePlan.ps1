[CmdletBinding()]
param(
    [ValidateSet('en-US', 'tr-TR', 'ar-SA')]
    [string] $Locale,

    [string] $OutputRoot = (Join-Path $PSScriptRoot '..\artifacts\store-capture'),

    [switch] $NativeReviewApproved
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$isWindowsPlatform = $PSVersionTable.PSEdition -ne 'Core' -or $IsWindows
if (-not $isWindowsPlatform) {
    throw 'Microsoft Store screenshots must be prepared on Windows.'
}

if ($Locale -eq 'ar-SA' -and -not $NativeReviewApproved) {
    throw 'Arabic capture requires native terminology and RTL approval. Re-run with -NativeReviewApproved only after that review is recorded.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$listingPath = Join-Path $repositoryRoot "packaging\windows\store-listing\$Locale.json"
if (-not (Test-Path -LiteralPath $listingPath -PathType Leaf)) {
    throw "Missing Microsoft Store listing source: $listingPath"
}

$listing = Get-Content -LiteralPath $listingPath -Raw | ConvertFrom-Json
$expectedFiles = @(
    '01-request-builder.png',
    '02-collections-environments.png',
    '03-collection-runner.png',
    '04-git-workflow.png',
    '05-settings-support.png'
)
$sceneInstructions = @(
    'Open the ReqMint Tutorial request, send it, and keep the successful response inspector visible.',
    'Show the tutorial collections together with the active Tutorial environment selector.',
    'Run the seeded tutorial collection and show assertion results without opening private iteration data.',
    'Use a disposable ReqMint-only Git repository and show the explicit review screen without remote credentials.',
    'Show appearance, language, background mode, and trusted support links in Settings.'
)
$captions = @($listing.screenshotCaptions)
if ($captions.Count -ne $expectedFiles.Count) {
    throw "$Locale must define exactly $($expectedFiles.Count) screenshot captions; found $($captions.Count)."
}

$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$localeDirectory = Join-Path $resolvedOutputRoot $Locale
[System.IO.Directory]::CreateDirectory($localeDirectory) | Out-Null

$scenes = for ($index = 0; $index -lt $expectedFiles.Count; $index++) {
    [ordered]@{
        order = $index + 1
        fileName = $expectedFiles[$index]
        caption = [string]$captions[$index]
        instruction = $sceneInstructions[$index]
        status = 'pending-real-capture'
    }
}

$plan = [ordered]@{
    product = 'ReqMint'
    locale = $Locale
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    nativeReviewApproved = [bool]$NativeReviewApproved
    sourceWorkspace = 'ReqMint Tutorial'
    requiredResolution = '1920x1080 preferred; 1366x768 minimum'
    safety = @(
        'Use only the disposable local tutorial workspace.',
        'Do not capture credentials, personal paths, customer workspaces, notifications, or other applications.',
        'Capture the real Release application UI; do not use mockups or generated images.',
        'Keep important controls in the upper two-thirds of the frame.'
    )
    scenes = $scenes
}

$planPath = Join-Path $localeDirectory 'capture-plan.json'
$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText(
    $planPath,
    ($plan | ConvertTo-Json -Depth 8),
    $utf8WithoutBom)

Write-Host "Prepared real-screenshot capture plan: $planPath"
Write-Host 'After capturing all five PNG files, validate this draft set with:'
Write-Host "./eng/Test-WindowsStoreScreenshots.ps1 -ScreenshotRoot '$resolvedOutputRoot' -Locales $Locale"
