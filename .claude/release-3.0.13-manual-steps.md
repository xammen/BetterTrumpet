# Release 3.0.13 - Manual Steps Required

## ✅ Completed So Far

- [x] Version bumped to 3.0.13 in all files
- [x] Winget manifests created
- [x] Release notes written (cool & friendly tone)
- [x] Release build compiled successfully (Build/Release/BetterTrumpet.exe - 2.8 MB)

## 🔴 Inno Setup Not Found

The Inno Setup compiler (ISCC.exe) is not installed on your system.

**Option 1: Install Inno Setup**
1. Download from: https://jrsoftware.org/isdl.php
2. Install to default location: `C:\Program Files (x86)\Inno Setup 6\`
3. Run the release script: `.\release-3.0.13.ps1`

**Option 2: Manual Installer Creation**
1. Open Inno Setup IDE
2. Load `installer.iss`
3. Click "Compile" (or press Ctrl+F9)
4. Output: `dist/BetterTrumpet-3.0.13-setup.exe`

**Option 3: Use Existing Build Tools**
If you have another method for creating the installer, proceed with that.

## 📋 Next Steps After Installer is Created

```powershell
# 1. Calculate checksum
$hash = (Get-FileHash dist\BetterTrumpet-3.0.13-setup.exe -Algorithm SHA256).Hash
Write-Host "SHA256: $hash"

# 2. Update checksums in files
(Get-Content chocolatey\tools\chocolateyInstall.ps1) -replace 'PLACEHOLDER_CHECKSUM_TO_BE_CALCULATED', $hash | Set-Content chocolatey\tools\chocolateyInstall.ps1
(Get-Content winget-pkgs\manifests\x\xmn\BetterTrumpet\3.0.13\xmn.BetterTrumpet.installer.yaml) -replace 'PLACEHOLDER_CHECKSUM_TO_BE_CALCULATED', $hash | Set-Content winget-pkgs\manifests\x\xmn\BetterTrumpet\3.0.13\xmn.BetterTrumpet.installer.yaml

# 3. Commit everything
git add -A
git commit -m "release: bump version to 3.0.13"

# 4. Create tag
git tag -a v3.0.13 -m "BetterTrumpet 3.0.13"

# 5. Push
git push origin master
git push origin v3.0.13

# 6. Create GitHub release
gh release create v3.0.13 dist\BetterTrumpet-3.0.13-setup.exe --title "BetterTrumpet 3.0.13" --notes-file .claude\release-3.0.13-notes.md

# 7. Package Chocolatey
cd chocolatey
choco pack
choco push bettertrumpet.3.0.13.nupkg --source https://push.chocolatey.org/
```

## 🎯 Alternative: Use the Automated Script

Once Inno Setup is installed, just run:
```powershell
.\release-3.0.13.ps1
```

The script will guide you through all steps with confirmations.

## 📁 Files Ready

All version files and manifests are updated and ready:
- ✅ installer.iss (3.0.13)
- ✅ GitVersion.yml (3.0.13)
- ✅ Package.appxmanifest (3.0.13)
- ✅ chocolatey/bettertrumpet.nuspec (3.0.13)
- ✅ chocolatey/tools/chocolateyInstall.ps1 (3.0.13 URL, needs checksum)
- ✅ winget-pkgs/manifests/x/xmn/BetterTrumpet/3.0.13/ (needs checksum)
- ✅ Release notes: .claude/release-3.0.13-notes.md

## 🎉 What to Do After Release

1. Close issue #13 with: "Fixed in v3.0.13 🎉"
2. Test auto-update from 3.0.12 → 3.0.13
3. Announce the release (optional)

---

**Want me to help with anything else while you get Inno Setup sorted?**
