---
name: bettertrumpet
description: Control BetterTrumpet from an AI agent through the `bt` command-line interface. Use this skill when the user asks to change Windows audio volume, mute or unmute apps/devices, switch the default playback device, route an app to a device, or apply QuickTrumpet presets such as "Discord mode".
---

# BetterTrumpet Agent Skill

BetterTrumpet is a Windows per-app volume mixer with a JSON CLI exposed through the `bt` command. This skill teaches an AI agent how to safely translate natural-language audio requests into BetterTrumpet commands.

Use this skill when the user asks for actions like:

- Lower or raise the global volume.
- Change the volume of a specific app, such as Discord, Spotify, Chrome, or a game.
- Mute, unmute, or toggle mute for a device or app.
- Change the default output device.
- Route an app to a specific output device.
- List audio devices, apps, or current volume state.
- Apply, create, or inspect QuickTrumpet presets.
- Activate a named "mode", such as "Discord mode", "Focus mode", "Gaming", or any user-created preset.

## Core Principle

Do not hardcode special modes. In BetterTrumpet, a "mode" is normally a QuickTrumpet preset created by the user.

For example, if the user says "activate Discord mode", interpret it as:

```powershell
bt Discord
```

or equivalently:

```powershell
bt apply Discord
```

If the preset does not exist, explain that no `Discord` preset was found and offer to list or create presets.

## Environment Requirements

- BetterTrumpet must be running for remote commands.
- Use the `bt` command directly. `bt.cmd` is the supported automation wrapper and is responsible for finding `BetterTrumpet.exe`, waiting for the WPF/GUI process, and printing captured JSON output.
- CLI responses are JSON and should be parsed or inspected before reporting success.
- If `bt` is not found, the user should install BetterTrumpet with the PATH option or run the repo/root `bt.cmd` directly.
- If `bt` returns an error, report that error instead of falling back to ad-hoc executable calls.

## Standard Workflow

Follow this workflow for most requests:

1. Check that BetterTrumpet is reachable:

```powershell
bt --ping
bt doctor
```

2. Discover current state when needed:

```powershell
bt --list-devices
bt --list-apps
bt presets
```

3. Execute the smallest command that satisfies the user request.

4. Read the JSON response.

5. If the response contains `error`, report the problem and suggest the next useful command.

6. If the response contains `ok: true`, summarize what changed.

## Safety Rules

- Do not guess destructive or broad actions when the target is ambiguous.
- If the user asks for a named mode or preset, run `bt presets` first unless the exact preset name is already known from the current context.
- If multiple devices or apps could match, list candidates and ask which one to use.
- Use relative volume changes for vague requests like "a bit lower" or "un peu moins fort".
- Do not set volume to `0` or mute everything unless the user explicitly asks.
- Do not change the default playback device unless the user explicitly asks for a default device change.
- Do not create or delete presets without explicit user intent.
- Treat command failure as non-fatal. Report the JSON error and propose a corrective next step.

## Command Reference

### Health

Check whether BetterTrumpet is running:

```powershell
bt --ping
```

Run a fuller machine-readable diagnostic:

```powershell
bt doctor
```

Expected success shape:

```json
{
  "status": "ok",
  "version": "3.0.10"
}
```

### List Devices

List output devices and nested app sessions:

```powershell
bt --list-devices
```

Use this before changing default devices, targeting a device by partial name, or diagnosing routing.

### List Apps

List active audio apps:

```powershell
bt --list-apps
```

Use this before targeting an app if the app name is uncertain.

### Get Volume

Default playback device:

```powershell
bt --get-volume
```

Specific device:

```powershell
bt --get-volume --device "Headphones"
```

### Set Device Volume

Absolute value from `0` to `100`:

```powershell
bt --set-volume 50
```

Relative change:

```powershell
bt --set-volume -10
bt --set-volume +5
```

Specific device:

```powershell
bt --set-volume 40 --device "Speakers"
```

### Set App Volume

Absolute app volume:

```powershell
bt --set-volume 30 --app discord
```

Relative app volume:

```powershell
bt --set-volume -10 --app spotify
```

Friendly aliases:

```powershell
bt volume discord 67
bt volume vivaldi 30
bt volume 50
```

App names can be exact executable names or display names when supported by the current BetterTrumpet CLI behavior. Prefer checking `bt --list-apps` first.

### Mute And Unmute

Mute default device:

```powershell
bt --mute
```

Unmute default device:

```powershell
bt --unmute
```

Mute a device:

```powershell
bt --mute --device "Speakers"
```

Mute an app:

```powershell
bt --mute --app discord
bt mute discord
```

Unmute an app:

```powershell
bt --unmute --app discord
bt unmute discord
```

Toggle mute on a device:

```powershell
bt --toggle-mute
bt --toggle-mute --device "Headphones"
```

### Default Playback Device

Show current default output device:

```powershell
bt --get-default
```

Change default output device:

```powershell
bt --set-default "Headphones"
```

Device matching supports partial names, but if several devices could match, list devices first and ask the user.

### Route App To Device

Route an app to a specific playback device:

```powershell
bt --set-device discord.exe "Headphones"
bt --set-device spotify.exe "Speakers"
```

Use `bt --list-apps` and `bt --list-devices` before routing if names are uncertain.

### QuickTrumpet Presets

List presets:

```powershell
bt presets
```

Apply a preset by name or slug:

```powershell
bt apply Discord
bt Discord
bt mode Discord
bt focus
```

Run multiple operations and return one JSON result:

```powershell
bt batch --set-volume 67 --app discord --set-volume 30 --app vivaldi
```

