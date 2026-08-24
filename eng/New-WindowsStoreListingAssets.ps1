[CmdletBinding()]
param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\packaging\windows\store-listing\assets')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$isWindowsPlatform = $PSVersionTable.PSEdition -ne 'Core' -or $IsWindows
if (-not $isWindowsPlatform) {
    throw 'Microsoft Store listing artwork can only be generated on Windows.'
}

Add-Type -AssemblyName System.Drawing

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null

$size = 300
$bitmap = [System.Drawing.Bitmap]::new($size, $size)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

try {
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.Clear([System.Drawing.Color]::FromArgb(15, 23, 42))

    $mintBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(52, 211, 153))
    $darkBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(15, 23, 42))
    $font = [System.Drawing.Font]::new(
        'Segoe UI',
        112,
        [System.Drawing.FontStyle]::Bold,
        [System.Drawing.GraphicsUnit]::Pixel)
    $centered = [System.Drawing.StringFormat]::new()

    try {
        $centered.Alignment = [System.Drawing.StringAlignment]::Center
        $centered.LineAlignment = [System.Drawing.StringAlignment]::Center

        $markBounds = [System.Drawing.RectangleF]::new(54, 54, 192, 192)
        $graphics.FillEllipse($mintBrush, $markBounds)
        $letterBounds = [System.Drawing.RectangleF]::new(54, 45, 192, 192)
        $graphics.DrawString('R', $font, $darkBrush, $letterBounds, $centered)
    }
    finally {
        $centered.Dispose()
        $font.Dispose()
        $darkBrush.Dispose()
        $mintBrush.Dispose()
    }

    $destination = Join-Path $resolvedOutputDirectory 'ReqMint-Store-Tile-300x300.png'
    $bitmap.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Host "Generated ReqMint Microsoft Store listing artwork in $resolvedOutputDirectory"
