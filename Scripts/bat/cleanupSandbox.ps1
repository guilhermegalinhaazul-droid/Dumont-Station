$sandboxPath = Join-Path $PSScriptRoot '..\..\RobustToolbox\Robust.Shared\ContentPack\Sandbox.yml'
$sandboxPath = [System.IO.Path]::GetFullPath($sandboxPath)
$contents = Get-Content -Raw -Path $sandboxPath
$contents = [System.Text.RegularExpressions.Regex]::Replace(
    $contents,
    '(?m)^\s*-\s*"void \.ctor\(!0&\)"\r?\n',
    '')
Set-Content -Path $sandboxPath -Value $contents -NoNewline
