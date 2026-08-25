Set-StrictMode -Version Latest

function Get-ReqMintArabicReviewSources {
    param([Parameter(Mandatory)][string] $RepositoryRoot)

    $fixedSources = @(
        'src\ReqMint.App\Localization\en.json',
        'src\ReqMint.App\Localization\ar.json',
        'packaging\windows\store-listing\ar-SA.json',
        'website\app\components\ArabicShell.tsx',
        'website\app\lib\docs-ar.ts'
    ) | ForEach-Object { Join-Path $RepositoryRoot $_ }

    $documentSources = Get-ChildItem `
        -LiteralPath (Join-Path $RepositoryRoot 'docs\localization\ar-SA') `
        -Filter '*.md' `
        -File
    $websiteSources = Get-ChildItem `
        -LiteralPath (Join-Path $RepositoryRoot 'website\app\ar') `
        -Recurse `
        -File `
        -Include '*.tsx', '*.ts'

    $sources = @($fixedSources) + @($documentSources.FullName) + @($websiteSources.FullName)
    $missingSources = @($sources | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
    if ($missingSources.Count -gt 0) {
        throw "Arabic review source is missing: $($missingSources -join ', ')"
    }

    return @($sources | Sort-Object -Unique)
}

function Get-ReqMintArabicReviewFingerprint {
    param([Parameter(Mandatory)][string] $RepositoryRoot)

    $resolvedRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
    $records = foreach ($source in Get-ReqMintArabicReviewSources -RepositoryRoot $resolvedRoot) {
        $relativePath = [System.IO.Path]::GetRelativePath($resolvedRoot, $source).Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
        "$relativePath|$hash"
    }

    $bytes = [System.Text.Encoding]::UTF8.GetBytes(($records -join "`n"))
    $fingerprint = [System.Security.Cryptography.SHA256]::HashData($bytes)
    return [Convert]::ToHexString($fingerprint).ToLowerInvariant()
}

function Assert-ReqMintArabicReviewEvidence {
    param(
        [Parameter(Mandatory)][string] $RepositoryRoot,
        [Parameter(Mandatory)][string] $EvidencePath
    )

    $resolvedEvidencePath = [System.IO.Path]::GetFullPath($EvidencePath)
    if (-not (Test-Path -LiteralPath $resolvedEvidencePath -PathType Leaf)) {
        throw "Arabic review evidence was not found: $resolvedEvidencePath"
    }

    $evidence = Get-Content -LiteralPath $resolvedEvidencePath -Raw | ConvertFrom-Json
    if ($evidence.locale -ne 'ar-SA') {
        throw 'Arabic review evidence locale must be ar-SA.'
    }
    if ($evidence.decision -ne 'approved') {
        throw 'Arabic review evidence decision must be approved.'
    }
    if ([string]::IsNullOrWhiteSpace([string]$evidence.reviewerName)) {
        throw 'Arabic review evidence must identify the native reviewer.'
    }

    try {
        [DateTimeOffset]::Parse([string]$evidence.reviewedAtUtc) | Out-Null
    }
    catch {
        throw 'Arabic review evidence must contain a valid reviewedAtUtc value.'
    }

    $currentFingerprint = Get-ReqMintArabicReviewFingerprint -RepositoryRoot $RepositoryRoot
    if ($evidence.sourceFingerprint -ne $currentFingerprint) {
        throw 'Arabic review evidence does not match the current application, Store, documentation, and website sources.'
    }

    $requiredChecks = @(
        'allApplicationStringsReviewed',
        'storeListingReviewed',
        'documentationReviewed',
        'websiteReviewed',
        'rtlAndBidirectionalBehaviorReviewed',
        'truncationReviewed',
        'screenReaderNamesReviewed'
    )
    foreach ($check in $requiredChecks) {
        $property = $evidence.checks.PSObject.Properties[$check]
        if ($null -eq $property -or $property.Value -ne $true) {
            throw "Arabic review evidence check is not approved: $check"
        }
    }

    if ([string]::IsNullOrWhiteSpace([string]$evidence.reviewedStringsFile)) {
        throw 'Arabic review evidence must reference the reviewed strings CSV file.'
    }
    $evidenceDirectory = [System.IO.Path]::GetDirectoryName($resolvedEvidencePath)
    $stringsPath = [System.IO.Path]::GetFullPath((Join-Path $evidenceDirectory $evidence.reviewedStringsFile))
    $directoryPrefix = $evidenceDirectory.TrimEnd('\') + '\'
    if (-not $stringsPath.StartsWith($directoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The reviewed strings CSV must remain inside the review evidence directory.'
    }
    if (-not (Test-Path -LiteralPath $stringsPath -PathType Leaf)) {
        throw "Reviewed strings CSV was not found: $stringsPath"
    }

    $rows = @(Import-Csv -LiteralPath $stringsPath)
    $expectedCount = (Get-Content `
        -LiteralPath (Join-Path $RepositoryRoot 'src\ReqMint.App\Localization\ar.json') `
        -Raw | ConvertFrom-Json).PSObject.Properties.Name.Count
    if ($rows.Count -ne $expectedCount) {
        throw "Reviewed strings CSV must contain $expectedCount rows; found $($rows.Count)."
    }
    $unapprovedRows = @($rows | Where-Object { $_.ReviewerStatus -ne 'approved' })
    if ($unapprovedRows.Count -gt 0) {
        throw "Every Arabic application string must be approved; $($unapprovedRows.Count) rows remain."
    }

    $stringsHash = (Get-FileHash -LiteralPath $stringsPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($evidence.reviewedStringsSha256 -ne $stringsHash) {
        throw 'Arabic review evidence hash does not match the reviewed strings CSV.'
    }

    return $evidence
}
