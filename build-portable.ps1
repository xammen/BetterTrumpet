param(
    [ValidateSet('x86', 'x64', 'arm64')]
    [string]$Arch = 'x86'
)

$ErrorActionPreference = 'Stop'
$Version = '3.4.0'

switch ($Arch) {
    'x86'   { $src = 'Build\Release';        $suffix = '' }
    'x64'   { $src = 'Build\Release-x64';    $suffix = '-x64' }
    'arm64' { $src = 'Build\Release-arm64';  $suffix = '-arm64' }
}

# Without this, a missing build dir is only a non-terminating error: the script would sail on and
# emit a ~200-byte ZIP containing nothing but portable.marker, with exit code 0.
if (-not (Test-Path (Join-Path $src 'BetterTrumpet.exe'))) {
    throw "Build output not found: $src\BetterTrumpet.exe - build Release|$Arch first."
}

$dst = "dist\BetterTrumpet-$Version-portable$suffix"
Remove-Item $dst -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $dst | Out-Null

# Copy main files (exclude pdb, xml doc files, Windows.winmd)
Get-ChildItem $src -File | Where-Object { $_.Extension -notin '.pdb','.xml' -and $_.Name -ne 'Windows.winmd' } | Copy-Item -Destination $dst

# Copy language folders. Exclude packaging/publish output folders that may be
# produced by the Microsoft Store build under Build\Release.
Get-ChildItem $src -Directory | Where-Object { Get-ChildItem $_.FullName -File -Filter '*.resources.dll' -ErrorAction SilentlyContinue } | ForEach-Object {
    Copy-Item $_.FullName -Destination (Join-Path $dst $_.Name) -Recurse
}

# Copy WebView2 content folders (settings UI + announcements page)
foreach ($folder in @('SettingsWeb', 'AnnouncementsWeb', 'runtimes')) {
    $srcFolder = Join-Path $src $folder
    if (Test-Path $srcFolder) {
        Copy-Item $srcFolder -Destination (Join-Path $dst $folder) -Recurse
    }
}

# Create portable marker file
Set-Content (Join-Path $dst 'portable.marker') 'BetterTrumpet Portable Mode'

# Zip it
$zipPath = "dist\BetterTrumpet-$Version-portable$suffix.zip"
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$dst\*" -DestinationPath $zipPath -CompressionLevel Optimal

$size = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "ZIP created: $zipPath ($size MB)"
