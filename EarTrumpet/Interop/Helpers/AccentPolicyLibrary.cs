using EarTrumpet.Extensions;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace EarTrumpet.Interop.Helpers
{
    public static class AccentPolicyLibrary
    {
        public static bool AccentPolicySupportsTintColor => Environment.OSVersion.IsAtLeast(OSVersions.RS4);

        private static void SetAccentPolicy(IntPtr handle, User32.AccentPolicy policy)
        {
            var accentStructSize = Marshal.SizeOf(policy);
            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(policy, accentPtr, false);

            var data = new User32.WindowCompositionAttribData();
            data.Attribute = User32.WindowCompositionAttribute.WCA_ACCENT_POLICY;
            data.SizeOfData = accentStructSize;
            data.Data = accentPtr;

            var ret = User32.SetWindowCompositionAttribute(handle, ref data);
            Debug.Assert(ret == 0 || ret == 1);

            Marshal.FreeHGlobal(accentPtr);
        }

        public static void EnableAcrylic(Visual target, Color color, User32.AccentFlags flags)
        {
            var handle = HandleFromVisual(target);

            // Acrylic and DWM corner rounding are coupled by the platform, not by choice:
            // SetWindowCompositionAttribute fills the whole window rect and ignores WPF corner
            // radii, so an acrylic surface with rounded content shows its tint bleeding past the
            // corners unless DWM clips the window to the same shape. Rounding here rather than at
            // each call site so the two cannot drift apart.
            //
            // Precondition: the HWND rect has to BE the visible surface. A template that reserves
            // layout space outside the visible edge -- the ContextMenu style's HasDropShadow
            // padding, for instance -- makes the window larger than what the user sees, and DWM
            // then rounds the wrong rectangle. Such a caller needs its own clipping, not this.
            //
            // Pre-Win11 the rounding is a no-op and the tint stays square; the menus rely on tint
            // and veil being the same colour, so there the bleed is unnoticeable, not absent.
            WindowExtensions.EnableRoundedCornersIfApplicable(handle);

            SetAccentPolicy(handle,
                new User32.AccentPolicy
                {
                    AccentFlags = flags,
                    AccentState = AccentPolicySupportsTintColor ? User32.AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND : User32.AccentState.ACCENT_ENABLE_BLURBEHIND,
                    GradientColor = color.ToABGR(),
                });
        }

        /// <summary>
        /// Turns the acrylic material off. Deliberately does NOT undo the corner rounding that
        /// <see cref="EnableAcrylic"/> applies, despite the asymmetry looking like an oversight.
        /// </summary>
        /// <remarks>
        /// This is a transient suppression, not a teardown: AcrylicBrush calls it on every
        /// LocationChanged and SizeChanged and restores the material 200ms later, so unrounding here
        /// would square a window's corners for the duration of every drag and resize.
        ///
        /// It is also not this function's rounding to undo. Windows that want to be round say so
        /// themselves in SourceInitialized (FlyoutWindow, SettingsWindow, MediaPopupWindow and
        /// others all call EnableRoundedCornersIfApplicable), and nothing here records whether a
        /// given HWND was rounded on its own behalf or on acrylic's.
        /// </remarks>
        public static void DisableAcrylic(Visual target)
        {
            SetAccentPolicy(HandleFromVisual(target),
                new User32.AccentPolicy
                {
                    AccentState = User32.AccentState.ACCENT_DISABLED,
                });
        }

        private static IntPtr HandleFromVisual(Visual visual)
        {
            Visual targetVisual = visual;

            // Popup owns a separate HWND through its child visual. Applying acrylic to the
            // Popup object itself can resolve to no source, which makes the call a no-op.
            if (visual is Popup popup && popup.Child is Visual popupChild)
            {
                targetVisual = popupChild;
            }

            return PresentationSource.FromVisual(targetVisual) is HwndSource source ? source.Handle : IntPtr.Zero;
        }
    }
}
