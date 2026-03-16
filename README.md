<p align="center">
  <code>♬⋆.˚ ✩°｡⋆⸜ 🎺</code>
</p>

---

<h1 align="center">✩°｡⋆⸜ BetterTrumpet ⸝⋆｡°✩</h1>

<p align="center">
  <a href="https://bettertrumpet.hiii.boo">
    <img src="https://img.shields.io/badge/official%20site-bettertrumpet.hiii.boo-trumpet?labelColor=0a0a0a&color=4a9&style=for-the-badge" alt="Official Site"/>
  </a>
  <a href="https://github.com/xammen/BetterTrumpet/releases">
    <img src="https://img.shields.io/badge/download-releases-trumpet?labelColor=0a0a0a&color=888&style=for-the-badge" alt="Releases"/>
  </a>
  <a href="https://github.com/xammen/BetterTrumpet">
    <img src="https://img.shields.io/badge/github-xammen/BetterTrumpet-trumpet?labelColor=0a0a0a&color=666&style=for-the-badge&logo=github&logoColor=888" alt="GitHub"/>
  </a>
  <a href="https://www.producthunt.com/posts/bettertrumpet">
    <img src="https://img.shields.io/badge/product%20hunt-bettertrumpet-da552f?labelColor=0a0a0a&style=for-the-badge&logo=producthunt&logoColor=da552f" alt="Product Hunt"/>
  </a>
</p>

<p align="center">
  <i>windows volume control that doesn't suck</i>
  <br/>
  <i>fork of eartrumpet with extra everything ˚ʚ♡ɞ˚</i>
</p>

---

## ✦ what is this?

windows volume mixer is ugly and limited. eartrumpet fixed that. we made it better.

custom themes. smooth animations. media popup. volume profiles. undo/redo. CLI. 19 hotkeys. and a changelog window with a shimmer effect because we care about the little things.

```
                    ┌─────────────────┐
   system tray  ──► │  ♬⋆.˚ 🎺 ˚.⋆♬   │  ──►  per-app volume
                    │  BetterTrumpet  │  ──►  custom colors
                    └─────────────────┘  ──►  media controls
                                        ──►  volume profiles
                                        ──►  CLI control
```

---

## ⋆˚✿˖° features

### core (inherited from eartrumpet)

| feature | |
|---------|---|
| per-app volume control | ✓ |
| move apps between devices | ✓ |
| default device management | ✓ |
| multi-channel peak metering | ✓ |
| light/dark mode support | ✓ |
| configurable hotkeys | ✓ |
| multilingual (20+ languages) | ✓ |

### ˚₊‧꒰ა new in bettertrumpet v3 ໒꒱ ‧₊˚

| feature | |
|---------|---|
| 🎓 premium onboarding wizard | ✓ |
| 🎨 full theme engine (7 color channels) | ✓ |
| ↩️ undo / redo volume changes | ✓ |
| 📌 pin flyout (stays open) | ✓ |
| 🔄 auto update notifications | ✓ |
| 🎚️ volume profiles (save/restore/export) | ✓ |
| ⌨️ quick switch device hotkey | ✓ |
| 🎵 media popup on tray hover | ✓ |
| ⚡ CLI with 19 commands | ✓ |
| 🛡️ crash protection (sentry + health monitor) | ✓ |
| 🌿 eco mode (battery saver) | ✓ |
| 🎞️ configurable animations | ✓ |
| 📦 export / import all settings | ✓ |
| 📋 what's new changelog window | ✓ |

---

## ✩₊˚.⋆ premium onboarding ˚˖𓍢ִ🎓

5-page welcome wizard that actually looks good.

