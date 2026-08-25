[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string] $Repository = 'alparslanakbas/ReqMint',

    [Parameter(Mandatory)]
    [string] $IdentityName,

    [Parameter(Mandatory)]
    [string] $Publisher,

    [Parameter(Mandatory)]
    [string] $PublisherDisplayName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'Test-WindowsStoreIdentity.ps1') `
    -IdentityName $IdentityName `
    -Publisher $Publisher `
    -PublisherDisplayName $PublisherDisplayName

if (-not $WhatIfPreference) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw 'GitHub CLI is required to configure repository variables.'
    }

    & gh auth status | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'GitHub CLI is not authenticated.'
    }
}

$variables = [ordered]@{
    REQMINT_STORE_IDENTITY_NAME = $IdentityName
    REQMINT_STORE_PUBLISHER = $Publisher
    REQMINT_STORE_PUBLISHER_DISPLAY_NAME = $PublisherDisplayName
}

foreach ($variable in $variables.GetEnumerator()) {
    if ($PSCmdlet.ShouldProcess($Repository, "Set GitHub Actions variable $($variable.Key)")) {
        & gh variable set $variable.Key `
            --repo $Repository `
            --body $variable.Value
        if ($LASTEXITCODE -ne 0) {
            throw "GitHub variable could not be configured: $($variable.Key)"
        }
    }
}

if (-not $WhatIfPreference) {
    $configuredNames = @(& gh variable list --repo $Repository --json name --jq '.[].name')
    foreach ($requiredName in $variables.Keys) {
        if ($requiredName -notin $configuredNames) {
            throw "GitHub did not report the required Store variable after configuration: $requiredName"
        }
    }

    Write-Host "Configured and verified the three Microsoft Store identity variable names for $Repository without printing their values."
}
