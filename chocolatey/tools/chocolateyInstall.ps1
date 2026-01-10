$ErrorActionPreference = 'Stop'

$toolsDir = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)"

$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  fileFullPath   = "$toolsDir\BetterTrumpet.exe"
  url64bit       = 'https://github.com/xammen/BetterTrumpet/releases/download/v2.3.1/BetterTrumpet-2.3.1-Portable.exe'
  checksum64     = '6C73C1580A42E795425520293DEBB7BC92026AE0598F43DBBD236CF4449DBF17'
  checksumType64 = 'sha256'
}

Get-ChocolateyWebFile @packageArgs

# Create shim ignore file so choco doesn't auto-create a shim
New-Item "$toolsDir\BetterTrumpet.exe.ignore" -Type File -Force | Out-Null