```
  ╭─────────────────────────────────╮
  │  ▬▬▬▬▬▬▬▬▬▬░░░░░░░░░░   2/5   │
  │                                 │
  │  🎵  Sortie audio               │
  │                                 │
  │  ┌──────────────────────────┐   │
  │  │ 🔊  Speakers (Realtek)  ●│   │
  │  │     ▌▌▌▌▌▐               │   │
  │  ├──────────────────────────┤   │
  │  │ 🎧  Headphones (USB)    ○│   │
  │  └──────────────────────────┘   │
  │                                 │
  │  ◄ Retour            Suivant ► │
  ╰─────────────────────────────────╯
```

- dark theme matching the app (dona-inspired, `#101014` background, blue accent)
- progress bar at the top
- 5 pages: welcome → audio device → appearance → privacy → ready
- live VU meters on device cards
- theme preview with mini mixer bars
- toggle switches for telemetry & startup (no dark pattern — inline reassuring text)
- staggered checkmarks + confetti on the final page
- slide transitions between pages

---

## ✩₊˚.⋆ media popup ˚˖𓍢ִ໋🎧✧

hover over the tray icon → beautiful floating media player appears

```
  ╭─────────────────────────────────────╮
  │                                     │
  │   ┌─────────────────────────────┐   │
  │   │     ┌───────────────┐       │   │
  │   │     │   ♪ ♫ ♪ ♫     │       │   │
  │   │     │   album art   │       │   │
  │   │     │   (blurred)   │       │   │
  │   │     └───────────────┘       │   │
  │   │                             │   │
  │   │     ♪ song title ♪          │   │
  │   │     ━━━━━━━━━━━○────────    │   │
  │   │     0:42 / 3:21             │   │
  │   │                             │   │
  │   │      ⟲   ⏮  ▶  ⏭   ⟳       │   │
  │   └─────────────────────────────┘   │
  │                                     │
  ╰─────────────────────────────────────╯
```

- album art background with configurable blur (0–30px)
- track progress bar (clickable seek!)
- shuffle & repeat controls
- smooth pop-in/pop-out animations
- expandable cover art view
- color glow that adapts to album art
- configurable hover delay (0.5–5s)
- option to show only when music is playing

---

## ✩₊˚.⋆ theme engine 🎨

7 color channels, saved themes, and a dynamic album art mode.

```
  ┌─────────────────────────────────────────────────────┐
  │                                                     │
  │   ✦ slider thumb         your volume knob color     │
  │   ✦ track fill           filled portion             │
  │   ✦ track background     empty portion              │
  │   ✦ peak meter           audio level indicator      │
  │   ✦ window background    flyout bg color            │
  │   ✦ text                 label color                │
  │   ✦ accent glow          glow around elements       │
  │                                                     │
  │   presets: cyberpunk, ocean, sunset, forest,         │
  │           neon, monochrome, or pick your own ˚ʚ♡ɞ˚  │
  │                                                     │
  │   ✦ dynamic album art mode: theme adapts to         │
  │     whatever album art is currently playing          │
  │                                                     │
  └─────────────────────────────────────────────────────┘
```

save unlimited custom themes. switch between them instantly.

---

## ✩₊˚.⋆ undo / redo ↩️

accidentally changed volume? ctrl+z to undo, ctrl+y to redo.

works for all slider changes across all apps and devices. stores a history stack so you can go back multiple steps.

---

## ✩₊˚.⋆ pin flyout 📌

click the pin icon (or ctrl+P) to keep the volume flyout open while you work.

no more clicking the tray icon every time you need to adjust something. pin it, leave it, adjust freely.

---

## ✩₊˚.⋆ volume profiles 🎚️

save your entire audio setup as a profile. restore it with one click.

```
  ╭─────────────────────────────────╮
  │  Volume Profiles                │
  │                                 │
  │  ┌──────────────────────────┐   │
  │  │ 🎮  Gaming              │   │
  │  │    Discord 80%, Game 100% │  │
  │  ├──────────────────────────┤   │
  │  │ 🎵  Music                │   │
  │  │    Spotify 90%, All 40%  │   │
  │  ├──────────────────────────┤   │
  │  │ 💼  Work                 │   │
  │  │    Teams 100%, All 30%   │   │
  │  └──────────────────────────┘   │
  │                                 │
  │  [ Capture ]  [ Apply ]         │
  ╰─────────────────────────────────╯
```

