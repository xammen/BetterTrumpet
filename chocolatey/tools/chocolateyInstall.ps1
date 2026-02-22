$ErrorActionPreference = 'Stop'

$toolsDir = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)"

$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  fileFullPath   = "$toolsDir\BetterTrumpet.exe"
  url64bit       = 'https://github.com/xammen/BetterTrumpet/releases/download/v2.4.0/BetterTrumpet-2.4.0-Portable.exe'
  checksum64     = 'EEBA677140BD87088572E7F2A5D38471A2D9DB062AEB713D15E70337A0A756E7'
  checksumType64 = 'sha256'
}

Get-ChocolateyWebFile @packageArgs

# Create shim ignore file so choco doesn't auto-create a shim
New-Item "$toolsDir\BetterTrumpet.exe.ignore" -Type File -Force | Out-Null
