$ErrorActionPreference = 'Stop'

# Kill running instance
Get-Process -Name 'BetterTrumpet' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Uninstall via Inno Setup uninstaller
$uninstallKey = Get-UninstallRegistryKey -SoftwareName 'BetterTrumpet*'

if ($uninstallKey) {
  $uninstallString = $uninstallKey.UninstallString
  # Inno Setup uninstaller path may be quoted
  $uninstallExe = $uninstallString -replace '"', ''
  
  if (Test-Path $uninstallExe) {
    Start-Process -FilePath $uninstallExe -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait
    Write-Host "BetterTrumpet uninstalled successfully." -ForegroundColor Green
  } else {
    Write-Warning "Uninstaller not found at: $uninstallExe"
  }
} else {
  Write-Warning "BetterTrumpet uninstall registry key not found. It may have been removed manually."
}
