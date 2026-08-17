# Information Collected And Transmitted By BetterTrumpet

First, a reminder: BetterTrumpet is provided "as is", without warranty of any kind, express or
implied, including but not limited to the warranties of merchantability,
fitness for a particular purpose and noninfringement.

BetterTrumpet has two independent network features. Opting out of telemetry does **not**
disable update checks.

## Crash reporting (Sentry) — gated by telemetry consent

When **Send diagnostic data** is enabled, unhandled exceptions may be sent to Sentry
(`ingest.de.sentry.io`). No default PII is attached. Scope tags are limited to version,
OS, architecture, portable mode, and .NET version. Exception payloads can, in rare cases,
contain paths to applications on your computer.

Sentry is not initialized:

* Before first-run onboarding completes, if no stored telemetry consent exists yet
* When the user has opted out (`IsTelemetryEnabled = false`)
* On Microsoft Store / MSIX builds that never start the GitHub updater (Sentry still
  follows the telemetry toggle)

### Application-Level (crash time)

Includes:

* Exception information
  * Could, in rare cases, contain paths to applications on your computer
* Version number (e.g. 3.2.3)
* App state (e.g. is shutting down)
* App identity present (true/false)
* Time between starting and crashing
* Handle count
* GDI and User object counts

### Operating System-Level

Includes:

* Architecture (e.g. 32-bit)
* Windows Build
* Available processors/cores
* .NET runtime version
* Light/Dark mode configuration
* Right-to-Left configuration
* Transparency configuration
* Accent color configuration
* System Animations configuration
* High Contrast theme configuration
* Language and region (e.g. en-US)

## Update checks (GitHub) — independent of telemetry

Unpackaged GitHub / Chocolatey / Winget builds may query
`https://api.github.com/repos/xammen/BetterTrumpet/releases/latest` when
**auto-check for updates** is enabled. This starts only after first-run onboarding
completes. Microsoft Store packages do not use this updater.

Downloading an update (user-initiated) fetches the GitHub setup executable.

## Manual diagnostic export

Settings → About → export diagnostic report creates a local zip. It is **not** uploaded
automatically. The export flow:

1. Warns that the bundle can contain app names, device names, process IDs, endpoint IDs,
   settings state, and recent logs
2. Writes a staging folder so the files can be reviewed or edited
3. Replaces user folder paths with `%USERPROFILE%`, `%APPDATA%`, `%LOCALAPPDATA%`, and `%TEMP%`
4. Zips the folder only after confirmation

Crash dialogs still create a zip immediately (no interactive review) so a dying process
can save evidence. Paths inside that zip are still sanitized.

### Third-Party Policies

* Sentry https://sentry.io/privacy/
* GitHub https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement
* Microsoft Store https://docs.microsoft.com/en-us/legal/windows/agreements/store-policies
