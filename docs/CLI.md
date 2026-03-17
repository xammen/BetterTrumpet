# BetterTrumpet CLI Reference

> Command-line interface for controlling Windows audio from the terminal.

## Overview

BetterTrumpet exposes a full CLI that lets you control system audio — devices, apps, volumes, muting, profiles — directly from PowerShell, cmd, or any scripting environment.

### How It Works

```
 Terminal                          Tray App
 ───────                          ────────
 BetterTrumpet.exe --list-apps
        │
        ├──► Named Pipe (BetterTrumpet_IPC_v1) ──► Running Instance
        │                                              │
        │                                          Processes on
        │                                          UI thread (STA)
        │                                              │
        ◄──────────── JSON response ◄──────────────────┘
        │
   Pretty-prints JSON
   Exits
```

- The CLI launches a **second BetterTrumpet.exe process** that sends a command to the already-running tray instance via a Windows named pipe (`BetterTrumpet_IPC_v1`).
- The tray instance processes the command on the UI thread (required for COM audio / STA) and returns a **JSON response**.
- The CLI process prints the response and exits.
- **BetterTrumpet must be running** for all remote commands. Only `--help` and `--version` work locally.
- All argument prefixes are accepted: `--`, `-`, `/` (e.g., `--help`, `-help`, `/help`).

---

## Quick Start

```powershell
# Check if BetterTrumpet is running
BetterTrumpet.exe --ping

# List all audio devices and their apps
BetterTrumpet.exe --list-devices

# Set default device volume to 50%
BetterTrumpet.exe --set-volume 50

# Mute Discord
BetterTrumpet.exe --mute --app discord

# Bump Spotify volume up by 10
BetterTrumpet.exe --set-volume +10 --app spotify

# Switch default output to your headphones
BetterTrumpet.exe --set-default "Headphones"
```

---

## Command Reference

### Summary Table

| Command | Description | Requires Running Instance |
|---|---|:---:|
| `--help` | Show help text | No |
| `--version` | Print version string | No |
| `--ping` | Check if BetterTrumpet is running | Yes |
| `--list-devices` | List all audio devices with nested apps | Yes |
| `--list-apps` | List all audio apps (deduplicated) | Yes |
| `--get-volume` | Get volume of default device or specific target | Yes |
| `--set-volume <value>` | Set volume (absolute or relative) | Yes |
| `--mute` | Mute a device or app | Yes |
| `--unmute` | Unmute a device or app | Yes |
| `--toggle-mute` | Toggle mute on a device | Yes |
| `--get-default` | Show the current default playback device | Yes |
| `--set-default <name>` | Change the system default playback device | Yes |
| `--set-device <app> <device>` | Route an app's audio to a specific device | Yes |
| `--list-profiles` | List saved volume profiles | Yes |
| `--apply-profile <name>` | Apply a saved volume profile | Yes |
| `--watch` | Snapshot all devices/apps with timestamp | Yes |

---

### `--help`

Print the full help text. Runs locally without the named pipe.

```
BetterTrumpet.exe --help
```

---

### `--version`

Print the version string. Runs locally without the named pipe.

```
BetterTrumpet.exe --version
```

```
BetterTrumpet v2.3.1.1
```

---

### `--ping`

Check whether the BetterTrumpet tray instance is running and responsive.

```
BetterTrumpet.exe --ping
```

**Response:**

```json
{
  "status": "ok",
  "version": "2.3.1.1"
}
```

---

### `--list-devices`

List all active audio playback devices with their nested audio sessions (apps).

```
BetterTrumpet.exe --list-devices
```

**Response:**

```json
[
  {
    "id": "{0.0.0.00000000}.{guid}",
    "name": "Headphones (BEACN Mic)",
    "volume": 80,
    "isMuted": false,
    "isDefault": true,
    "apps": [
      {
        "id": "{session-guid}",
        "name": "Spotify",
        "exeName": "spotify.exe",
        "volume": 65,
        "isMuted": false
      },
      {
        "id": "{session-guid}",
        "name": "Discord",
        "exeName": "discord.exe",
        "volume": 100,
        "isMuted": false
      }
    ]
  },
  {
    "id": "{0.0.0.00000000}.{guid}",
    "name": "Speakers (Realtek)",
    "volume": 50,
    "isMuted": true,
    "isDefault": false,
    "apps": []
  }
]
```

