$ErrorActionPreference = 'Stop'

$version = '3.4.0'
$baseUrl = "https://github.com/xammen/BetterTrumpet/releases/download/v$version"

# release.ps1 rewrites these three assignments in place; keep the "$name = '<value>'" shape.
$checksumX86   = 'PLACEHOLDER_CHECKSUM_X86'
$checksumX64   = 'PLACEHOLDER_CHECKSUM_X64'
$checksumArm64 = 'PLACEHOLDER_CHECKSUM_ARM64'

# choco.exe is a 32-bit process, so under WOW64 (including ARM64 emulation) PROCESSOR_ARCHITECTURE
# reports "x86" and the real machine architecture is only in PROCESSOR_ARCHITEW6432. Chocolatey's own
# Get-OSArchitectureWidth additionally forces 32-bit whenever it sees ARM64, so url64bit is never
# selected on those machines: without this check an ARM64 install silently lands on the x86 build.
$nativeArch = if ($env:PROCESSOR_ARCHITEW6432) { $env:PROCESSOR_ARCHITEW6432 } else { $env:PROCESSOR_ARCHITECTURE }

if ($nativeArch -eq 'ARM64' -and -not $env:ChocolateyForceX86) {
  $packageArgs = @{
    packageName    = $env:ChocolateyPackageName
    softwareName   = 'BetterTrumpet*'
    fileType       = 'exe'
    url            = "$baseUrl/BetterTrumpet-$version-setup-arm64.exe"
    checksum       = $checksumArm64
    checksumType   = 'sha256'
    silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
    validExitCodes = @(0)
  }
} else {
  $packageArgs = @{
    packageName    = $env:ChocolateyPackageName
    softwareName   = 'BetterTrumpet*'
    fileType       = 'exe'
    url            = "$baseUrl/BetterTrumpet-$version-setup.exe"
    checksum       = $checksumX86
    checksumType   = 'sha256'
    url64bit       = "$baseUrl/BetterTrumpet-$version-setup-x64.exe"
    checksum64     = $checksumX64
    checksumType64 = 'sha256'
    silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
    validExitCodes = @(0)
  }
}

# Kill running instance before install
Get-Process -Name 'BetterTrumpet' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

Install-ChocolateyPackage @packageArgs
