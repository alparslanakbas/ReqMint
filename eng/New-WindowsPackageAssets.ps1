[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$isWindowsPlatform = $PSVersionTable.PSEdition -ne 'Core' -or $IsWindows
if (-not $isWindowsPlatform) {
    throw 'Windows package assets can only be generated on Windows.'
}

Add-Type -AssemblyName System.Drawing

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null

function New-ReqMintPackageAsset {
    param(
        [Parameter(Mandatory)]
        [string] $FileName,
        [Parameter(Mandatory)]
        [int] $Width,
        [Parameter(Mandatory)]
        [int] $Height,
        [switch] $IncludeWordmark
    )

    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $graphics.Clear([System.Drawing.Color]::FromArgb(15, 23, 42))

        $shortSide = [Math]::Min($Width, $Height)
        $markSize = if ($IncludeWordmark) { [int]($shortSide * 0.58) } else { [int]($shortSide * 0.72) }
        $markX = if ($IncludeWordmark) { [int]($Width * 0.08) } else { [int](($Width - $markSize) / 2) }
        $markY = [int](($Height - $markSize) / 2)

        $mintBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(52, 211, 153))
        $darkBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(15, 23, 42))
        $lightBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(241, 245, 249))

        try {
            $graphics.FillEllipse($mintBrush, $markX, $markY, $markSize, $markSize)

            $markFontSize = [Math]::Max(8, [single]($markSize * 0.58))
            $markFont = [System.Drawing.Font]::new('Segoe UI', $markFontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
            $centered = [System.Drawing.StringFormat]::new()

            try {
                $centered.Alignment = [System.Drawing.StringAlignment]::Center
                $centered.LineAlignment = [System.Drawing.StringAlignment]::Center
                $markRectangle = [System.Drawing.RectangleF]::new($markX, $markY - ($markSize * 0.04), $markSize, $markSize)
                $graphics.DrawString('R', $markFont, $darkBrush, $markRectangle, $centered)
            }
            finally {
                $centered.Dispose()
                $markFont.Dispose()
            }

            if ($IncludeWordmark) {
                $wordmarkFontSize = [Math]::Max(11, [single]($Height * 0.24))
                $wordmarkFont = [System.Drawing.Font]::new('Segoe UI', $wordmarkFontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

                try {
                    $wordmarkX = [single]($markX + $markSize + ($Width * 0.055))
                    $wordmarkY = [single](($Height - $wordmarkFont.GetHeight($graphics)) / 2)
                    $graphics.DrawString('ReqMint', $wordmarkFont, $lightBrush, $wordmarkX, $wordmarkY)
                }
                finally {
                    $wordmarkFont.Dispose()
                }
            }
        }
        finally {
            $mintBrush.Dispose()
            $darkBrush.Dispose()
            $lightBrush.Dispose()
        }

        $destination = Join-Path $resolvedOutputDirectory $FileName
        $bitmap.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

New-ReqMintPackageAsset -FileName 'StoreLogo.png' -Width 50 -Height 50
New-ReqMintPackageAsset -FileName 'Square44x44Logo.png' -Width 44 -Height 44
New-ReqMintPackageAsset -FileName 'Square150x150Logo.png' -Width 150 -Height 150
New-ReqMintPackageAsset -FileName 'Square310x310Logo.png' -Width 310 -Height 310
New-ReqMintPackageAsset -FileName 'Wide310x150Logo.png' -Width 310 -Height 150 -IncludeWordmark
New-ReqMintPackageAsset -FileName 'SplashScreen.png' -Width 620 -Height 300 -IncludeWordmark

Write-Host "Generated ReqMint Windows package assets in $resolvedOutputDirectory"
