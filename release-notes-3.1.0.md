## BetterTrumpet v3.1.0

### Modernized Foundation

- **.NET 8 Migration** - BetterTrumpet now runs on a modern .NET 8 Windows foundation while preserving the existing x86 desktop workflow.
- **Release Packaging** - Installer, portable, Chocolatey, Winget, and Microsoft Store package metadata have been aligned for the 3.1.0 release line.

### Onboarding And Privacy

- **Focused First Run** - The onboarding flow now starts directly with audio setup and guides users through appearance, privacy, readiness, and tray pinning.
- **Telemetry Confirmation** - Turning telemetry off now requires explicit confirmation and explains that telemetry is used for bugs, crashes, and memory-leak diagnostics, not data resale.
- **Appearance Setup** - Appearance choices are now selectable during onboarding, including the system look and the BetterTrumpet palette.

### QuickTrumpet And CLI

- **QuickTrumpet Presets** - Added preset workflows for saving, applying, previewing, and creating reusable audio setups.
- **Automation Commands** - Expanded the CLI with app resolution, rule preview/apply, preset helpers, batch execution, and friendlier aliases.
- **App Mute Control** - Added app-compatible mute, unmute, and toggle-mute flows for scripts, launchers, and shortcuts.

### Mixer Polish

- **Theme Engine** - Improved custom color handling so transparent saved values correctly fall back to the current theme defaults.
- **Volume Feedback** - Added smoother volume slider motion, optional volume tick feedback, and better peak meter behavior.
- **Session Motion** - Refined app row animations for session appearance, mute changes, hide/unhide, and solo-mute feedback.

### Reliability And Diagnostics

- **Tray Startup Hardening** - Improved tray icon null safety during startup so early shell icon requests do not crash the app.
- **Support Bundles** - Manual diagnostics and crash handling now create zip bundles with recent logs and support context.
- **Changelog Stability** - Hardened the changelog window resources and localized copy so update notes can open reliably.
