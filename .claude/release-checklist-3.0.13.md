# BetterTrumpet 3.0.13 Release Checklist

## ✅ Preparation (Completed)

- [x] Version bumped to 3.0.13 in all files
  - [x] installer.iss
  - [x] GitVersion.yml
  - [x] Package.appxmanifest
  - [x] chocolatey/bettertrumpet.nuspec
  - [x] chocolatey/tools/chocolateyInstall.ps1
- [x] Winget manifests created (3.0.13/)
- [x] Release notes written (.claude/release-3.0.13-notes.md)

## 📦 Build & Package

### 1. Build the installer
```powershell
# Clean build
msbuild EarTrumpet.vs15.sln /t:Clean /p:Configuration=Release /p:Platform=x86

# Build Release
msbuild EarTrumpet.vs15.sln /t:Rebuild /p:Configuration=Release /p:Platform=x86 /m
```

### 2. Create the Inno Setup installer
```powershell
# Run Inno Setup Compiler (adjust path if needed)
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss

# Output: dist/BetterTrumpet-3.0.13-setup.exe
```

### 3. Calculate SHA256 checksum
```powershell
# Calculate checksum for the installer
$checksum = (Get-FileHash -Path "dist\BetterTrumpet-3.0.13-setup.exe" -Algorithm SHA256).Hash
Write-Host "SHA256: $checksum"
```

### 4. Update checksums in manifests
```powershell
# Update chocolatey checksum
(Get-Content chocolatey\tools\chocolateyInstall.ps1) -replace 'PLACEHOLDER_CHECKSUM_TO_BE_CALCULATED', $checksum | Set-Content chocolatey\tools\chocolateyInstall.ps1

# Update winget checksum
(Get-Content winget-pkgs\manifests\x\xmn\BetterTrumpet\3.0.13\xmn.BetterTrumpet.installer.yaml) -replace 'PLACEHOLDER_CHECKSUM_TO_BE_CALCULATED', $checksum | Set-Content winget-pkgs\manifests\x\xmn\BetterTrumpet\3.0.13\xmn.BetterTrumpet.installer.yaml
```

## 🚀 Git & GitHub

### 5. Commit all changes
```bash
git add -A
git commit -m "release: bump version to 3.0.13

- Fixed backdrop rendering on startup (issue #13)
- Added device context menu
- New CLI commands: doctor, batch, volume shortcuts
- Performance improvements with 3-phase startup

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### 6. Create and push tag
```bash
git tag -a v3.0.13 -m "BetterTrumpet 3.0.13"
git push origin master
git push origin v3.0.13
```

### 7. Create GitHub Release
```bash
# Upload the installer first, then create release
gh release create v3.0.13 \
  dist/BetterTrumpet-3.0.13-setup.exe \
  --title "BetterTrumpet 3.0.13" \
  --notes-file .claude/release-3.0.13-notes.md
```

## 📦 Package Managers

### 8. Test Chocolatey package locally
```powershell
cd chocolatey
choco pack
# Test install (optional)
choco install bettertrumpet -s . --force
```

### 9. Push to Chocolatey
```powershell
choco push bettertrumpet.3.0.13.nupkg --source https://push.chocolatey.org/
```

### 10. Winget PR (optional, can be done later)
The winget manifests are ready in `winget-pkgs/manifests/x/xmn/BetterTrumpet/3.0.13/`
- Fork microsoft/winget-pkgs
- Copy manifests to your fork
- Create PR to microsoft/winget-pkgs

## ✅ Verification

### 11. Post-release checks
- [ ] GitHub release is live: https://github.com/xammen/BetterTrumpet/releases/tag/v3.0.13
- [ ] Installer downloads correctly
- [ ] Chocolatey package is published
- [ ] Auto-update works from 3.0.12 → 3.0.13
- [ ] Close issue #13 with reference to release

## 🎉 Announce

- [ ] Update bettertrumpet.hiii.boo website (if applicable)
- [ ] Post on social media / Reddit / Discord (if applicable)
- [ ] Respond to issue #13 with release link

---

## Quick Commands Summary

```powershell
# 1. Build
msbuild EarTrumpet.vs15.sln /t:Rebuild /p:Configuration=Release /p:Platform=x86 /m

# 2. Create installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss

# 3. Get checksum
$hash = (Get-FileHash dist\BetterTrumpet-3.0.13-setup.exe -Algorithm SHA256).Hash
Write-Host $hash

# 4. Update checksums
(Get-Content chocolatey\tools\chocolateyInstall.ps1) -replace 'PLACEHOLDER_CHECKSUM_TO_BE_CALCULATED', $hash | Set-Content chocolatey\tools\chocolateyInstall.ps1
(Get-Content winget-pkgs\manifests\x\xmn\BetterTrumpet\3.0.13\xmn.BetterTrumpet.installer.yaml) -replace 'PLACEHOLDER_CHECKSUM_TO_BE_CALCULATED', $hash | Set-Content winget-pkgs\manifests\x\xmn\BetterTrumpet\3.0.13\xmn.BetterTrumpet.installer.yaml

# 5. Git
git add -A
git commit -m "release: bump version to 3.0.13"
git tag -a v3.0.13 -m "BetterTrumpet 3.0.13"
git push origin master
git push origin v3.0.13

# 6. GitHub Release
gh release create v3.0.13 dist/BetterTrumpet-3.0.13-setup.exe --title "BetterTrumpet 3.0.13" --notes-file .claude/release-3.0.13-notes.md

# 7. Chocolatey
cd chocolatey
choco pack
choco push bettertrumpet.3.0.13.nupkg --source https://push.chocolatey.org/
```
