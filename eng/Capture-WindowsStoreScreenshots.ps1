[CmdletBinding()]
param(
    [string] $OutputRoot = (Join-Path $PSScriptRoot '..\artifacts\store-capture\approved'),

    [string] $BuildRoot = (Join-Path $PSScriptRoot '..\artifacts\store-capture\build')
)

$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw 'Microsoft Store screenshots can only be captured on Windows.'
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
$captureRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot 'artifacts\store-capture'))
$captureRootPrefix = $captureRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
foreach ($candidate in @($resolvedOutputRoot, $resolvedBuildRoot)) {
    $candidatePrefix = $candidate.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidatePrefix.StartsWith(
        $captureRootPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Store capture paths must remain inside $captureRoot."
    }
}
$projectPath = Join-Path $repositoryRoot 'src\ReqMint.App\ReqMint.App.csproj'
$executablePath = Join-Path $resolvedBuildRoot 'ReqMint.App.exe'

dotnet publish $projectPath `
    --configuration Release `
    --no-restore `
    --output $resolvedBuildRoot `
    -p:StoreCaptureMode=true `
    -p:UseAppHost=true
if ($LASTEXITCODE -ne 0) {
    throw "The Store screenshot build failed with exit code $LASTEXITCODE."
}

foreach ($locale in @('en-US', 'tr-TR')) {
    $localeOutput = Join-Path $resolvedOutputRoot $locale
    $localeData = Join-Path $resolvedOutputRoot ".data-$locale"
    if (Test-Path -LiteralPath $localeOutput) {
        Remove-Item -LiteralPath $localeOutput -Recurse -Force
    }
    if (Test-Path -LiteralPath $localeData) {
        Remove-Item -LiteralPath $localeData -Recurse -Force
    }
    New-Item -ItemType Directory -Path $localeOutput -Force | Out-Null
    New-Item -ItemType Directory -Path $localeData -Force | Out-Null

    $previousOutput = $env:REQMINT_STORE_CAPTURE_OUTPUT
    $previousLocale = $env:REQMINT_STORE_CAPTURE_LOCALE
    $previousData = $env:REQMINT_STORE_CAPTURE_DATA
    try {
        $env:REQMINT_STORE_CAPTURE_OUTPUT = $localeOutput
        $env:REQMINT_STORE_CAPTURE_LOCALE = $locale
        $env:REQMINT_STORE_CAPTURE_DATA = $localeData
        $captureProcess = Start-Process -FilePath $executablePath -PassThru
        if (-not $captureProcess.WaitForExit(120000)) {
            Stop-Process -Id $captureProcess.Id -Force
            throw "The $locale Store screenshot process exceeded the two-minute limit."
        }
        if ($captureProcess.ExitCode -ne 0) {
            throw "The $locale Store screenshot process failed with exit code $($captureProcess.ExitCode)."
        }
    }
    finally {
        $env:REQMINT_STORE_CAPTURE_OUTPUT = $previousOutput
        $env:REQMINT_STORE_CAPTURE_LOCALE = $previousLocale
        $env:REQMINT_STORE_CAPTURE_DATA = $previousData
    }
}

& (Join-Path $PSScriptRoot 'Test-WindowsStoreScreenshots.ps1') `
    -ScreenshotRoot $resolvedOutputRoot

Write-Host "Captured current Release Store screenshots in $resolvedOutputRoot"
