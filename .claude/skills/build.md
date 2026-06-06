---
name: build
description: Build BetterTrumpet and optionally launch it
tags: [build, dev, quick]
model: sonnet
---

# Build BetterTrumpet

Quick build and launch workflow for development.

## Usage

```
/build [options]
```

## Options

- No options: Build Debug x86 and launch
- `--release`: Build in Release mode
- `--clean`: Clean before building
- `--no-launch`: Build only, don't launch

## What this skill does

1. **Build the project:**
   - Uses MSBuild with Debug/x86 configuration
   - Ignores non-critical project errors (ColorTool, Package)
   - Shows minimal output for speed

2. **Verify build:**
   - Checks that `Build/Debug/BetterTrumpet.exe` was created
   - Shows file size and build time

3. **Launch (unless --no-launch):**
   - Kills any existing BetterTrumpet.exe instances
   - Launches the new build in background
   - Verifies it started (PID and memory)

## Examples

```bash
# Quick build + launch for testing
/build

# Release build without launching
/build --release --no-launch

# Clean rebuild
/build --clean
```

## Implementation

```bash
# 1. Determine build configuration
if [[ "$1" == "--release" ]]; then
    CONFIG="Release"
else
    CONFIG="Debug"
fi

# 2. Clean if requested
if [[ "$1" == "--clean" || "$2" == "--clean" ]]; then
    echo "🧹 Cleaning..."
    msbuild //t:Clean //p:Configuration=$CONFIG //p:Platform=x86
fi

# 3. Build
echo "🔨 Building BetterTrumpet ($CONFIG)..."
START_TIME=$(date +%s)

msbuild EarTrumpet.vs15.sln \
    //p:Configuration=$CONFIG \
    //p:Platform=x86 \
    //m \
    //v:minimal \
    //t:Rebuild

BUILD_EXIT=$?
END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

# 4. Check build result
if [ $BUILD_EXIT -eq 0 ]; then
    echo "✅ Build succeeded in ${DURATION}s"
else
    # Check if EarTrumpet.exe was built despite errors in other projects
    if [ -f "Build/$CONFIG/BetterTrumpet.exe" ]; then
        echo "⚠️ Build had errors but BetterTrumpet.exe was created"
    else
        echo "❌ Build failed"
        exit 1
    fi
fi

# 5. Show exe info
EXE_PATH="Build/$CONFIG/BetterTrumpet.exe"
EXE_SIZE=$(stat -c%s "$EXE_PATH" 2>/dev/null || stat -f%z "$EXE_PATH")
EXE_SIZE_MB=$((EXE_SIZE / 1024 / 1024))
echo "📦 BetterTrumpet.exe: ${EXE_SIZE_MB} MB"

# 6. Launch (unless --no-launch)
if [[ "$1" != "--no-launch" && "$2" != "--no-launch" ]]; then
    echo "🚀 Launching BetterTrumpet..."
    
    # Kill existing instances
    taskkill //F //IM BetterTrumpet.exe 2>/dev/null
    sleep 1
    
    # Launch new instance
    cd "Build/$CONFIG" && ./BetterTrumpet.exe &
    sleep 2
    
    # Verify it started
    PROCESS_INFO=$(tasklist | grep -i "BetterTrumpet")
    if [ -n "$PROCESS_INFO" ]; then
        echo "✅ BetterTrumpet started:"
        echo "$PROCESS_INFO"
    else
        echo "❌ Failed to start BetterTrumpet"
    fi
fi
```

## Notes

- Build errors from `EarTrumpet.ColorTool` and `EarTrumpet.Package` are expected and can be ignored
- The main project `EarTrumpet` should build successfully
- Use `--no-launch` when you just want to verify the build compiles
