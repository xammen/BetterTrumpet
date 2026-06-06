@echo off
setlocal

set "BT_EXE=%~dp0BetterTrumpet.exe"
if not exist "%BT_EXE%" if exist "%~dp0Build\Release\BetterTrumpet.exe" set "BT_EXE=%~dp0Build\Release\BetterTrumpet.exe"
if not exist "%BT_EXE%" if exist "%ProgramFiles(x86)%\BetterTrumpet\BetterTrumpet.exe" set "BT_EXE=%ProgramFiles(x86)%\BetterTrumpet\BetterTrumpet.exe"
if not exist "%BT_EXE%" if exist "%ProgramFiles%\BetterTrumpet\BetterTrumpet.exe" set "BT_EXE=%ProgramFiles%\BetterTrumpet\BetterTrumpet.exe"

if not exist "%BT_EXE%" (
    echo {"error":"BetterTrumpet.exe was not found. Install BetterTrumpet or build it into Build\\Release first."}
    exit /b 1
)

set "BT_ARGS=%*"

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $id=[guid]::NewGuid().ToString('N'); $out=Join-Path $env:TEMP ('bettertrumpet-cli-' + $id + '.out'); $err=Join-Path $env:TEMP ('bettertrumpet-cli-' + $id + '.err'); try { $p=Start-Process -FilePath $env:BT_EXE -ArgumentList $env:BT_ARGS -NoNewWindow -RedirectStandardOutput $out -RedirectStandardError $err -Wait -PassThru; $o=''; if (Test-Path -LiteralPath $out) { $o=Get-Content -LiteralPath $out -Raw }; if (-not [string]::IsNullOrWhiteSpace($o)) { try { $null=$o | ConvertFrom-Json; $o } catch { @{error=$o.Trim()} | ConvertTo-Json -Compress } } elseif ($p.ExitCode -eq 0) { @{error='BetterTrumpet CLI produced no output. Ensure BetterTrumpet is running and responsive.'} | ConvertTo-Json -Compress }; if ($p.ExitCode -ne 0) { $e=''; if (Test-Path -LiteralPath $err) { $e=Get-Content -LiteralPath $err -Raw }; if ([string]::IsNullOrWhiteSpace($e)) { $e='BetterTrumpet CLI failed with exit code ' + $p.ExitCode }; @{error=$e.Trim()} | ConvertTo-Json -Compress; exit $p.ExitCode } } finally { Remove-Item -LiteralPath $out -ErrorAction SilentlyContinue; Remove-Item -LiteralPath $err -ErrorAction SilentlyContinue }"
set "BT_EXIT=%ERRORLEVEL%"

exit /b %BT_EXIT%
