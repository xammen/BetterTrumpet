# BetterTrumpet - Project Context for Codex

## Mirror Policy

On Windows, `AGENTS.md` and `agents.md` are the same path. Use `AGENTS.md` as the canonical filename.

## Maintenance Rule

Update this file whenever a task adds a meaningful feature, fixes an important bug, changes user-visible behavior, changes release/build/package workflow, adds a new CLI command, modifies diagnostics/logging, or introduces a recurring pitfall. Keep entries concise and practical so the next AI session can understand the current app state without rereading the whole history.

## Current Branch State

- Branch: `master`
- `master` and `origin/master` contain the released 3.2.3 source and distribution metadata.
- Public tag: `v3.2.3`, pointing at release commit `69a3012`.
- Current version line: `3.2.3` (released). The x86 Release binary and public GitHub assets report `3.2.3`.
- `migration/net8` is a historical ancestor at `7a6e8f9`; do not move or synchronize it as part of current releases.
- Target framework: `net8.0-windows10.0.19041.0`
- Language: C# / WPF
- Assembly name: `BetterTrumpet`
- Namespace: `EarTrumpet`
- The tree contains unrelated user work. Known unrelated local state includes `bettertrumpet-site`, `.planning/*`, `docs/FEATURES-3.0.13.md`, `docs/RECENT-CHANGES.md`, and untracked Chocolatey `.nupkg` files. Never revert changes you did not make.

## What This Is

