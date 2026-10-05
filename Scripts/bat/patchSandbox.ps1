$sandboxPath = Join-Path $PSScriptRoot '..\..\RobustToolbox\Robust.Shared\ContentPack\Sandbox.yml'
$sandboxPath = [System.IO.Path]::GetFullPath($sandboxPath)
$contents = Get-Content -Raw -Path $sandboxPath

if ($contents -notmatch 'void \.ctor\(!0&\)') {
    $contents = $contents.Replace(
        '      - "void .ctor(!0[])"',
        "      - `"void .ctor(!0[])`"`r`n      - `"void .ctor(!0&)`"")
    Set-Content -Path $sandboxPath -Value $contents -NoNewline
}