---

### `--list-apps`

List all audio apps across all devices, deduplicated. Useful for finding app names before targeting them with other commands.

```
BetterTrumpet.exe --list-apps
```

**Response:**

```json
[
  {
    "exeName": "spotify.exe",
    "displayName": "Spotify",
    "volume": 65,
    "isMuted": false,
    "device": "Headphones (BEACN Mic)",
    "processId": 14208
  },
  {
    "exeName": "discord.exe",
    "displayName": "Discord",
    "volume": 100,
    "isMuted": false,
    "device": "Headphones (BEACN Mic)",
    "processId": 9824
  }
]
```

---

### `--get-volume`

Get the current volume of a device or app.

| Syntax | Description |
|---|---|
| `--get-volume` | Default playback device |
| `--get-volume --device "BEACN"` | Specific device (partial name match) |

```
BetterTrumpet.exe --get-volume
```

**Response:**

```json
{
  "device": "Headphones (BEACN Mic)",
  "volume": 80,
  "isMuted": false
}
```

```
BetterTrumpet.exe --get-volume --device "Realtek"
```

```json
{
  "device": "Speakers (Realtek)",
  "volume": 50,
  "isMuted": true
}
```

> **Note:** Device matching is **case-insensitive** and supports **partial names**. `"BEACN"` matches `"Headphones (BEACN Mic)"`.

---

### `--set-volume`

Set the volume of a device or app. Supports **absolute** (0-100) and **relative** (`+N` / `-N`) values.

| Syntax | Description |
|---|---|
| `--set-volume 75` | Set default device to 75% |
| `--set-volume 75 --device "BEACN"` | Set specific device to 75% |
| `--set-volume 50 --app spotify` | Set app to 50% |
| `--set-volume +10` | Increase default device by 10 |
| `--set-volume -5 --app spotify` | Decrease app by 5 |

**Absolute example:**

```
BetterTrumpet.exe --set-volume 75
```

```json
{
  "ok": true,
  "device": "Headphones (BEACN Mic)",
  "volume": 75
}
```

**Relative example:**

```
BetterTrumpet.exe --set-volume +10 --app spotify
```

```json
{
  "ok": true,
  "app": "spotify.exe",
  "volume": 75,
  "delta": 10
}
```

> Volume is clamped to the 0-100 range. Setting `+20` when volume is 95 results in 100.

---

### `--mute`

Mute a device or app.

| Syntax | Description |
|---|---|
| `--mute` | Mute default device |
| `--mute --device "BEACN"` | Mute specific device |
| `--mute --app discord` | Mute specific app |

```
BetterTrumpet.exe --mute --app discord
```

```json
{
  "ok": true,
  "app": "discord.exe",
  "isMuted": true
}
```

---

### `--unmute`

Unmute a device or app. Same syntax as `--mute`.

| Syntax | Description |
|---|---|
| `--unmute` | Unmute default device |
| `--unmute --device "BEACN"` | Unmute specific device |
| `--unmute --app discord` | Unmute specific app |

```
BetterTrumpet.exe --unmute --device "Realtek"
```

```json
{
  "ok": true,
  "device": "Speakers (Realtek)",
  "isMuted": false
}
```

---

### `--toggle-mute`

Toggle the mute state of a device.

| Syntax | Description |
|---|---|
| `--toggle-mute` | Toggle mute on default device |
| `--toggle-mute --device "BEACN"` | Toggle mute on specific device |

```
BetterTrumpet.exe --toggle-mute
```

```json
{
  "ok": true,
  "device": "Headphones (BEACN Mic)",
  "isMuted": true
}
```

---

### `--get-default`

Show the current default playback device.

```
BetterTrumpet.exe --get-default
```

```json
{
  "id": "{0.0.0.00000000}.{guid}",
  "name": "Headphones (BEACN Mic)",
  "volume": 80,
  "isMuted": false
}
```

---

### `--set-default`

Change the system default playback device. Partial name matching is supported.

```
BetterTrumpet.exe --set-default "Headphones (BEACN Mic)"
```

```json
{
  "ok": true,
  "device": "Headphones (BEACN Mic)"
}
```

```
BetterTrumpet.exe --set-default "Realtek"
```

```json
{
  "ok": true,
  "device": "Speakers (Realtek)"
}
```

---

### `--set-device`