- capture current state of all devices and apps
- apply profiles instantly
- rename, delete, reorder
- export/import as `.btprofile` files
- accessible from settings UI and CLI

---

## ✩₊˚.⋆ quick switch device ⌨️

new hotkey to instantly cycle through your playback devices.

set it in settings → shortcuts. press the hotkey and it jumps to the next device. wraps around. that's it.

no more right-clicking the tray, scrolling through a list, clicking the right one. one key. done.

---

## ✩₊˚.⋆ update notifications 🔄

automatic update checking with visual indicators everywhere.

- **tray badge**: small dot on the tray icon when update available
- **flyout banner**: banner at the top of the volume flyout
- **context menu**: "Mettre à jour (vX.Y.Z)" entry in right-click menu
- **configurable channels**: all updates, minor+major only, major only, or none
- check manually from settings or CLI

---

## ✩₊˚.⋆ CLI interface ⚡

19 commands to control everything from the terminal.

```bash
# volume control
bettertrumpet set-volume 75
bettertrumpet get-volume
bettertrumpet mute
bettertrumpet unmute
bettertrumpet toggle-mute

# devices
bettertrumpet list-devices
bettertrumpet get-default
bettertrumpet set-default <id>

# apps
bettertrumpet list-apps
bettertrumpet set-app-volume <name> <volume>

# profiles
bettertrumpet list-profiles
bettertrumpet apply-profile <name>
bettertrumpet capture-profile <name>

# settings export/import
bettertrumpet export-settings [path]
bettertrumpet import-settings <path>

# updates
bettertrumpet check-update

# system
bettertrumpet status
bettertrumpet version
```

pipe-based IPC — CLI sends commands to the running instance. works from any terminal.

---

## ✩₊˚.⋆ export / import settings 📦

backup and restore your entire configuration.

- exports everything: themes, hotkeys, media popup settings, volume profiles, eco mode, animations, update preferences
- saves as `.btsettings` (readable JSON)
- import from file to restore on a new machine
- available from settings (About page) and CLI
- skips `RunAtStartup` on import for security

---

## ✩₊˚.⋆ what's new changelog 📋

after a version update, a "Quoi de neuf" window shows what changed.

