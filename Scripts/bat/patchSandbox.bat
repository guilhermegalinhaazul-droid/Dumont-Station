REM SPDX-License-Identifier: AGPL-3.0-or-later

@echo off
setlocal
cd /d "%~dp0..\.."

REM C# range expressions can emit the ReadOnlySpan ref constructor.
REM RobustToolbox must allow it during client assembly verification.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p = 'RobustToolbox\Robust.Shared\ContentPack\Sandbox.yml'; $t = Get-Content -Raw $p; if ($t -notmatch 'void \.ctor\(!0&\)') { $t = $t.Replace('      - \"void .ctor(!0[])\"', '      - \"void .ctor(!0[])\"' + [Environment]::NewLine + '      - \"void .ctor(!0&)\"'); Set-Content -Path $p -Value $t -NoNewline }"

