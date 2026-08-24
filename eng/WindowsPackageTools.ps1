Set-StrictMode -Version Latest

function Get-ReqMintMakeAppxPath {
    $makeAppxCommand = Get-Command 'makeappx.exe' -ErrorAction SilentlyContinue
    if ($null -ne $makeAppxCommand) {
        return $makeAppxCommand.Source
    }

    $programFilesX86 = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
    $windowsKitsRoot = Join-Path $programFilesX86 'Windows Kits\10'
    $candidatePaths = @(
        (Join-Path $windowsKitsRoot 'bin\*\x64\makeappx.exe'),
        (Join-Path $windowsKitsRoot 'App Certification Kit\makeappx.exe')
    )

    $makeAppxPath = Get-Item -Path $candidatePaths -ErrorAction SilentlyContinue |
        Sort-Object -Property FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName

    if ([string]::IsNullOrWhiteSpace($makeAppxPath)) {
        throw 'MakeAppx.exe was not found. Install the Windows 10/11 SDK, or use the Windows packaging GitHub Actions workflows.'
    }

    return $makeAppxPath
}