- dark themed window matching the app (same design language as onboarding)
- categorized layout: hero feature → audio control → experience → under the hood
- 2×2 feature cards with icons
- staggered fade-in animations on load
- subtle shimmer on the title (single pass, not a loop — we're not aliexpress)
- doesn't show on first run (onboarding handles that)
- only shows once per version

---

## ✩₊˚.⋆ crash protection & telemetry 🛡️

- **sentry integration** with GDPR-compliant DSN
- **crash handler** that catches unhandled exceptions
- **health monitor** running in background
- **structured logging** throughout the app
- telemetry is opt-out (default on outside EU, off inside EU — automatic geo detection)
- no personal data collected, ever

---

## ✩₊˚.⋆ eco mode 🌿

reduce CPU/GPU usage when you don't need the eye candy.

| mode | behavior |
|------|----------|
| eco mode | reduces peak meter FPS to 20, limits animations |
| auto eco | automatically activates eco mode on battery power |

manual toggle in settings, or let auto-eco handle it.

---

## ✩₊˚.⋆ animated tray icon 🎵

the tray icon comes alive when music is playing!

```
  ┌────────────────────────────────────┐
  │                                    │
  │     static     →    animated       │
  │                                    │
  │      🔊        →    🎵 ♪ ♫         │
  │                                    │
  │   (no audio)   →  (music playing)  │
  │                                    │
  └────────────────────────────────────┘
```

- dynamic vector-based speaker icon
- sound waves animate with audio peaks
- seamlessly toggles based on media state

---

## ✩₊˚.⋆ installation

```
  ┌────────────────────────────────────────────────────┐
  │                                                    │
  │   ✦ option 1: download release                     │
  │      github.com/xammen/BetterTrumpet/releases      │
  │                                                    │
  │   ✦ option 2: build from source                    │
  │      git clone https://github.com/xammen/BetterTrumpet │
  │      open EarTrumpet.vs15.sln in visual studio     │
  │      build & run                                   │
  │                                                    │
  │   done ♬⋆.˚ ✩°｡⋆⸜ 🎺                               │
  │                                                    │
  └────────────────────────────────────────────────────┘
```

---

## ₊˚⊹♡ usage

1. launch bettertrumpet
2. click the tray icon → volume flyout
3. adjust volume per app
4. right-click for settings & device switching
5. **hover** the tray icon for media controls
6. **pin** the flyout to keep it open (ctrl+P)
7. **ctrl+Z / ctrl+Y** to undo/redo volume changes

```
  ╭─────────────────────╮
  │ 📌  pinned           │
  │ ♪ spotify     ████░░ │
  │ ♪ discord     ██████ │
  │ ♪ chrome      ███░░░ │
  ├─────────────────────┤
  │ ♬ master      █████░ │
  ├─────────────────────┤
  │ 🔄 Update available  │
  ╰─────────────────────╯
```

---

## ⸝⸝˚⋆ settings

right-click tray icon → settings

organized in two categories:

### general
| page | what it does |
|------|-------------|
| shortcuts | 6 global hotkeys (flyout, mixer, settings, vol up, vol down, switch device) |
| mouse | scroll wheel behavior on tray icon |
| community | telemetry, logarithmic volume |
| legacy | legacy icon, legacy compatibility |
| about | version info, diagnostics, export/import settings |

### customization
| page | what it does |
|------|-------------|
| animations | smooth volume animation, speed, peak meter FPS, eco mode |
| colors | 7-channel theme engine with presets and custom themes |
| media popup | hover delay, blur radius, show-only-when-playing |
| volume profiles | create, apply, rename, delete, export/import profiles |

---

## ⋆｡°✩ hotkeys

| hotkey | action |
|--------|--------|
| configurable | open flyout |
| configurable | open full mixer |
| configurable | open settings |
| configurable | volume up (all devices) |
| configurable | volume down (all devices) |
| configurable | switch default playback device |
| ctrl+Z | undo last volume change |
| ctrl+Y | redo volume change |
| ctrl+P | pin/unpin flyout |

all configurable hotkeys are set in settings → shortcuts.

---

## 🔧 debug shortcuts

in debug builds, hold these keys while launching bettertrumpet:

| key | action |
|-----|--------|
| **Ctrl gauche** | force the onboarding wizard (even if already completed) |
| **Shift gauche** | force the "what's new" changelog window |

useful for testing the onboarding flow and changelog without uninstalling.

---

## ♪₊˚.⋆ supported systems

| os | |
|---|---|
| windows 10 (1803+) | ✓ |
| windows 11 | ✓ |

---

## ⋆˙⟡ tech stack

| | |
|---|---|
| language | c# / wpf |
| framework | .net framework 4.6.2 |
| audio api | windows core audio |
| media api | windows media session |
| telemetry | sentry (GDPR-compliant) |
| packaging | msix |
| CLI | named pipe IPC |

---

## ✩°｡⋆ credits

based on [eartrumpet](https://github.com/File-New-Project/EarTrumpet) by:
- david golden
- rafael rivera
- dave amenta

---

## ⋆.˚ license

[mit license](./LICENSE)

---

<p align="center">
  <br/>
  <code>♬⋆.˚ ✩°｡⋆⸜ 🎺 ⸝⋆｡°✩ ˚.⋆♬</code>
  <br/>
  <br/>
  <i>made with volume ˚ʚ♡ɞ˚</i>
  <br/>
  <br/>
</p>