BetterTrumpet is a fork of [EarTrumpet](https://github.com/File-New-Project/EarTrumpet), the Windows per-app volume mixer. This fork adds themes, onboarding, auto-updates, CLI, media popup, crash reporting, QuickTrumpet presets, and release tooling.

- Owner: `xammen`
- Repo: `https://github.com/xammen/BetterTrumpet`
- Build system: MSBuild + GitVersion + Inno Setup
- Current distribution: GitHub Releases, Chocolatey, Winget, Microsoft Store submission path
- Possible future distribution: Scoop bucket, web-hosted MSIX `.appinstaller`, npm wrapper for devs, Intune/enterprise package

## Build And Verify

```bash
nuget.exe restore EarTrumpet.vs15.sln
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" EarTrumpet\EarTrumpet.csproj /p:Configuration=Release /p:Platform=x86 /p:OutputPath=..\Build\Release /t:Rebuild /v:minimal
powershell -ExecutionPolicy Bypass -File build-portable.ps1
& 'C:\Users\xammen\AppData\Local\Programs\Inno Setup 6\ISCC.exe' installer.iss
[System.Diagnostics.FileVersionInfo]::GetVersionInfo('Build\Release\BetterTrumpet.exe').FileVersion
```

- Build x86 Release only for real validation.
- `Release|x86` is self-contained for GitHub/Chocolatey/Winget distribution so users do not need to install the x86 .NET desktop runtime separately.
- If `SelfContained` is removed or not applied for `Release|x86`, users can see a ".NET 8.0.0 (x86) required" launch prompt from `BetterTrumpet.exe`.
- During dev validation, if the running `Build\Release\BetterTrumpet.exe` locks build outputs, it is acceptable to close the running BetterTrumpet process before rebuilding; after a successful rebuild, relaunch `Build\Release\BetterTrumpet.exe` when runtime verification is needed.
- Before version bumps, commit first, then tag, then build.
- Clear `.git\gitversion_cache` and `EarTrumpet\obj` if the version looks stale.
- `GitVersion.yml` intentionally sets `assembly-file-versioning-format: '{Major}.{Minor}.{Patch}'` so `FileVersionInfo.FileVersion` displays `3.1.0`, not `3.1.0.0`.
- Sign binaries before calculating SHA256 checksums if code signing is added later.
- After rebuilding public assets, recalculate and update `release-checksums-*.txt`, Chocolatey checksum, and Winget `InstallerSha256` together.
- Never touch `dist/` unless the task explicitly requires release packaging.
- Microsoft Store packaging is now for the new BetterTrumpet Partner Center app, not the inherited EarTrumpet listing. Partner Center identity: `Package/Identity/Name=xammen.Bettertrumpet`, `Package/Identity/Publisher=CN=7EDFC72A-8780-4841-8F34-30B45D719EAF`, `Package/Properties/PublisherDisplayName=xammen`.
- Microsoft Store builds must pass `/p:Channel=Store`; without it, GitVersion appends the post-tag commit count (for example `3.2.0.3`) instead of producing the required four-part Store version `3.2.0.0`.
- Packaged/MSIX runs (`App.HasIdentity == true`) must not initialize or expose the GitHub/Inno updater. Microsoft Store owns updates for those installations; unpackaged GitHub/Chocolatey/Winget builds keep the existing updater.

## User-Led Validation

- The agent may execute `docs/DEV-BUILD-RUN.md` itself when requested or needed: close BetterTrumpet, build the Debug x86 binary, relaunch it, verify the active executable path, run `--ping`, and inspect the startup log.
- The user performs the functional/manual checks in the UI and reports the result. Do not claim that those checks passed unless the user confirmed them.
- Use `docs/DEV-BUILD-RUN.md` as the source of truth for the Debug x86 close/build/launch procedure. Its local GitVersion bypass is for development only; public Release builds still follow the Release procedure above.
- For every code change, provide a short, exact manual validation guide identifying the scenario, expected result, and log excerpt to provide if it fails.

## Workbench

Use `python tools/bettertrumpet_workbench.py` for repo-aware routing and validation.

- `analyze` classifies the current diff.
- `check --scope auto` runs the fast validations.
- `check --scope auto --full` adds heavier checks.
- `build`, `web`, and `package` run explicit heavy actions.
- `learn --area ... --symptom ... --rule ...` records recurring traps.

## Repo Map

```
EarTrumpet/
├── App.xaml.cs              # Startup, onboarding, changelog, tray menu
├── AppSettings.cs           # Registry / portable settings
├── CLI/
│   ├── CliHandler.cs        # CLI command parsing and pipe IPC
│   └── CliEntryPoint.cs     # bt entry point / help text
├── DataModel/
│   ├── UpdateService.cs     # GitHub release checks and installer flow
│   ├── StorageFactory.cs    # Registry vs portable JSON detection
│   └── SettingsExportService.cs
├── Diagnosis/
│   └── ErrorReporter.cs     # Crash reporting and logs
├── UI/
│   ├── Views/
│   │   ├── OnboardingWindow.xaml(.cs)
│   │   ├── ChangelogWindow.xaml(.cs)
│   │   ├── SettingsWindow.xaml(.cs)
│   │   └── FlyoutWindow.xaml(.cs)
│   └── ViewModels/
│       ├── OnboardingViewModel.cs
│       ├── FlyoutViewModel.cs
│       ├── EarTrumpetColorsSettingsPageViewModel.cs
│       └── EarTrumpetAboutPageViewModel.cs
├── Properties/
│   ├── Resources.resx
│   ├── Resources.fr-FR.resx
│   └── Resources.Designer.cs
└── Interop/Helpers/
    ├── PackageHelper.cs
    └── PipeClient.cs
```

## Localization Rules

- XAML uses `Text="{x:Static resx:Resources.KeyName}"`.
- C# uses `EarTrumpet.Properties.Resources.KeyName`.
- In `App.xaml.cs`, use the full `EarTrumpet.Properties.Resources` namespace because `Properties` is ambiguous.
- When adding UI text, update `Resources.resx`, `Resources.fr-FR.resx`, and `Resources.Designer.cs` together.

## Settings Storage

- Installed mode: `HKCU\Software\EarTrumpet` via `RegistrySettingsBag`
- Portable mode: `settings.json` next to the exe when `portable.marker` exists
- Store/MSIX packaging is only for the Microsoft Store release path. Installed mode still uses registry settings; portable mode still uses `settings.json` next to the exe.

## Key Design Decisions

- Use pack URIs without assembly qualifiers: `pack://application:,,,/Assets/file.ext`
- For animatable WPF visuals, use inline `<SolidColorBrush>` elements instead of raw color attributes
- `#if DEBUG` should not gate features that must exist in Release
- `PrepareToInstall` in `installer.iss` kills `BetterTrumpet.exe`
- Moving a tag makes the GitHub release draft again; republish with `--draft=false`
- Public EXE/setup signing is not configured yet. For public Authenticode signing, use a trusted code-signing certificate or Microsoft Trusted Signing; self-signed certificates are only useful for dev/test. Sign before hashing, Chocolatey/Winget updates, and GitHub upload.

## Startup And First Run

- `Left Ctrl` at startup forces onboarding
- `Left Shift` at startup forces changelog
- `HasShownFirstRun` is presence-based. Deleting `HKCU\Software\EarTrumpet\hasShownFirstRun` forces onboarding; writing `false` is not the same thing. The key is written when onboarding completes (or is skipped), not when the window is first shown, so a crash during first-run still reopens onboarding.
- The tray icon can become active before all startup work is finished. Keep tray icon code null-safe.

## Onboarding

Current flow is 5 pages:

1. Audio output
2. Appearance
3. Privacy
4. Ready
5. Tray pin

Notes:

- `TrayPin.gif` is animated via `XamlAnimatedGif`.
- Telemetry is staged in the onboarding ViewModel and only applied on the privacy step.
- The appearance step can either keep system colors or apply the custom BetterTrumpet palette.
- Disabling telemetry during onboarding requires an explicit confirmation because telemetry is used for crash, bug, and memory-leak diagnostics and no data is sold.
- The final tray pin page is the last step, not a decorative extra.
- The onboarding text is localized in EN and FR.

## Current Branch Notes

Recent work in `master` includes:

- Imported Cursor fixes for #33/#40/#36/#39/#30/#41/#43: focus-lost attenuation supports a configurable fade and app scope; mixer size persists; default-device changes can notify; tray-icon choice lives under Appearance; folder-rule empty states keep explicitly added profiles visible; session teardown is idempotent; icon callbacks are dispatcher-safe; and manual diagnostics warn first, stage files for review, sanitize user-folder paths, and honor stored telemetry consent. Telemetry opt-out does not disable update checks; they are independent.
- GitHub #37 remains handled by `36cb0e30` (`fix: reconcile stale sessions after default switch`). We retained only the independent .NET 8 WinRT HSTRING/IInspectable ABI fix required for per-app endpoint routing; Cursor's duplicate-session/reconciliation and RDP persistence refactors were excluded. #7 (RDP volume persistence) remains an audit item and is not implemented.
- Volume slider thumb offset and animation interruption fix: `SetPositionByControlPoint` now compensates thumb width so the thumb center aligns with the click position. Track clicks no longer use smooth animation because the flyout closes on mouse-up and unloads the slider, which cancels `CompositionTarget.Rendering` mid-animation and leaves `Value` stuck partway (e.g., at 92 instead of 100). This also resolved an intermittent drag-resets-to-original-position issue caused by imprecise `Value` writes interacting with Lock rule enforcement via `ApplyRuleToApp`.
- Folder launch-volume defaults: App rules now include custom folder defaults that apply a chosen starting volume to desktop sessions whose executable path is under the configured folder, recursively. The deepest matching folder wins. Explicit app `Set at launch` and `Lock` volume rules remain higher priority; hard mute still composes with a folder default. Folder-default settings export and import with the rest of the profile.
- CLI `set-default` now switches and verifies both Windows `Console` and `Multimedia` playback roles. COM failures or unchanged endpoints return an error instead of a false `ok: true`; `GetDefaultDevice(role)` must query the requested role rather than always reading `Multimedia`.
- The flyout uses tray icon bounds only to select the target monitor; its position remains anchored to that monitor's taskbar edge. It enters the topmost band before opening animations begin so another always-on-top window cannot cover it mid-animation.
- 3.2.1: persistent per-app volume rules support `Set at launch` and `Lock`, share one settings entry with hard mute, remain editable live from the flyout and Settings, and migrate/import legacy hard-mute data.
- Media-popup stability improvements coordinate it with the main flyout, add a larger hover tolerance and hide delay, prefer actively playing media sessions, and invalidate stale thumbnails when the selected session changes.
- CLI text is UTF-8 end to end, including named-pipe framing, attached-console output, redirected output, help text, device/app names, and JSON responses.
- Microsoft Store/MSIX builds use Store-owned updates only; unpackaged GitHub, Chocolatey, and Winget builds retain the GitHub/Inno updater.
- Single-bar peak-meter styles are vertically centered instead of inheriting the negative offset used by the two-bar Classic style.
- Custom Appearance colors now override the `FlyoutBackground` theme reference as a translucent content tint, as well as the acrylic tint, so `Window BG` is visible in the live flyout instead of being replaced by the Windows accent without losing the blur. `Window background opacity` controls the Acrylic tint from 5% to 100%, persists in settings, is included in settings export/import, and applies only when the slider is released to avoid repeated DWM acrylic reconfiguration during dragging. The Appearance palette shows live hex values, uses flatter swatches, and includes three restrained BetterTrumpet house presets (`Midnight Studio`, `Graphite`, `Night Shift`).

- Public 3.1.0 release on GitHub with setup exe, portable zip, and checksum file
- 3.1.0 hotfix: when custom slider colors are disabled, volume bars and peak meters now reapply theme brushes instead of falling back to white WPF defaults
- 3.1.1 hotfix: app-managed launch-at-startup now writes `BetterTrumpet.exe` via `Environment.ProcessPath` instead of `BetterTrumpet.dll` from `Assembly.Location`
- Release packaging hardening: `Release|x86` is self-contained, and file/product versions display `3.1.1`
- Onboarding refactor to a calmer 5-step flow with localized text, working option cards, and telemetry opt-out confirmation
- CLI app mute support via `--toggle-mute --app` and friendly `toggle-mute APP`
- QuickTrumpet / preset support expansion: `resolve-apps`, `rule-preview`, `rule-apply`, `preset-create`, plus aliases like `save`, `apply`, `mode`, `presets`
- Theme and slider color fix so custom colors no longer fall back to white bars
- App item entrance animation cleanup via `AnimateOnLoad`
- App mute/unmute visual polish: app rows fade smoothly when mute state changes
- Ctrl+click solo-mute feedback: subtle micro-scale animation on the clicked app, without accent glow
- Hidden app polish: hide/unhide uses fade, slide, and micro-scale only; avoid delayed or heavy layout-height animation
- Tray context menu polish: clearer order, shorter labels, localized EN/FR resources, no hard-coded English labels
- Tray context menu acrylic redesign: right-click tray menu now uses section headers, roomier rows, left glyphs, right-side checks/chevrons, blue translucent fallback styling, and `AccentPolicyLibrary.EnableAcrylic` blur on the popup when available.
- Changelog window hardening: fixed the missing `PrimaryButton` StaticResource by using the onboarding button style and localized the window strings
- Diagnostics hardening: manual diagnostics now export a `.zip` support bundle with logs and snapshot data; crash dialogs create an exception bundle and copy its path to the clipboard
- Tray icon startup hardening: null-safe icon handling and first-frame readiness
- Startup registry fix: app-managed launch-at-startup now writes `BetterTrumpet.exe` via `Environment.ProcessPath` instead of `Assembly.Location`, which points at `BetterTrumpet.dll` on .NET 8.
- Docs updates in `docs/CLI.md`
- After the experimental redesign was fully reverted, a new minimal media-popup pass removed only the blurred album-art background, dark scrim, glow, and shimmer. The popup surface now reuses the flyout's `FlyoutBackground` content brush and `AcrylicColor_Flyout` Windows Acrylic tint; media/session behavior remains at the `HEAD` baseline.
- Hard mute (persistent per-app mute): apps can be flagged "keep muted" from the flyout app focus menu. A hard-muted app is force-muted every time one of its audio sessions appears, including after relaunch or reboot. Keyed by `ExeName` (stable across restarts, unlike AppId/session ids). Stored in `AppSettings` as `HardMutedAppEntriesJson`, applied in `DeviceViewModel.AddSession` and re-applied via the `HardMutedAppsChanged` event (`DeviceViewModel.ApplyHardMuteState`). Toggle lives in `FocusedAppItemViewModel` as a checkable menu item; localized keys `HardMuteAppButtonText`/`HardMuteAppMenuText` (EN/FR). Included in settings export/import via the `HardMutedAppsJson` passthrough. WASAPI note: an app with no open audio session cannot be pre-muted because Windows exposes no per-app volume object until first playback; hard mute takes effect the moment the session is created. Disabling hard mute leaves the current mute state untouched so the user stays in control.
- Per-app volume rules are editable live from both the flyout and Settings. The Settings card exposes only `Set at launch` and `Lock`; removing the rule replaces the former visible `None`/`Free` choice. App-rule changes reconcile in place while Settings is open so sliders and focus are preserved.
- 3.1.2 (in development): monitored recording-device sessions from Windows "Listen to this device" are no longer collapsed into one system-sounds app row when WASAPI exposes distinct grouping parameters. `AudioDeviceSessionCollection.AddSystemSoundsSession()` separates system-sounds session groups by `GroupingParam`, and `AppItemViewModel.DoesGroupWith()` keeps system-sounds rows distinct by session id so each listened-to device can be adjusted independently from the main flyout.
- Default-device session reconciliation: when Windows leaves an old per-app session on the previous endpoint after a system-default switch, a session arriving on the current default endpoint hides matching implicit-route sessions on every other endpoint. Explicit per-app persisted routes are left untouched, and the reconciliation also covers sessions previously held in `_movedSessions` so switching A -> B -> A does not recreate stale duplicates.
- 3.2.0 adds a hidden monkey volume-sound easter egg: four clicks on the BetterTrumpet logo in About unlock and enable three cleaned PCM/WAV clips selected by volume (`monkeylow.wav` at 0-20, `monkeymid.wav` above 20 and below 85, `monkeyhigh.wav` at 85-100), then reveals a persistent toggle on the About page. Low is sourced from `monkeylow.mp3`, mid from the shorter `monkeymid2.mp3`, and high keeps its existing source. The normal tick remains independently disableable in mouse/volume settings. `MonkeyTickSoundUnlocked`, `UseMonkeyTickSound`, and `UseVolumeTickSound` participate in settings export/import. `MonkeySoundPlayer` alternates two channels, overlaps repetitions by 75 ms, and crossfades range changes over 40 ms. Audio cleanup uses a conservative `-50 dB` threshold and only compresses silences longer than 40 ms, preserving quiet monkey details while removing MP3 padding and long gaps.
- The post-update changelog is now a compact confirmation window showing the installed version, with `OK` and a localized link to `https://bettertrumpet.com/changelog`. It no longer downloads or renders full release notes inside the app.
- The tray context menu is anchored to the monitor work area rather than the click's Y coordinate. After opening, its popup HWND is clamped with an 8-DIP gap from the taskbar/work-area edges, preventing the menu from overlapping the taskbar across bottom, top, left, and right taskbar layouts and DPI scales.

## Release State

3.1.1 has been released as a startup hotfix:

- GitHub Release: `https://github.com/xammen/BetterTrumpet/releases/tag/v3.1.1`
- Tag `v3.1.1`: annotated tag on `13b8b884`
- `master` and `migration/net8`: both pushed to `13b8b884`
- GitHub assets:
  - `BetterTrumpet-3.1.1-setup.exe` SHA256 `347F9ED0AC304A0A5FFC16D1968055960047620ECD67D96214E23A89318A7CEE`
  - `BetterTrumpet-3.1.1-portable.zip` SHA256 `209594B31E6B10D251DBF52EFB013BDC5B5689123FC73C9D60E2EAC630424DCD`
- Chocolatey `bettertrumpet.3.1.1.nupkg` was pushed successfully and is pending moderation.
- Winget PR is open: `https://github.com/microsoft/winget-pkgs/pull/390442`.
- Microsoft Store package/submission is a separate Partner Center path; do not mix Store artifact versioning with GitHub/Choco/Winget without checking the Store manifest.

3.2.0 was released on 2026-07-13:

- GitHub Release: `https://github.com/xammen/BetterTrumpet/releases/tag/v3.2.0`
- Tag `v3.2.0`: annotated tag on `3ab038c1`
- `master` and `migration/net8` were pushed with the complete release source.
- GitHub assets:
  - `BetterTrumpet-3.2.0-setup.exe` SHA256 `A1040A2E8C3988DABED29E9050BBC76079537446B6228C9F51650D321DC75011`
  - `BetterTrumpet-3.2.0-portable.zip` SHA256 `5BEA8FEAA70437286B7CA93E12F44236236B851699174443B93566BB46B6C9EE`
- Chocolatey `bettertrumpet.3.2.0.nupkg` was pushed successfully and is awaiting automated checks/moderation.
- Winget PR: `https://github.com/microsoft/winget-pkgs/pull/401693` (open, CLA passed, WinGetSvc checks running at submission time).
- Microsoft Store Partner Center product `9PKBH40D32G8`, submission `1152921505701257832`, was submitted for certification on 2026-07-14 with `EarTrumpet.Package_3.2.0.0_x86_bundle.msixupload` (SHA256 `D39B5E25E1DE64F4BFC460A9614C2E7EF2EBA21324A2AC11983BEA6CF4383BB8`). Partner Center accepted package version `3.2.0.0`; preprocessing is in progress and publication is configured to start automatically after certification. The package carries the expected `runFullTrust` restricted-capability warning, which remains subject to Microsoft approval.

3.2.1 was released on 2026-07-29:

- GitHub Release: `https://github.com/xammen/BetterTrumpet/releases/tag/v3.2.1`
- Tag `v3.2.1`: annotated tag on `2ae6ecf`
- `master` was pushed with the complete release source. `migration/net8` remains intentionally unchanged at `7a6e8f9`.
- GitHub assets:
  - `BetterTrumpet-3.2.1-setup.exe` SHA256 `3C20BC45B6F07A2582165DB39BE85202CC7D3ABBB9C9BE5399BDF5331B70E7A9`
  - `BetterTrumpet-3.2.1-portable.zip` SHA256 `22366AFD1BC7B8A7BC5D3BDADF9095397EA57C32DBE0ACDDD29A4E9ED94E3D75`
- Chocolatey `bettertrumpet.3.2.1.nupkg` was pushed successfully and is visible from the community feed query; automated checks/moderation may still be pending.
- Winget PR: `https://github.com/microsoft/winget-pkgs/pull/409308` (open, CLA passed, remaining checks pending at submission time).
- Microsoft Store packaging was not part of the 3.2.1 release task; keep the existing Store submission path separate.

3.2.3 was released on 2026-08-04:

- GitHub Release: `https://github.com/xammen/BetterTrumpet/releases/tag/v3.2.3`
- Tag `v3.2.3`: annotated tag on `69a3012`; `master` was pushed with the complete source and distribution metadata. `migration/net8` remains intentionally unchanged at `7a6e8f9`.
- GitHub assets:
  - `BetterTrumpet-3.2.3-setup.exe` SHA256 `1E06CEDDE3BFA04CAD7771CF9E4B2359F91BD26DFC03256FAA9800125FA2A42C`
  - `BetterTrumpet-3.2.3-portable.zip` SHA256 `AC89F51CB14CBB81CFDC8784D228CC0A4E9B1243BFF15F9AC42748B9DE2D337A`
- Chocolatey `bettertrumpet.3.2.3.nupkg` was pushed successfully; catalog visibility and moderation remain pending.
- Winget PR is open: `https://github.com/microsoft/winget-pkgs/pull/411985` (CLA passed; remaining checks pending at submission time).
- Microsoft Store packaging was not part of the 3.2.3 release task.

If replacing same-version GitHub assets again, update hashes everywhere before or immediately after upload. Winget and Chocolatey verify the setup hash and will fail if the GitHub asset changes without their metadata changing.

## CLI

`bt.cmd` maps to `BetterTrumpet.exe`.

The CLI surface now includes:

- `list-devices`, `list-apps`, `get-volume`, `set-volume`, `set-device`
- `mute`, `unmute`, `toggle-mute`
- app-friendly aliases that accept `--app NAME`
- QuickTrumpet preset commands and rule/preset helpers
- update, settings export/import, and health commands

Device matching is partial (`IndexOf`). App matching is exact on `ExeName` or `DisplayName`.
Treat `docs/CLI.md` as the user-facing syntax reference when in doubt.

## Theme And Volume UI

`EarTrumpetColorsSettingsPageViewModel.cs` owns the theme engine:

- 7 color channels: thumb, fill, track background, peak meter, window background, text, accent glow
- built-in presets plus custom theme save/load/import/export
- dynamic album art theme mode

`UI/Controls/VolumeSlider.cs` is sensitive:

- custom slider colors now fall back to theme defaults instead of white
- when custom slider colors are disabled, reset code must reapply the resolved theme brushes; `ClearValue` alone can expose white WPF fallback bars and hide peak meters
- the peak meter default should stay accent-colored
- tick sound playback is in this control

`ThemeRegistry.cs` defines the default palette.

## Media Popup (minimal Acrylic background pass)

- Media popup placement now converts the physical tray icon and monitor work area to WPF DIPs, keeping hover placement correct at custom Windows scaling values. While visible, a lightweight fallback check detects media providers that miss track-change events and refreshes the title and thumbnail cache.

The experimental 2026-07-10 popup redesign was reverted completely. A new isolated visual pass now changes only the popup surface while keeping `MediaSessionService.cs` and all session behavior at the `HEAD` baseline.

- `MediaPopupWindow` no longer paints album art across the full popup or applies the old dark gradient, dominant-color glow, or shimmer animation.
- The root content surface uses `Theme:Brush.Background="FlyoutBackground"` under the `Flyout` theme scope.
- Window Acrylic uses the same `AcrylicColor_Flyout` reference as the main flyout and refreshes on theme changes; the effect is disabled again when the popup hides.
- Popup Acrylic enforces a minimum tint alpha of `0xA8`, making the Windows backdrop clearly visible while retaining enough dark tint for control legibility.
- Media controls now use local Phosphor Bold `Path` geometries (play/pause, skip, shuffle, repeat, volume, caret) from `PhosphorIconData` instead of mixed thin Segoe MDL2 glyphs.
- Media-control glyphs stay deliberately compact inside unchanged hit targets. The expand/collapse caret crossfades between separate up/down paths with a 2-DIP directional slide instead of rotating 180 degrees.
- The collapsed caret points up (expand action) and the expanded caret points down (collapse action); its crossfade starts immediately with no delayed incoming phase.
- Expand/collapse animates `Window.Height`/`Top` only with short `FillBehavior=Stop` clocks over final base values, while the timecode uses local interpolation and performs no COM refresh during the transition. Artwork adds a 6-DIP fade/slide/micro-scale; stale cover-fade completions are state-guarded.
- The popup entrance storyboard is controllable and finalized into base opacity/transform values on completion or before the first caret action, so its `HoldEnd` clocks cannot conflict with the first expansion.
- Track changes no longer scale/flash the entire popup. Only the title and expanded artwork use a short fade/6-DIP slide; the new title returns independently of slower artwork loading, and late artwork gets a 140 ms fade.
- Each decoded track thumbnail animates the shared media accent and volume gradient to its dominant color over 320 ms. New transitions start from the currently rendered color to avoid flashes during rapid track changes.
- The timecode is now a real WPF `Slider`: click-to-seek and dragging update the time optimistically and send one seek on release.
- SMTC often advances its reported timeline only every 3-5 seconds. The popup stores each SMTC position as an anchor and interpolates locally at 100 ms while playing; SMTC events, pause/play, track changes, and seeks resynchronize the anchor without polling COM at render frequency.
- Mouse seeking is owned explicitly by the popup instead of relying on WPF `Slider`/`RepeatButton` command ordering: pointer down captures the mouse and maps X to duration, move previews continuously, and pointer up commits exactly one SMTC seek. Lost capture also commits the last preview safely.
- After a seek, stale SMTC positions are ignored until Windows reports a position within 2 seconds of the optimistic anchor or a 5-second safety timeout expires; direct clicks therefore cannot snap back to the old timecode.
- Seek requests await the SMTC result and retry after 180 ms; a final delayed retry is used only when the provider still rejects the command.
- Popup storyboards and active/inactive icon brushes are cached; hidden track changes only preload artwork instead of refreshing the whole view.
- The expanded foreground artwork remains unchanged, including its low-resolution fallback handling.
- The expanded artwork uses a cheap flat 2-DIP translucent shadow layer instead of WPF `DropShadowEffect`, avoiding first-use effect preparation during the caret interaction.
- The obsolete blur preview/slider was removed from the media-popup settings page. `AppSettings.MediaPopupBlurRadius` remains tolerated for stored-settings compatibility but is no longer consumed by the popup.
- The media setting is presented as `ShowWhenPaused` while retaining the inverse `MediaPopupShowOnlyWhenPlaying` storage key for compatibility. When enabled, tray hover opens only if Windows exposes a controllable paused/playing session, allowing Play to resume it without showing an empty popup.
- Media-popup volume is app-only. It resolves the current SMTC `SourceAppUserModelId` against flyout `AppId`/`ExeName`, locks that app for the duration of a drag, and never falls back to the default-device/master volume. If no reliable app session is available, the volume row is disabled.
- The known SMTC limitation remains: Windows may choose a browser session/thumbnail instead of Spotify in some multi-session situations.
- Any future session-selection fix should be developed and verified separately from a visual redesign so behavior can be validated before changing the UI.
- Do not restore the discarded planning files or assume source navigation, snapshot coordination, color modes, or the redesigned sliders are currently implemented.

## Distribution Notes

- GitHub Releases are the canonical source for setup exe and portable zip.
- Winget and Chocolatey consume the GitHub setup exe and validate SHA256.
- Scoop is a good next channel because the portable zip already exists; start with a personal bucket before attempting Scoop Extras.
- A web-hosted MSIX `.appinstaller` can add App Installer-based installs outside Store, but it is separate from the current Inno setup path.
- npm is possible only as a dev-oriented wrapper package that downloads the GitHub installer or portable zip; do not treat npm as the primary Windows distribution channel.
- Intune/enterprise deployment can reuse Inno silent switches: `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-`.

## Tray And Startup Hardening

- Flyout positioning uses the HWND's native DPI and repositions after `WM_DPICHANGED`, preventing stale WPF DPI transforms from making the flyout content overflow after repeated Windows scaling changes.

Important recent fixes:

- `TaskbarIconSource` must survive the window asking for the tray icon before animation has populated the first frame
- `ShellNotifyIcon` now tolerates a missing current icon
- `IconExtensions.AsDisposableIcon()` must handle null safely

Do not undo these changes unless you are actively replacing the tray pipeline.

## Tray Context Menu

`GetTrayContextMenuItems()` in `App.xaml.cs` owns the tray right-click menu.

Current intended order:

1. `SORTIE AUDIO` / audio output header, then playback devices
2. Hidden app/device restore menus, only when needed
3. Add-on items, when present
4. `ACTIONS` header, then BetterTrumpet primary actions: open mixer, open settings
5. Update/install action, only when an update is available
6. Support/info actions: check updates, what's new, onboarding, GitHub
7. `AUTRES OUTILS` / other tools header, then Windows audio tools submenu
8. Exit, isolated at the bottom

Menu labels must be localized through `Resources.resx`, `Resources.fr-FR.resx`, and `Resources.Designer.cs`. The recent tray-only keys are `TrayOpenVolumeMixer`, `TrayOpenSettings`, `TrayShowOnboarding`, `TrayWhatsNew`, `TrayStarProject`, `TrayWindowsAudioTools`, `TraySectionAudioOutput`, `TraySectionActions`, and `TraySectionOtherTools`. Hard-coded glyph values like `"\xE713"` are acceptable because they are Segoe MDL2 icon codes, not user-visible text.

The visual template lives in `App.xaml` under the global `ContextMenu`/`MenuItem` styles. `ShellNotifyIcon.ShowContextMenu()` applies acrylic blur to the popup HWND via `AccentPolicyLibrary.EnableAcrylic`; the XAML gradient is the fallback when DWM/acrylic is unavailable.

Tray menu primary icons can use local Phosphor Bold geometries from `UI/Helpers/PhosphorIconData.cs` via `ContextMenuItem.IconData`/`IconScale`. Keep the legacy `Glyph` populated as a Segoe MDL2 fallback and avoid webfont/CDN dependencies.

## Diagnostics And Logs

`ErrorReporter` wires Trace to both an in-memory circular listener and `FileTraceListener`.

- Installed logs: `%APPDATA%\BetterTrumpet\logs`
- Portable logs: `config\logs` next to the executable
- Log rotation: `bettertrumpet-*.log`, max 5 files of 5 MB
- Manual export: Settings -> About -> `TroubleshootEarTrumpetText`
- Manual export warns that the bundle can contain app/device names and logs, writes a staging folder for review/edit, then zips on confirmation
- Exported text replaces `C:\Users\<name>\...` with `%USERPROFILE%` / `%APPDATA%` / `%LOCALAPPDATA%` / `%TEMP%`
- Crash handling creates a diagnostic bundle immediately (no review UI) with the exception and recent logs, without taking a live audio snapshot to avoid cascading failures
- Crash dialogs are localized (EN/FR) and show a sanitized path
- Sentry initializes only after stored telemetry consent or a completed first-run; GitHub update checks wait until `hasShownFirstRun` exists. Telemetry opt-out does not disable updates.

The diagnostic zip can contain app names, device names, process IDs, endpoint IDs, settings state, and recent logs. Keep this clear in user-facing copy when asking users to attach it.

## Common Pitfalls

1. Build before tag gives the wrong version in the binary.
2. `git add -A` can accidentally scoop up `dist/`.
3. `Resources.Designer.cs` is not auto-generated.
4. `Properties.Resources` in `App.xaml.cs` is ambiguous.
5. Frozen WPF brushes crash when animated.
6. Custom theme colors that are stored as `Transparent` are meant to mean "use the current default", not "render white".
7. The onboarding first-run flag is presence-based, not bool-based.
8. Replacing GitHub release assets changes their SHA256; Chocolatey and Winget must be updated or installs will fail checksum validation.
9. `VolumeSlider.ResetVisualElementColors()` must not rely on `ClearValue` alone after `Theme:Brush` has written local values.
10. For startup/run entries on .NET 8, do not use `Assembly.GetExecutingAssembly().Location`; it points to `BetterTrumpet.dll`. Use `Environment.ProcessPath` for `BetterTrumpet.exe`.
11. SMTC's manager-level `GetCurrentSession()` can switch between Spotify and browser tabs between calls. This remains a known baseline limitation after the experimental fix was reverted; isolate any future behavioral fix from visual redesign work.
12. A custom window background must update both `Background` and `FlyoutBackground` refs. Keep `FlyoutBackground` translucent (`color/opacity/1`) so changing only `AcrylicColor_Flyout` does not leave the content painted with the system accent or remove the acrylic blur.
13. Telemetry (`IsTelemetryEnabled`) only gates Sentry. GitHub release checks are a separate `AutoCheckForUpdates` / `UpdateNotifyChannel` path.
14. #7 remains unaudited: do not claim RDP volume persistence is fixed until reconnect identity and per-session restoration are validated on a real RDP path.
15. The WinRT audio-policy factory is projected as `InterfaceIsIUnknown` with the three explicit `IInspectable` ABI slots. Removing those slots can crash the app during session enumeration; do not revert to `InterfaceIsIInspectable` on .NET 8.
16. `VolumeSlider.SetPositionByControlPoint` must compensate thumb width: `percent = (point.X - thumbWidth/2) / (ActualWidth - thumbWidth)`. Without this, the thumb center is offset from the click position by up to half the thumb width, and clicking the right end only reaches ~92 while the left end only reaches ~8.
17. Do not use smooth animation (`animate: true`) when clicking on the volume slider track. The flyout closes on mouse-up and unloads the slider, which cancels the `CompositionTarget.Rendering` subscription mid-animation and leaves `Value` stuck partway to the target. Set the value directly (`animate: false`) for track clicks.

## Validation Status

- `x86 Release` rebuild passes for 3.2.3. `BetterTrumpet.exe` reports `FileVersion=3.2.3` and `ProductVersion=3.2.3`; the output includes `coreclr.dll`, `hostfxr.dll`, and `hostpolicy.dll` for the x86 self-contained runtime.
- `BetterTrumpet-3.2.3-setup.exe` and `BetterTrumpet-3.2.3-portable.zip` were generated on 2026-08-04. Setup SHA256: `1E06CEDDE3BFA04CAD7771CF9E4B2359F91BD26DFC03256FAA9800125FA2A42C`; portable SHA256: `AC89F51CB14CBB81CFDC8784D228CC0A4E9B1243BFF15F9AC42748B9DE2D337A`. The ZIP contains `portable.marker`, excludes PDBs, and includes the self-contained runtime. Public signing is still not configured.
- Chocolatey `choco pack` produced `bettertrumpet.3.2.3.nupkg`, and the canonical three-file Winget manifest set validates successfully under `winget-manifest/manifests/x/xmn/BetterTrumpet/3.2.3`.
- `x86 Release` rebuild passes for the released 3.2.1 source. `BetterTrumpet.exe` reports `FileVersion=3.2.1` and `ProductVersion=3.2.1`; the output includes the x86 self-contained runtime.
- `BetterTrumpet-3.2.1-setup.exe` and `BetterTrumpet-3.2.1-portable.zip` were generated on 2026-07-29. Setup SHA256: `3C20BC45B6F07A2582165DB39BE85202CC7D3ABBB9C9BE5399BDF5331B70E7A9`; portable SHA256: `22366AFD1BC7B8A7BC5D3BDADF9095397EA57C32DBE0ACDDD29A4E9ED94E3D75`. Public signing is still not configured.
- Chocolatey `choco pack` produced `bettertrumpet.3.2.1.nupkg`, and the canonical three-file Winget manifest set validates successfully under `winget-manifest/manifests/x/xmn/BetterTrumpet/3.2.1`.
- `x86 Release` build passes as of `2026-06-19` after the 3.1.1 startup hotfix
- `Build\Release\BetterTrumpet.exe` reports `FileVersion=3.1.1` and `ProductVersion=3.1.1`
- `build-portable.ps1` and `installer.iss` both succeeded for the 3.1.1 hotfix assets
- Onboarding first-run launch was exercised successfully
- Latest onboarding log showed no onboarding crash
- The previous `ChangelogWindow` StaticResource crash has been fixed
- `x86 Release` rebuild passes as of `2026-07-10` with the minimal media-popup Acrylic background pass. Startup is clean (`MediaPopup initialized`, no popup-related exception), and the binary remains on the 3.1.1 version line. Visual confirmation at real tray hover is pending creator review.
- `x86 Release` rebuild also passes after the media accent transition, action-oriented caret, and paused-session popup option. Runtime startup remains clean and a 300x300 SMTC thumbnail decodes successfully.
- `x86 Release` rebuild passes for 3.2.0. `BetterTrumpet.exe` reports `FileVersion=3.2.0` and `ProductVersion=3.2.0`; the portable ZIP and Inno Setup installer were generated, Chocolatey packaging succeeded, and the isolated three-file Winget manifest set validates successfully.

## Release Notes Convention

GitHub release notes must be:

- In English
- Human-written with a lightly conversational, casual tone; avoid generic AI or marketing copy
- Format: `## BetterTrumpet vX.Y.Z`, then `### Section`, then `- **Feature** - description`
