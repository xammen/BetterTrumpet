$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  softwareName   = 'BetterTrumpet*'
  fileType       = 'exe'
  url            = 'https://github.com/xammen/BetterTrumpet/releases/download/v3.5.0/BetterTrumpet-3.5.0-setup.exe'
  checksum       = '9D50668C1EDB29199AA49CE162BEC0AF27992A6A3765AE7E220081FD8C9DBE58'
  checksumType   = 'sha256'
  silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
  validExitCodes = @(0)
}

# Kill running instance before install
Get-Process -Name 'BetterTrumpet' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

Install-ChocolateyPackage @packageArgs