Route an app's audio output to a specific device. Uses the Windows per-app audio routing API (`AudioPolicyConfig`). The command first attempts to match by audio session, then falls back to process name lookup.

```
BetterTrumpet.exe --set-device spotify "Headphones (BEACN Mic)"
```

```json
{
  "ok": true,
  "app": "spotify.exe",
  "device": "Headphones (BEACN Mic)",
  "processesRouted": 3
}
```

> `processesRouted` indicates how many processes of that app were moved (some apps spawn multiple audio processes).

---

### `--list-profiles`

List all saved volume profiles.

```
BetterTrumpet.exe --list-profiles
```

```json
[
  {
    "name": "Night Mode",
    "devices": 3,
    "createdAt": "2025-12-01T22:30:00Z"
  },
  {
    "name": "Gaming",
    "devices": 2,
    "createdAt": "2025-11-15T14:00:00Z"
  }
]
```

---

### `--apply-profile`

Apply a saved volume profile. Restores all device and app volumes to the values stored in the profile.

```
BetterTrumpet.exe --apply-profile "Night Mode"
```

```json
{
  "ok": true,
  "profile": "Night Mode",
  "devicesRestored": 3,
  "appsRestored": 7
}
```

---

### `--watch`

Return a timestamped snapshot of all devices and their apps. Designed for monitoring scripts, logging, and automation pipelines.

```
BetterTrumpet.exe --watch
```

```json
{
  "timestamp": "2026-03-16T14:32:05.123Z",
  "devices": [
    {
      "name": "Headphones (BEACN Mic)",
      "volume": 80,
      "isMuted": false,
      "isDefault": true,
      "appCount": 2,
      "apps": [
        {
          "name": "Spotify",
          "exeName": "spotify.exe",
          "volume": 65,
          "isMuted": false
        },
        {
          "name": "Discord",
          "exeName": "discord.exe",
          "volume": 100,
          "isMuted": false
        }
      ]
    }
  ]
}
```

---

## Matching Rules

### Device Matching

- **Case-insensitive**
- **Partial name** supported: `"BEACN"` matches `"Headphones (BEACN Mic)"`
- Matches against both the device **ID** and **display name**
- If multiple devices match, returns an error

### App Matching

- **Case-insensitive**
- Matches against both **exeName** (e.g., `spotify.exe`) and **displayName** (e.g., `Spotify`)
- You can omit the `.exe` extension: `--app spotify` works

---

## Scripting Examples

### PowerShell

**Mute Discord when launching a game:**

```powershell
# Mute Discord, launch the game, unmute when done
BetterTrumpet.exe --mute --app discord
Start-Process "C:\Games\MyGame.exe" -Wait
BetterTrumpet.exe --unmute --app discord
```

