REM SPDX-License-Identifier: AGPL-3.0-or-later

@echo off
setlocal
cd /d "%~dp0..\.."

REM C# range expressions can emit the ReadOnlySpan ref constructor.
REM RobustToolbox must allow it during client assembly verification.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0patchSandbox.ps1"
