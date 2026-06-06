---
name: restart
description: Restart BetterTrumpet quickly
tags: [dev, quick, test]
model: haiku
---

# Restart BetterTrumpet

Quickly restart BetterTrumpet during development/testing.

## Usage

```
/restart
```

## What this skill does

1. **Kill existing instances:**
   - Finds all BetterTrumpet.exe processes
   - Kills them with taskkill //F
   - Waits 1 second for cleanup

2. **Launch new instance:**
   - Starts Build/Debug/BetterTrumpet.exe in background
   - Waits 2 seconds for startup

3. **Verify:**
   - Checks that the process is running
   - Shows PID and memory usage

## Examples

```bash
# Simple restart
/restart
```

## Implementation

```bash
echo "🔄 Restarting BetterTrumpet..."

# 1. Kill existing instances
EXISTING=$(tasklist | grep -i "BetterTrumpet" | wc -l)
if [ $EXISTING -gt 0 ]; then
    echo "🛑 Stopping $EXISTING instance(s)..."
    taskkill //F //IM BetterTrumpet.exe 2>/dev/null
    sleep 1
else
    echo "ℹ️ No running instances found"
fi

# 2. Launch new instance
echo "🚀 Starting BetterTrumpet..."
cd Build/Debug && ./BetterTrumpet.exe &
sleep 2

# 3. Verify and show info
PROCESS_INFO=$(tasklist | grep -i "BetterTrumpet")
if [ -n "$PROCESS_INFO" ]; then
    echo "✅ BetterTrumpet restarted successfully:"
    echo "$PROCESS_INFO"
else
    echo "❌ Failed to start BetterTrumpet"
    exit 1
fi
```

## Notes

- This assumes the build already exists in `Build/Debug/`
- Use `/build` if you need to rebuild first
- The process runs in background, so Claude remains responsive
