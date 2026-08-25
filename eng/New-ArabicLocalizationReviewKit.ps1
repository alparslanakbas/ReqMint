[CmdletBinding()]
param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\localization-review\ar-SA')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ArabicLocalizationReviewTools.ps1')

$english = Get-Content `
    -LiteralPath (Join-Path $repositoryRoot 'src\ReqMint.App\Localization\en.json') `
    -Raw | ConvertFrom-Json
$arabic = Get-Content `
    -LiteralPath (Join-Path $repositoryRoot 'src\ReqMint.App\Localization\ar.json') `
    -Raw | ConvertFrom-Json
$englishKeys = @($english.PSObject.Properties.Name | Sort-Object)
$arabicKeys = @($arabic.PSObject.Properties.Name | Sort-Object)
if (Compare-Object $englishKeys $arabicKeys) {
    throw 'English and Arabic application resources must have identical keys before native review.'
}

$sourceFiles = Get-ChildItem `
    -LiteralPath (Join-Path $repositoryRoot 'src\ReqMint.App') `
    -Recurse `
    -File `
    -Include '*.axaml', '*.cs' | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
    }
$sourceContent = @{}
foreach ($sourceFile in $sourceFiles) {
    $sourceContent[$sourceFile.FullName] = Get-Content -LiteralPath $sourceFile.FullName -Raw
}

function Get-Category([string] $Key) {
    switch -Wildcard ($Key) {
        'Onboarding*' { return 'Onboarding' }
        'Tutorial*' { return 'Tutorial' }
        'Git*' { return 'Git' }
        'CollectionRun*' { return 'Collection runner' }
        'History*' { return 'History' }
        'Status*' { return 'Status and errors' }
        'Tooltip*' { return 'Tooltips and accessibility' }
        'Nav*' { return 'Navigation' }
        'About*' { return 'Settings and support' }
        default { return 'Workspace and request editor' }
    }
}

$rows = foreach ($key in $englishKeys) {
    $usageFiles = foreach ($entry in $sourceContent.GetEnumerator()) {
        if ($entry.Value.Contains($key, [StringComparison]::Ordinal)) {
            [System.IO.Path]::GetRelativePath($repositoryRoot, $entry.Key).Replace('\', '/')
        }
    }

    [pscustomobject][ordered]@{
        Key = $key
        Category = Get-Category $key
        English = [string]$english.PSObject.Properties[$key].Value
        Arabic = [string]$arabic.PSObject.Properties[$key].Value
        UsageFiles = (@($usageFiles | Sort-Object -Unique) -join '; ')
        ReviewerStatus = 'pending'
        ReviewerComment = ''
    }
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null
$stringsPath = Join-Path $resolvedOutputDirectory 'strings.csv'
$rows | Export-Csv -LiteralPath $stringsPath -NoTypeInformation -Encoding utf8

$fingerprint = Get-ReqMintArabicReviewFingerprint -RepositoryRoot $repositoryRoot
$evidenceTemplate = [ordered]@{
    locale = 'ar-SA'
    sourceFingerprint = $fingerprint
    reviewerName = ''
    reviewerRole = 'Native Arabic speaker experienced with developer tools'
    reviewedAtUtc = ''
    decision = 'pending'
    reviewedStringsFile = 'strings.csv'
    reviewedStringsSha256 = ''
    checks = [ordered]@{
        allApplicationStringsReviewed = $false
        storeListingReviewed = $false
        documentationReviewed = $false
        websiteReviewed = $false
        rtlAndBidirectionalBehaviorReviewed = $false
        truncationReviewed = $false
        screenReaderNamesReviewed = $false
    }
    reviewerNotes = ''
}
$evidencePath = Join-Path $resolvedOutputDirectory 'review-evidence.json'
$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText(
    $evidencePath,
    ($evidenceTemplate | ConvertTo-Json -Depth 6),
    $utf8WithoutBom)

Write-Host "Prepared $($rows.Count) Arabic application strings for native review: $stringsPath"
Write-Host "Prepared pending review evidence: $evidencePath"
Write-Host 'The reviewer must approve every CSV row, complete real RTL scenarios, hash the reviewed CSV, and then set the evidence decision to approved.'