Save current default device and its apps as a preset:

```powershell
bt save focus
```

Save current apps only, leaving device volume/mute untouched when applied:

```powershell
bt save Discord --apps-only
```

Save all devices and apps:

```powershell
bt save streaming --all-devices
```

Delete a preset only when explicitly requested:

```powershell
bt delete Discord
```

## Natural Language Mapping

Use these examples as translation rules. When the user asks for several independent changes at once, prefer `bt batch` so the agent receives one JSON response containing every sub-result.

| User intent | Command |
| --- | --- |
| "Lower the volume" | `bt --set-volume -10` |
| "Lower it a bit" | `bt --set-volume -5` |
| "Raise the volume" | `bt --set-volume +10` |
| "Set the volume to 50" | `bt --set-volume 50` |
| "Lower Discord" | `bt --set-volume -10 --app discord` |
| "Set Discord to 30" | `bt volume discord 30` |
| "Mute Discord" | `bt mute discord` |
| "Unmute Discord" | `bt unmute discord` |
| "Mute the sound" | `bt --mute` |
| "Unmute the sound" | `bt --unmute` |
| "What is the default device?" | `bt --get-default` |
| "Set my headphones as default" | `bt --set-default "Headphones"` after confirming/listing the device name |
| "Send Spotify to the speakers" | `bt --set-device spotify.exe "Speakers"` after discovery if needed |
| "List my audio apps" | `bt --list-apps` |
| "List my output devices" | `bt --list-devices` |
| "Activate Discord mode" | `bt Discord` after confirming the preset exists if needed |
| "Activate Discord mode" | `bt mode Discord` if using the explicit mode alias |
| "Set Discord to 67 and Vivaldi to 30" | `bt batch --set-volume 67 --app discord --set-volume 30 --app vivaldi` |
| "Activate my focus preset" | `bt focus` |
| "Save this setup as Gaming" | `bt save Gaming` |
| "Save only the apps as Discord" | `bt save Discord --apps-only` |

## Handling Preset Modes

When the user says "mode X", "preset X", "profil X", "configuration X", or "scene X":

1. Treat `X` as a QuickTrumpet preset candidate.
2. Run:

```powershell
bt presets
```

3. Match by `name` or `slug`, case-insensitively.
4. If found, run:

```powershell
bt apply "X"
```

or:

```powershell
bt X
```

5. If not found, say that the preset does not exist and offer one of these next steps:

- List available presets.
- Save the current setup as that preset with `bt save "X"`.
- Save app-only state with `bt save "X" --apps-only`.

Do not assume that "Discord mode" means muting or routing Discord. It means the user-created preset named `Discord` unless the user explicitly asks for another behavior.

## Ambiguity Handling

Ask a short clarification when:

- The target device name is vague and multiple devices may match.
- The target app is not visible in `bt --list-apps`.
- The user asks for "mode" without naming it.
- The user asks to "fix audio" without a concrete desired state.
- Applying a preset returns warnings such as missing apps or devices.

Useful clarification examples:

- "I see multiple output devices. Which one should I set as default?"
- "I do not see Discord in the active audio sessions. Do you still want me to apply the Discord preset?"
- "The Discord preset does not exist yet. Do you want me to save the current state as the Discord preset?"

## Reporting Results

After executing a command, summarize the actual JSON result rather than assuming success.

Good summaries:

- "Main volume set to 50%."
- "Discord was lowered by 10 points, new volume: 30%."
- "Discord preset applied: 2 apps applied, 1 app missing."
- "BetterTrumpet is not responding. Start the app and try the command again."

If a command returns an error, include the essential error and next step:

```json
{
  "error": "QuickTrumpet preset not found: Discord"
}
```

Report:

"The Discord preset does not exist yet. I can list your presets or save the current state as `Discord`."

## Recommended Agent Behavior

- Prefer one command per user-visible action.
- Prefer discovery before mutation when names are uncertain.
- Prefer relative volume changes for subjective phrases.
- Prefer exact preset application for named modes.
- Keep user-facing responses brief.
- Never invent preset contents. Preset contents belong to BetterTrumpet and the user.

## Troubleshooting

### BetterTrumpet is not running

Symptom:

```text
Error: BetterTrumpet is not running or not responding.
```

Action: Tell the user to start BetterTrumpet, then retry `bt --ping`.

### App not found

Symptom:

```json
{
  "error": "app not found: discord"
}
```

Action: Run `bt --list-apps`. The app may not currently have an audio session.

### Device not found

Symptom:

```json
{
  "error": "device not found"
}
```

Action: Run `bt --list-devices` and choose the closest matching device name.

### Preset not found

Symptom:

```json
{
  "error": "QuickTrumpet preset not found: Discord"
}
```

Action: Run `bt presets`. If the user wants to create it from the current state, run `bt save Discord` or `bt save Discord --apps-only`.

## Minimal Examples

Activate Discord mode:

```powershell
bt presets
bt Discord
```

Lower Discord only:

```powershell
bt --list-apps
bt --set-volume -10 --app discord
```

Switch output to headphones:

```powershell
bt --list-devices
bt --set-default "Headphones"
```

Save the current audio setup as a reusable mode:

```powershell
bt save Work
```

Save an app-only Discord preset:

```powershell
bt save Discord --apps-only
```

## Compatibility Notes

This skill is intentionally plain Markdown with front matter so it can be reused by agent systems that support skill files, prompt snippets, command recipes, or project instructions.

For OpenCode, Hermes, Claude, or similar agents, install or reference this file as the instruction source for BetterTrumpet audio control. The agent only needs shell command execution and access to the `bt` command.
