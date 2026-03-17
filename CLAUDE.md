# BetterTrumpet — Project Context for Claude

## What is this

BetterTrumpet is a fork of [EarTrumpet](https://github.com/File-New-Project/EarTrumpet), the popular Windows per-app volume mixer. The fork adds premium features: themes, onboarding, auto-updates, CLI, media popup, crash reporting, etc.

- **Owner**: xammen
- **Repo**: https://github.com/xammen/BetterTrumpet
- **Current version**: 3.0.7
- **Language**: C# / WPF (.NET Framework 4.6.2)
- **Build system**: MSBuild + GitVersion + Inno Setup
- **Distribution**: GitHub Releases, Chocolatey, Winget

## Build Commands

```bash
# Restore NuGet
nuget.exe restore EarTrumpet.vs15.sln

# Build (ALWAYS x86 Release)
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" EarTrumpet\EarTrumpet.csproj /p:Configuration=Release /p:Platform=x86 /p:OutputPath=..\Build\Release /t:Rebuild /v:minimal

# Build portable zip
powershell -ExecutionPolicy Bypass -File build-portable.ps1

# Build installer
& 'C:\Users\xammen\AppData\Local\Programs\Inno Setup 6\ISCC.exe' installer.iss

# Verify version
[System.Diagnostics.FileVersionInfo]::GetVersionInfo('Build\Release\BetterTrumpet.exe').FileVersion
```

## Critical: Version Bumping

GitVersion derives the version from **git tags**. The workflow is:
1. Edit version in: `GitVersion.yml`, `installer.iss` (5 places), `build-portable.ps1` (2 places)
2. **Commit first, THEN tag, THEN build** — the tag must exist on HEAD before MSBuild runs
3. Clean caches before build: `Remove-Item '.git\gitversion_cache' -Recurse -Force; Remove-Item 'EarTrumpet\obj' -Recurse -Force`
4. Build → verify FileVersion → build portable + installer → upload to release

## Critical: Releasing

```bash
# Commit + push + tag
git push && git tag v3.0.X && git push origin v3.0.X

# Rebuild AFTER tag
# ... (build commands above)

# Create release
gh release create v3.0.X "dist/BetterTrumpet-3.0.X-setup.exe" "dist/BetterTrumpet-3.0.X-portable.zip" --title "BetterTrumpet v3.0.X" --latest --notes "..."

# If updating an existing release (tag moved):
git tag -d v3.0.X && git push origin :refs/tags/v3.0.X && git tag v3.0.X && git push origin v3.0.X
gh release delete-asset v3.0.X BetterTrumpet-3.0.X-setup.exe --yes
gh release delete-asset v3.0.X BetterTrumpet-3.0.X-portable.zip --yes
gh release upload v3.0.X "dist/..." "dist/..."
gh release edit v3.0.X --draft=false --latest
```

**Moving a tag causes the release to go back to Draft. Always re-publish with `--draft=false`.**

## Architecture

```
EarTrumpet/
├── App.xaml.cs              # Startup: 3-phase init, onboarding, changelog
├── AppSettings.cs           # All settings (registry or JSON portable)
├── CLI/
│   ├── CliHandler.cs        # 19 commands via named pipe IPC
│   └── CliEntryPoint.cs     # Pipe server
├── DataModel/
│   ├── UpdateService.cs     # GitHub API check every 6h, auto-download+install
│   ├── StorageFactory.cs    # Registry vs portable JSON detection
│   └── SettingsExportService.cs
├── Diagnosis/
│   └── ErrorReporter.cs     # Sentry crash reporting (GDPR opt-in)
├── UI/
│   ├── Views/
│   │   ├── OnboardingWindow.xaml(.cs)    # 6-page wizard
│   │   ├── ChangelogWindow.xaml(.cs)     # Fetches GitHub release notes
│   │   ├── SettingsWindow.xaml(.cs)      # ~3000 lines, all settings
│   │   └── FlyoutWindow.xaml(.cs)        # Main volume popup
│   └── ViewModels/
│       ├── OnboardingViewModel.cs
│       ├── FlyoutViewModel.cs
│       ├── EarTrumpetColorsSettingsPageViewModel.cs  # Themes engine
│       └── EarTrumpetAboutPageViewModel.cs
├── Properties/
│   ├── Resources.resx        # English strings (default)
│   ├── Resources.fr-FR.resx  # French translations
│   └── Resources.Designer.cs # Auto-generated accessors (MUST be updated manually when adding keys)
└── Interop/
    └── Helpers/
        ├── PackageHelper.cs  # MSIX vs portable detection
        └── PipeClient.cs     # Named pipe for CLI
```

## Localization

All user-facing strings use `.resx` resources:
- **XAML**: `Text="{x:Static resx:Resources.KeyName}"` (namespace: `xmlns:resx="clr-namespace:EarTrumpet.Properties"`)
- **C#**: `EarTrumpet.Properties.Resources.KeyName` (use full path in App.xaml.cs because `Properties` is ambiguous with `Application.Properties`)
- **When adding new keys**: add to `Resources.resx` (EN) + `Resources.fr-FR.resx` (FR) + `Resources.Designer.cs` (static property)

## Settings Storage

- **Installed** (non-MSIX): `HKCU\Software\EarTrumpet` via `RegistrySettingsBag`
- **Portable**: `settings.json` next to exe via `JsonSettingsBag` (detected by `portable.marker` file)
- **MSIX**: `ApplicationData.Current.LocalSettings` (not used in BetterTrumpet)

## Key Design Decisions

- **AssemblyName is `BetterTrumpet`** but namespace is `EarTrumpet` (fork constraint)
- **Pack URIs**: use `pack://application:,,,/Assets/file.ext` (no assembly qualifier needed)
- **WPF frozen brushes**: never set `Fill="#RRGGBB"` as attribute on elements you want to animate — use inline `<SolidColorBrush>` child elements instead
- **`#if DEBUG` blocks**: avoid for features you want in Release builds (e.g., debug shortcuts)
- **Installer kills process**: `PrepareToInstall` in `installer.iss` runs `taskkill /F /IM BetterTrumpet.exe`
- **Silent install relaunch**: `[Run]` section has `skipifnotsilent` entry to relaunch after `/VERYSILENT`
- **Uninstall cleans registry**: `CurUninstallStepChanged` deletes `HKCU\Software\EarTrumpet` + removes from PATH

## Debug Shortcuts

- **Left Ctrl at startup** → force onboarding
- **Left Shift at startup** → force changelog
- **Left Ctrl + click "Check" in Settings** → simulate fake update (v99.0.0) to test UI

## Update System

`UpdateService.cs`:
- Checks `api.github.com/repos/xammen/BetterTrumpet/releases/latest` every 6 hours
- Finds `-setup.exe` asset URL from release JSON
- `DownloadAndInstallAsync()`: downloads to temp, launches with `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`
- Fallback: opens GitHub release page in browser

## Onboarding Flow

6 pages: Welcome → Audio (device picker) → Appearance (theme) → Privacy (telemetry + update channel + startup) → Ready (checkmarks) → Tray Pin (GIF + "Done!" with confetti)

- Telemetry enabled only when user passes Privacy page (GDPR)
- `HasShownFirstRun` flag prevents re-showing
- Step dots animated at bottom, accent color

## Theme System

`EarTrumpetColorsSettingsPageViewModel.cs`:
- 7 color channels: slider thumb, fill, track bg, peak meter, window bg, text, accent glow
- 28 built-in presets in 7 categories
- Dynamic Album Art mode (timer-based, disabled when selecting a preset)
- Save/load/delete custom themes
- Export/import as `.bttheme` JSON

## CLI

`bt.cmd` → `BetterTrumpet.exe` (added to PATH optionally via installer)

19 commands via named pipe `BetterTrumpet_CLI`:
- `list-devices`, `list-apps`, `get-volume`, `set-volume`, `set-device`, `mute`, `unmute`, `toggle-mute`
- `profile-save`, `profile-load`, `profile-list`, `profile-delete`, `profile-export`, `profile-import`
- `update-check`, `update-install`, `settings-export`, `settings-import`, `health`

Device matching: partial (IndexOf). App matching: exact on ExeName or DisplayName.

## Packaging

- **Chocolatey**: `chocolatey/` folder, `choco push` with API key
- **Winget**: `manifests/x/xmn/BetterTrumpet/X.Y.Z/`, PR to `microsoft/winget-pkgs` (use `wingetcreate` or manual via `gh api`)
- **Installer**: `installer.iss` (Inno Setup 6), outputs to `dist/`
- **Portable**: `build-portable.ps1`, outputs to `dist/`

## Common Pitfalls

1. **Build before tag** → wrong version in binary. Always tag first.
2. **`git add -A`** → includes `dist/` binaries. Use specific `git add` per file.
3. **Moving a tag** → GitHub release goes Draft. Must re-publish.
4. **`Resources.Designer.cs`** → not auto-generated on build. Must manually add new properties.
5. **`Properties.Resources` in App.xaml.cs** → ambiguous. Use `EarTrumpet.Properties.Resources`.
6. **Frozen WPF brushes** → crash when animating. Use inline `<SolidColorBrush>`.
7. **Sentry `Package.Current`** → throws in non-MSIX mode. Expected, don't log to Sentry.

## Release Notes Convention

GitHub release notes must be:
- **In English**, professional tone
- Format: `## BetterTrumpet vX.Y.Z` title, `### Section` headers, `- **Feature** — description` bullets
- The changelog window fetches these notes live from GitHub API and renders them
