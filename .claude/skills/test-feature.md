---
name: test-feature
description: Test a specific feature quickly
tags: [test, qa, quick]
model: haiku
---

# Test Feature

Quickly test a specific feature after implementation or bug fix.

## Usage

```
/test-feature <feature-name>
```

## What this skill does

1. **Build and launch:**
   - Rebuilds the project
   - Kills existing instances
   - Launches fresh BetterTrumpet

2. **Provide test instructions:**
   - Clear step-by-step instructions
   - Expected behavior
   - What to look for

3. **Wait for feedback:**
   - Asks user to verify
   - Records result (pass/fail)
   - Suggests next steps if failed

## Common Features to Test

### UI Features
- `device-header-context-menu`: Right-click menu on device header
- `expand-collapse-button`: Device expand/collapse functionality
- `pin-button`: Pin window functionality
- `backdrop-rendering`: Backdrop/acrylic rendering on startup
- `theme-switching`: Light/dark theme switching
- `tray-icon`: System tray icon and menu

### CLI Features
- `cli-volume-set`: Set volume via CLI
- `cli-volume-get`: Get volume via CLI
- `cli-device-list`: List devices via CLI
- `cli-device-default`: Set default device via CLI

### Performance
- `startup-time`: Measure startup time
- `memory-usage`: Check memory footprint
- `tray-icon-responsiveness`: Tray icon appears quickly

## Examples

```bash
# Test device header context menu (issue #15)
/test-feature device-header-context-menu

# Test backdrop rendering (issue #13)
/test-feature backdrop-rendering

# Test CLI volume control
/test-feature cli-volume-set
```

## Test Instructions by Feature

### device-header-context-menu
```
1. Open BetterTrumpet flyout (click tray icon)
2. Right-click on the device title (e.g., "Speakers")
3. Verify context menu appears with "Set as default device"
4. Click the menu item
5. Verify the device becomes the default

✅ Pass: Menu appears and device becomes default
❌ Fail: Menu doesn't appear or action doesn't work
```

### backdrop-rendering
```
1. Completely quit BetterTrumpet (right-click tray → Quit)
2. Launch BetterTrumpet fresh
3. Immediately open the flyout (don't open settings first)
4. Check if the backdrop/background renders correctly

✅ Pass: Backdrop looks correct on first launch
❌ Fail: Backdrop is wrong until settings are opened
```

### expand-collapse-button
```
1. Open BetterTrumpet flyout
2. Click the expand/collapse arrow on a device
3. Verify apps list expands/collapses
4. Check that arrow icon changes direction

✅ Pass: Apps expand/collapse correctly, arrow animates
❌ Fail: Apps don't expand or arrow doesn't change
```

### startup-time
```
1. Quit BetterTrumpet completely
2. Note the current time
3. Launch BetterTrumpet
4. Wait for tray icon to appear
5. Calculate the time taken

✅ Pass: Tray icon appears within 2 seconds
⚠️ Warning: 2-5 seconds
❌ Fail: Takes more than 5 seconds
```

### cli-volume-set
```
1. Open PowerShell/CMD
2. Run: BetterTrumpet --volume 50
3. Verify volume changes to 50%
4. Run: BetterTrumpet --volume +10
5. Verify volume increases by 10%

✅ Pass: Volume changes as expected
❌ Fail: Volume doesn't change or errors
```

## Implementation

```bash
FEATURE="$1"

# 1. Build and launch
echo "🔨 Building BetterTrumpet..."
/build --no-launch

if [ $? -ne 0 ]; then
    echo "❌ Build failed, cannot test"
    exit 1
fi

echo "🚀 Launching BetterTrumpet..."
taskkill //F //IM BetterTrumpet.exe 2>/dev/null
sleep 1
cd Build/Debug && ./BetterTrumpet.exe &
sleep 2

# 2. Show test instructions based on feature
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "🧪 Testing: $FEATURE"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

case "$FEATURE" in
    device-header-context-menu)
        echo "📋 Test Instructions:"
        echo "1. Open BetterTrumpet flyout (click tray icon)"
        echo "2. Right-click on the device title"
        echo "3. Verify context menu appears with 'Set as default device'"
        echo "4. Click the menu item"
        echo "5. Verify the device becomes default"
        ;;
    backdrop-rendering)
        echo "📋 Test Instructions:"
        echo "1. Completely quit BetterTrumpet (right-click tray → Quit)"
        echo "2. Launch BetterTrumpet fresh"
        echo "3. Immediately open the flyout"
        echo "4. Check if backdrop renders correctly"
        ;;
    *)
        echo "📋 Test Instructions:"
        echo "Please test the feature: $FEATURE"
        echo "(No specific instructions available for this feature)"
        ;;
esac

# 3. Wait for feedback
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "Did the test pass? (y/n)"

# Record result
read -r RESULT
if [ "$RESULT" = "y" ]; then
    echo "✅ Test passed: $FEATURE"
else
    echo "❌ Test failed: $FEATURE"
    echo ""
    echo "Next steps:"
    echo "- Review the implementation"
    echo "- Check logs for errors"
    echo "- Debug with /xaml-debug or code inspection"
fi
```

## Test Result Tracking

After testing, the skill can optionally:
- Log results to `.claude/test-results.md`
- Comment on related GitHub issues
- Update a testing checklist

## Notes

- Always test on a fresh launch for accurate results
- Test both positive and negative cases when applicable
- Document any unexpected behavior
- Use this before closing GitHub issues
