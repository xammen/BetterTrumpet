# DPI Scaling Fix - GitHub Issue #11

## Problem
BetterTrumpet appeared to be scaled more aggressively than other normal Windows control elements compared to the original EarTrumpet reference. This was most noticeable at different DPI settings (100%, 125%, 150%, etc.).

## Root Cause
The application manifest was using `PerMonitor` DPI awareness mode, which is an older implementation introduced in Windows 10 Anniversary Update (1607). This mode can cause inconsistent scaling behavior and aggressive scaling compared to standard Windows controls.

## Solution
Upgraded the DPI awareness mode to `PerMonitorV2, PerMonitor` (comma-separated fallback list):

1. **PerMonitorV2** - Primary mode for Windows 10 Creators Update (1703) and later
   - Provides better DPI scaling support
   - Fixes aggressive scaling issues
   - Ensures consistent scaling with Windows controls
   - Handles DPI changes more gracefully (e.g., moving windows between monitors)

2. **PerMonitor** - Fallback for Windows 10 Anniversary Update (1607-1703)
   - Maintains compatibility with older Windows 10 versions

3. **System DPI** - Implicit fallback for pre-1607 Windows versions via `dpiAware` tag

## Changes Made
**File**: `EarTrumpet/App.manifest`

Changed:
```xml
<dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitor</dpiAwareness>
```

To:
```xml
<dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2, PerMonitor</dpiAwareness>
```

## Benefits of PerMonitorV2
- **Better child window DPI scaling**: Child windows automatically inherit DPI awareness from parent
- **Improved DPI change handling**: Smoother transitions when moving between monitors with different DPI settings
- **Consistent with Windows UI**: Matches the behavior of modern Windows applications
- **Non-client area scaling**: Windows automatically scales non-client areas (title bars, borders)
- **Mixed-mode DPI support**: Better handling of scenarios where different monitors have different DPI settings

## Testing Recommendations
1. Test at different DPI settings:
   - 100% (96 DPI)
   - 125% (120 DPI)
   - 150% (144 DPI)
   - 175% (168 DPI)
   - 200% (192 DPI)

2. Test multi-monitor scenarios:
   - Monitors with different DPI settings
   - Moving the flyout window between monitors
   - Opening on secondary monitor with different DPI

3. Compare with original EarTrumpet:
   - Flyout window size and positioning
   - Text rendering and clarity
   - Icon sizes in the flyout
   - Popup positioning

4. Test on different Windows versions:
   - Windows 10 1703+ (should use PerMonitorV2)
   - Windows 10 1607-1703 (should fall back to PerMonitor)
   - Verify no visual regressions

## References
- Microsoft Docs: [High DPI Desktop Application Development on Windows](https://docs.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)
- Microsoft Docs: [DPI_AWARENESS_CONTEXT](https://docs.microsoft.com/en-us/windows/win32/hidpi/dpi-awareness-context)
- Windows 10 version 1703 (Creators Update) introduced PerMonitorV2

## Notes
- The existing DPI calculation code in `VisualExtensions.cs` remains unchanged and is correct
- The `DpiX()` and `DpiY()` extension methods properly calculate DPI from `PresentationSource.FromVisual()`
- No code changes required - only manifest update needed
- This fix aligns with Windows best practices for DPI-aware applications
