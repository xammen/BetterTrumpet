$src = 'Build\Release'
$dst = 'dist\BetterTrumpet-3.1.0-portable'
Remove-Item $dst -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $dst | Out-Null

# Copy main files (exclude pdb, xml doc files, Windows.winmd)
Get-ChildItem $src -File | Where-Object { $_.Extension -notin '.pdb','.xml' -and $_.Name -ne 'Windows.winmd' } | Copy-Item -Destination $dst

# Copy language folders. Exclude packaging/publish output folders that may be
# produced by the Microsoft Store build under Build\Release.
Get-ChildItem $src -Directory | Where-Object { Get-ChildItem $_.FullName -File -Filter '*.resources.dll' -ErrorAction SilentlyContinue } | ForEach-Object {
    Copy-Item $_.FullName -Destination (Join-Path $dst $_.Name) -Recurse
}

# Create portable marker file
Set-Content (Join-Path $dst 'portable.marker') 'BetterTrumpet Portable Mode'

# Zip it
$zipPath = 'dist\BetterTrumpet-3.1.0-portable.zip'
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$dst\*" -DestinationPath $zipPath -CompressionLevel Optimal

$size = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "ZIP created: $zipPath ($size MB)"
