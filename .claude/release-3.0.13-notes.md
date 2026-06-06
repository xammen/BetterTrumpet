# What's New in 3.0.13 🎉

Hey there! This release brings some nice bug fixes, new CLI tricks, and performance improvements. Let's dive in!

---

## 🐛 Bug Fixes

### That annoying backdrop thing is finally fixed! (#13)
You know how sometimes the flyout looked weird on first launch until you opened settings? Yeah, that was driving us crazy too. It's fixed now!

- Backdrop/acrylic renders correctly right from the start
- No more "open settings to fix the UI" workaround needed
- Better theme initialization timing

**Big thanks to @Meteony for reporting this one!**

### Quick device switching just got easier
Right-click the device title and boom—you can set it as default right there. No need to hunt for the three-dot menu anymore.

---

## 🚀 CLI Gets Even Better

### New commands for power users

**`doctor`** - Your audio troubleshooter
```bash
bt doctor
```
Checks your system, reports device status, and helps you figure out what's going on when things get weird.

**`batch`** - Chain commands like a boss
```bash
bt batch --set-volume 67 --app discord --set-volume 30 --app vivaldi
```
Run multiple commands in one shot. Perfect for scripts and automation!

**Shorthand aliases** - Because typing is overrated
```bash
bt volume discord 67    # instead of --set-volume
bt mute spotify         # instead of --mute
bt unmute chrome        # you get the idea
```

Way faster to type, same great results.

---

## ⚡ Startup is Now *Chef's Kiss*

We completely rewrote how BetterTrumpet starts up, and the difference is noticeable:

- **Tray icon appears instantly** (like, *really* fast)
- Audio devices load in the background while you get to work
- Everything else loads in parallel without blocking

**The result?** You'll see the tray icon way faster, and the app feels snappier overall.

---

## 🎨 UI Improvements

- Better device management with hidden devices tracking
- Improved context menus
- More consistent theme rendering across the board

---

## 🔧 Under the Hood

- Tons of code cleanup and better organization
- Improved error handling (fewer mystery crashes)
- Better logging for when things do go wrong
- Smarter resource management

---

## 📝 What Changed

- **20 files touched**, **1,174 additions**
- **2 major bug fixes** (backdrop + context menu)
- **4 new CLI commands** (doctor, batch, volume shortcuts)
- **Performance improvements** across the board

---

## 🙏 Thanks!

Shoutout to @Meteony for reporting issue #13, and to everyone using BetterTrumpet and sending feedback. You rock! 🤘

---

**Full changelog:** https://github.com/xammen/BetterTrumpet/compare/v3.0.12...v3.0.13
