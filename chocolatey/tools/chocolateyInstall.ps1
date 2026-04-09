$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  softwareName   = 'BetterTrumpet*'
  fileType       = 'exe'
  url            = 'https://github.com/xammen/BetterTrumpet/releases/download/v3.0.11/BetterTrumpet-3.0.11-setup.exe'
  checksum       = 'REPLACE_WITH_SHA256'
  checksumType   = 'sha256'
  silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
  validExitCodes = @(0)
}

# Kill running instance before install
Get-Process -Name 'BetterTrumpet' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

Install-ChocolateyPackage @packageArgs
