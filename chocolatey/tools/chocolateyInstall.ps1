$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  softwareName   = 'BetterTrumpet*'
  fileType       = 'exe'
  url            = 'https://github.com/xammen/BetterTrumpet/releases/download/v3.0.12/BetterTrumpet-3.0.12-setup.exe'
  checksum       = 'CE398361E1D77EA082B5F4FA56BDFAEEA0B9CCB4217722B13C98FA3E15CCEA41'
  checksumType   = 'sha256'
  silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
  validExitCodes = @(0)
}

# Kill running instance before install
Get-Process -Name 'BetterTrumpet' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

Install-ChocolateyPackage @packageArgs