**Save and restore volumes (poor man's profiles):**

```powershell
# Save current state to a file
BetterTrumpet.exe --watch | Out-File ".\volume-backup.json"

# ... do stuff ...

# Restore by reading the backup and setting volumes
$state = Get-Content ".\volume-backup.json" | ConvertFrom-Json
foreach ($device in $state.devices) {
    BetterTrumpet.exe --set-volume $device.volume --device $device.name
    foreach ($app in $device.apps) {
        BetterTrumpet.exe --set-volume $app.volume --app $app.exeName
    }
}
```

**Monitor volume changes in a loop:**

```powershell
while ($true) {
    $snapshot = BetterTrumpet.exe --watch | ConvertFrom-Json
    $ts = $snapshot.timestamp
    foreach ($dev in $snapshot.devices) {
        Write-Host "[$ts] $($dev.name): $($dev.volume)% $(if ($dev.isMuted) {'[MUTED]'})"
        foreach ($app in $dev.apps) {
            Write-Host "  - $($app.name): $($app.volume)%"
        }
    }
    Start-Sleep -Seconds 5
}
```

**Get default device volume as a number:**

```powershell
$vol = (BetterTrumpet.exe --get-volume | ConvertFrom-Json).volume
if ($vol -gt 80) {
    Write-Host "Volume is loud: $vol%"
}
```

**Night mode automation (scheduled task):**

```powershell
# night-mode.ps1 - Run via Task Scheduler at 10 PM
BetterTrumpet.exe --apply-profile "Night Mode"
```

**Switch audio device based on time of day:**

```powershell
$hour = (Get-Date).Hour
if ($hour -ge 22 -or $hour -lt 8) {
    BetterTrumpet.exe --set-default "Headphones"
} else {
    BetterTrumpet.exe --set-default "Speakers"
}
```

**Route game audio to headphones, keep everything else on speakers:**

```powershell
BetterTrumpet.exe --set-device "game.exe" "Headphones (BEACN Mic)"
BetterTrumpet.exe --set-default "Speakers (Realtek)"
```

### cmd.exe

**Quick mute toggle from a batch file or shortcut:**

```batch
@echo off
BetterTrumpet.exe --toggle-mute
```

**Check if running before sending commands:**

```batch
@echo off
BetterTrumpet.exe --ping >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo BetterTrumpet is not running. Starting...
    start "" "C:\Program Files\BetterTrumpet\BetterTrumpet.exe"
    timeout /t 3 >nul
)
BetterTrumpet.exe --set-volume 50
```

**Set multiple app volumes in one script:**

```batch
@echo off
BetterTrumpet.exe --set-volume 30 --app discord
BetterTrumpet.exe --set-volume 80 --app spotify
BetterTrumpet.exe --set-volume 50 --app chrome
echo Volumes configured.
```

---

## Error Handling

All errors are returned as JSON with an `"error"` key:

```json
{
  "error": "description of the problem"
}
```

### Common Errors

| Error Message | Cause |
|---|---|
| `"BetterTrumpet is not running"` | No tray instance found; the named pipe does not exist |
| `"Device not found: XXXX"` | No device matched the given name/ID |
| `"Multiple devices match: XXXX"` | Ambiguous partial name; be more specific |
| `"App not found: XXXX"` | No audio session matched the given app name |
| `"Volume must be between 0 and 100"` | Absolute volume value out of range |
| `"Profile not found: XXXX"` | No saved profile with that name |
| `"Pipe timeout"` | The running instance did not respond in time |
| `"Unknown command: XXXX"` | Unrecognized CLI argument |

### Exit Codes

| Code | Meaning |
|:---:|---|
| `0` | Success |
| `1` | Command error (bad arguments, target not found) |
| `2` | Connection error (BetterTrumpet not running, pipe timeout) |

---

## Troubleshooting

### BetterTrumpet is not running

```
{"error":"BetterTrumpet is not running"}
```

The CLI requires a running BetterTrumpet tray instance. Start BetterTrumpet from the Start menu, taskbar, or startup folder before using CLI commands.

```powershell
# Start BetterTrumpet and wait for it to initialize
Start-Process "BetterTrumpet.exe"
Start-Sleep -Seconds 2
BetterTrumpet.exe --ping
```

### Pipe timeout

```
{"error":"Pipe timeout"}
```

The running instance didn't respond within the timeout window. This can happen if:

- The UI thread is blocked (e.g., a modal dialog is open)
- The system is under heavy load
- Audio subsystem is temporarily unavailable

**Fix:** Close any open BetterTrumpet dialogs and retry. If the issue persists, restart BetterTrumpet.

### Device / app not found

```
{"error":"Device not found: XXXX"}
{"error":"App not found: XXXX"}
```

- Run `--list-devices` or `--list-apps` to see exact names.
- Device/app matching is case-insensitive but the target must be currently active.
- Apps only appear if they have an active audio session (i.e., they have produced audio recently).

### Multiple devices match

```
{"error":"Multiple devices match: XXXX"}
```

Your partial name is too broad. Use a more specific string:

```powershell
# Too vague - might match multiple devices
BetterTrumpet.exe --set-volume 50 --device "Headphones"

# More specific
BetterTrumpet.exe --set-volume 50 --device "Headphones (BEACN Mic)"
```

### Command not recognized

```
{"error":"Unknown command: XXXX"}
```

Check your syntax. All commands use `--` prefix by default. Run `--help` for the full command list.

### Permission issues

If BetterTrumpet is running as a different user (e.g., elevated vs. non-elevated), the named pipe connection may fail. Ensure both the CLI and the tray instance run under the same user context.

---

## Technical Details

| Property | Value |
|---|---|
| Executable | `BetterTrumpet.exe` |
| Runtime | .NET Framework 4.6.2 |
| Architecture | x86 |
| IPC Mechanism | Windows Named Pipe |
| Pipe Name | `BetterTrumpet_IPC_v1` |
| Response Format | JSON (UTF-8) |
| Audio Threading | STA (COM requirement) |
