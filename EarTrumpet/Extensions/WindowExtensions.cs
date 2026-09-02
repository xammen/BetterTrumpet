using EarTrumpet.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EarTrumpet.Extensions
{
    public static class WindowExtensions
    {
        public static void SetWindowPos(this Window window, double top, double left, double height, double width)
        {
            User32.SetWindowPos(window.GetHandle(), IntPtr.Zero, (int)left, (int)top, (int)width, (int)height, User32.WindowPosFlags.SWP_NOZORDER | User32.WindowPosFlags.SWP_NOACTIVATE);
        }

        public static void RaiseWindow(this Window window)
        {
            window.Topmost = true;
            window.Activate();
            window.Topmost = false;
        }

        public static void Cloak(this Window window, bool hide = true)
        {
            int attributeValue = hide ? 1 : 0;
            DwmApi.DwmSetWindowAttribute(window.GetHandle(), DwmApi.DWMA_CLOAK, ref attributeValue, Marshal.SizeOf(attributeValue));
        }

        public static void EnableRoundedCornersIfApplicable(this Window window)
        {
            EnableRoundedCornersIfApplicable(window.GetHandle());
        }

        /// <summary>
        /// Rounds an HWND at the DWM level. Required in addition to any WPF CornerRadius when
        /// the window also carries a SetWindowCompositionAttribute backdrop (acrylic): that API
        /// fills the entire window rect and ignores WPF corner radii, so without this the square
        /// backdrop bleeds out past the rounded content. Also usable for Popup-hosted HWNDs,
        /// which are not Windows and so cannot go through the extension method above.
        /// No-op before Windows 11, where the backdrop therefore stays square.
        /// </summary>
        internal static void EnableRoundedCornersIfApplicable(IntPtr handle)
        {
            if (handle != IntPtr.Zero && Environment.OSVersion.IsAtLeast(OSVersions.Windows11))
            {
                int attributeValue = (int)DwmApi.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
                DwmApi.DwmSetWindowAttribute(handle, DwmApi.DWMWA_WINDOW_CORNER_PREFERENCE, ref attributeValue, Marshal.SizeOf(attributeValue));
            }
        }

        /// <summary>
        /// Returns true when the DWM system backdrop (native Mica/Acrylic) is available.
        /// Requires Windows 11 22H2 (build 22621) or later.
        /// </summary>
        public static bool IsSystemBackdropSupported =>
            Environment.OSVersion.IsAtLeast(OSVersions.Windows11_22H2);

        /// <summary>
        /// Applies a DWM-composited system backdrop to the window (GPU, same material as
        /// native Win11 UI). The window must NOT use AllowsTransparency=true and its WPF
        /// Background must be transparent for the backdrop to show through. No-op on
        /// unsupported OSes, so callers can keep their existing fallback path.
        /// </summary>
        internal static void TrySetSystemBackdrop(this Window window, DwmApi.DWM_SYSTEMBACKDROP_TYPE type)
        {
            if (!IsSystemBackdropSupported)
            {
                return;
            }

            int value = (int)type;
            DwmApi.DwmSetWindowAttribute(window.GetHandle(), DwmApi.DWMWA_SYSTEMBACKDROP_TYPE, ref value, Marshal.SizeOf(value));
        }

        /// <summary>
        /// Aligns the DWM-drawn titlebar/border with the app theme (affects the native
        /// 1px border color around backdrop windows). Safe no-op pre-Win11.
        /// </summary>
        public static void SetImmersiveDarkMode(this Window window, bool dark)
        {
            if (!Environment.OSVersion.IsAtLeast(OSVersions.Windows11))
            {
                return;
            }

            int value = dark ? 1 : 0;
            DwmApi.DwmSetWindowAttribute(window.GetHandle(), DwmApi.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, Marshal.SizeOf(value));
        }

        public static void RemoveWindowStyle(this Window window, int styleToRemove)
        {
            var currentStyle = GetWindowStyle(window.GetHandle(), User32.GWL.GWL_STYLE);
            if (currentStyle == 0)
            {
                Trace.WriteLine($"WindowExtensions RemoveWindowStyle Failed: ({Marshal.GetLastWin32Error()})");
                return;
            }

            SetWindowStyle(window.GetHandle(), User32.GWL.GWL_STYLE, currentStyle & ~styleToRemove);
        }

        public static void ApplyExtendedWindowStyle(this Window window, int newExStyle)
        {
            var currentExStyle = GetWindowStyle(window.GetHandle(), User32.GWL.GWL_EXSTYLE);
            if (currentExStyle == 0)
            {
                Trace.WriteLine($"WindowExtensions ApplyExtendedWindowStyle Failed: ({Marshal.GetLastWin32Error()})");
                return;
            }

            var oldExStyle = SetWindowStyle(window.GetHandle(), User32.GWL.GWL_EXSTYLE, currentExStyle | newExStyle);
            if (oldExStyle != currentExStyle)
            {
                Trace.WriteLine($"WindowExtensions ApplyExtendedWindowStyle Unexpected: ({oldExStyle} vs. {currentExStyle})");
                return;
            }
        }

#if X86
        private static int GetWindowStyle(IntPtr hWnd, User32.GWL index) => User32.GetWindowLong(hWnd, index);
        private static int SetWindowStyle(IntPtr hWnd, User32.GWL index, int style) => User32.SetWindowLong(hWnd, index, style);
#elif X64 || ARM64
        // GWL_STYLE/GWL_EXSTYLE are DWORD-sized, but [Get|Set]WindowLongPtr zero-extend them into
        // a LONG_PTR. WS_POPUP alone sets bit 31, which would make IntPtr.ToInt32() throw
        // OverflowException, so truncate explicitly on the way out and zero-extend on the way in.
        private static int GetWindowStyle(IntPtr hWnd, User32.GWL index) =>
            unchecked((int)User32.GetWindowLongPtr(hWnd, index).ToInt64());

        private static int SetWindowStyle(IntPtr hWnd, User32.GWL index, int style) =>
            unchecked((int)User32.SetWindowLongPtr(hWnd, index, new IntPtr(unchecked((long)(uint)style))).ToInt64());
#endif

        public static IntPtr GetHandle(this Window window)
        {
            return new WindowInteropHelper(window).Handle;
        }
    }
}
