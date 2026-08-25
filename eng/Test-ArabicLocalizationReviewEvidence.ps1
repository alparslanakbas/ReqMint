[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $EvidencePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ArabicLocalizationReviewTools.ps1')

$evidence = Assert-ReqMintArabicReviewEvidence `
    -RepositoryRoot $repositoryRoot `
    -EvidencePath $EvidencePath

Write-Host "Validated native Arabic review by $($evidence.reviewerName) for fingerprint $($evidence.sourceFingerprint)."
