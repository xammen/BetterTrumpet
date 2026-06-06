# BetterTrumpet 3.0.13 Release Notes

## 🐛 Bug Fixes

### Backdrop Rendering Issue (#13)
- **Fixed inconsistent backdrop/acrylic rendering on first startup**
  - Added forced theme refresh after initialization
  - Implemented deferred acrylic application using Dispatcher
  - Backdrop now renders correctly on first launch without needing to open settings
  - Improved theme binding to handle Options.Source propagation timing

### Device Context Menu
- **Added "Set as default device" to device header context menu**
  - Right-click on device title now shows context menu
  - Quick access to set device as default without using 3-dot menu
  - Improves accessibility and user experience

## 🚀 CLI Enhancements

### New Commands
- **`doctor`** - System diagnostics and health check
  - Reports audio device status
  - Validates configuration
  - Identifies potential issues
  
- **`batch`** - Execute multiple CLI commands in sequence
  - Chain multiple operations in one call
  - Example: `bt batch --set-volume 67 --app discord --set-volume 30 --app vivaldi`
  - Improves automation and scripting capabilities

- **`volume` / `mute` / `unmute`** - Shorter command aliases
  - `bt volume discord 67` (shorthand for `--set-volume`)
  - `bt mute discord` (shorthand for `--mute`)
  - `bt unmute discord` (shorthand for `--unmute`)
  - More intuitive CLI experience

### Improved
- Enhanced app name matching for CLI commands
- Better error handling and reporting
- Improved command parsing

## ⚡ Performance Improvements

### Startup Optimization
- **3-phase startup architecture for faster tray icon appearance**
  - Phase 1: Instant tray icon (0-500ms) - icon visible immediately
  - Phase 2: Audio loading (500ms-2s) - background initialization
  - Phase 3: Feature loading (parallel) - non-blocking addons, hotkeys, etc.
  - Tray icon now appears immediately even before audio devices load
  
### Resource Management
- Lazy initialization of timers and non-critical components
- Deferred media popup creation (only when enabled)
- Parallel loading of background features
- Reduced startup memory footprint

## 🎨 UI/UX Improvements

### Device Management
- Hidden devices tracking and management
- Improved device visibility controls
- Better device state persistence
- Context menu improvements

### Theme System
- Fixed theme binding edge cases
- Improved color resolution timing
- Better backdrop consistency across sessions

## 🔧 Technical Improvements

### Code Quality
- Added comprehensive code comments for startup phases
- Improved separation of concerns in initialization
- Better error handling throughout
- Enhanced logging for diagnostics

### Settings
- New hidden devices persistence system
- Improved settings serialization
- Added HiddenDeviceEntry tracking with timestamps

## 📝 Documentation

- Updated README with new CLI commands
- Added examples for `batch`, `doctor`, and shorthand commands
- Improved CLI command table organization
- Updated command count (19+ commands)

## 🛠️ Developer Experience

### New Claude Code Skills
- `/build` - Quick build and launch
- `/restart` - Fast app restart
- `/fix-issue` - Structured issue workflow
- `/xaml-debug` - XAML layout debugging
- `/commit-and-push` - Intelligent commits
- `/test-feature` - Feature testing automation

## 📊 Changes Summary

- **20 files changed**
- **1,174 additions**, 173 deletions
- **2 bug fixes** (backdrop rendering, context menu)
- **4 new CLI commands** (doctor, batch, volume, mute/unmute)
- **Major startup performance improvements**

## 🙏 Credits

- Fixed by Claude Opus 4.8 with xammen
- Issue #13 reported by @Meteony
- Thanks to the EarTrumpet community

---

**Full Changelog**: https://github.com/xammen/BetterTrumpet/compare/v3.0.12...v3.0.13
