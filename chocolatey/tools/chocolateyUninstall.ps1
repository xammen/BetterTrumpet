$ErrorActionPreference = 'Stop'

# Remove Start Menu shortcut
$startMenuDir = [Environment]::GetFolderPath('Programs')
$shortcutPath = Join-Path $startMenuDir 'BetterTrumpet.lnk'
if (Test-Path $shortcutPath) {
    Remove-Item $shortcutPath -Force
    Write-Host "Removed Start Menu shortcut." -ForegroundColor Green
}

# Remove Startup shortcut
$startupDir = [Environment]::GetFolderPath('Startup')
$startupShortcut = Join-Path $startupDir 'BetterTrumpet.lnk'
if (Test-Path $startupShortcut) {
    Remove-Item $startupShortcut -Force
    Write-Host "Removed Startup shortcut." -ForegroundColor Green
}

# Kill running instance
Get-Process -Name 'BetterTrumpet' -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "BetterTrumpet uninstalled." -ForegroundColor Green
