using System;
using System.Runtime.InteropServices;

namespace EarTrumpet.Interop
{
    class DwmApi
    {
        internal const int DWMA_CLOAK = 13;
        internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        // Native backdrop + chrome attributes (Windows 11). Used to replace the
        // legacy SetWindowCompositionAttribute acrylic (software-rendered) with the
        // DWM-composited system backdrop (GPU, identical material to native Win11 UI).
        internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // dark titlebar/border
        internal const int DWMWA_BORDER_COLOR = 34;            // custom 1px border color
        internal const int DWMWA_SYSTEMBACKDROP_TYPE = 38;     // Mica / Acrylic / Tabbed

        // DWMWA_BORDER_COLOR sentinel: let DWM omit the border entirely.
        internal const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

        internal enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3
        }

        internal enum DWM_SYSTEMBACKDROP_TYPE
        {
            DWMSBT_AUTO = 0,
            DWMSBT_NONE = 1,             // no backdrop (solid)
            DWMSBT_MAINWINDOW = 2,       // Mica
            DWMSBT_TRANSIENTWINDOW = 3,  // Acrylic — the right material for flyouts/popups
            DWMSBT_TABBEDWINDOW = 4      // Mica Alt (tabbed)
        }

        [DllImport("dwmapi.dll", PreserveSig = false)]
        internal static extern void DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);
    }
}
