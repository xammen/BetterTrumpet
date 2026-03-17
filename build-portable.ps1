$src = 'Build\Release'
$dst = 'dist\BetterTrumpet-3.0.4-portable'
Remove-Item $dst -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $dst | Out-Null

# Copy main files (exclude pdb, xml doc files, Windows.winmd)
Get-ChildItem $src -File | Where-Object { $_.Extension -notin '.pdb','.xml' -and $_.Name -ne 'Windows.winmd' } | Copy-Item -Destination $dst

# Copy language folders
Get-ChildItem $src -Directory | ForEach-Object {
    Copy-Item $_.FullName -Destination (Join-Path $dst $_.Name) -Recurse
}

# Create portable marker file
Set-Content (Join-Path $dst '.portable') 'BetterTrumpet Portable Mode'

# Zip it
$zipPath = 'dist\BetterTrumpet-3.0.4-portable.zip'
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$dst\*" -DestinationPath $zipPath -CompressionLevel Optimal

$size = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "ZIP created: $zipPath ($size MB)"
