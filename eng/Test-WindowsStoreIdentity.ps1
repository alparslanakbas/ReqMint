[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9.-]{3,50}$')]
    [string] $IdentityName,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $Publisher,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $PublisherDisplayName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($IdentityName -eq 'ReqMint.Development') {
    throw 'Development package identity cannot be used for Microsoft Store submission.'
}
if ($Publisher -eq 'CN=ReqMint Development') {
    throw 'Development publisher cannot be used for Microsoft Store submission.'
}
if ($Publisher -notmatch '^CN=.+') {
    throw 'Publisher must be copied exactly from Partner Center and begin with CN=.'
}
if ($IdentityName -ne $IdentityName.Trim() -or
    $Publisher -ne $Publisher.Trim() -or
    $PublisherDisplayName -ne $PublisherDisplayName.Trim()) {
    throw 'Microsoft Store identity values must not contain leading or trailing whitespace.'
}
if ($Publisher.Length -gt 255 -or $PublisherDisplayName.Length -gt 255) {
    throw 'Microsoft Store publisher values must not exceed 255 characters.'
}

Write-Host 'Validated non-development Microsoft Store package identity metadata.'
