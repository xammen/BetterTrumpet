using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Timings and curves of the tray flyout motion, modelled on the Windows 11 flyouts:
    /// the surface decelerates out of the taskbar edge on open and accelerates back on close.
    /// </summary>
    internal static class FlyoutMotion
    {
        // Open: the surface (the HWND, so the acrylic travels with it) slides out of the
        // taskbar edge; the content trails it by a few DIPs and fades in quickly.
        public const int OpenSurfaceMs = 280;
        public const int OpenContentSlideMs = 220;
        public const int OpenContentFadeMs = 120;
        public const double OpenContentSlideDip = 8;

        // Close: accelerate back toward the taskbar; the content is gone slightly before the
        // surface is cloaked, so the last frames never show half-faded content.
        public const int CloseSurfaceMs = 150;
        public const int CloseContentFadeMs = 120;

        // Expand/collapse: a content swap inside the open surface (no cloak, no acrylic toggle).
        public const int SwapFadeOutMs = 90;
        public const int SwapSurfaceMs = 240;
        public const int SwapBlockMs = 200;
        public const int SwapBlockStaggerMs = 24;
        public const int SwapMaxBlocks = 5;
        public const double SwapBlockSlideDip = 6;

        // App row insertion while the flyout is open: the row container grows from 0 to its
        // natural height and the window edge travels with it (see FlyoutWindow.RowEntrance.cs);
        // the row content fades in slightly behind the growth and rises a few DIPs.
        public const int RowGrowMs = 240;
        public const int RowContentDelayMs = 60;
        public const int RowContentFadeMs = 160;
        public const double RowContentRiseDip = 6;

        // Travel of the surface along the taskbar axis. Full travel only when a taskbar on the
        // same monitor hides the start position; a shorter one when the surface is exposed.
        public const double MaxTravelDip = 36;
        public const double ExposedTravelDip = 16;

        /// <summary>Windows 11 "decelerate": fast start, long settle. Used for every entrance.</summary>
        public static readonly KeySpline Decelerate = CreateSpline(0.1, 0.9, 0.2, 1);

        /// <summary>Plain ease-out used for fades: most of the change happens early, so a fade
        /// is visually done one frame before it ends.</summary>
        public static readonly KeySpline EaseOut = CreateSpline(0, 0, 0.2, 1);

        /// <summary>Windows 11 "accelerate": used for exits.</summary>
        public static readonly KeySpline Accelerate = CreateSpline(0.7, 0, 1, 0.5);

        /// <summary>
        /// Builds a single-segment key-frame animation. With <paramref name="from"/> null it starts
        /// from the property's current (possibly animated) value, so a new animation begun with
        /// <see cref="HandoffBehavior.SnapshotAndReplace"/> retargets without a jump. With a
        /// value, it holds <paramref name="from"/> during <paramref name="delayMs"/>, which lets a
        /// staggered element stay hidden without touching its base value.
        /// </summary>
        public static DoubleAnimationUsingKeyFrames Create(double? from, double to, int durationMs, KeySpline spline, int delayMs = 0, FillBehavior fill = FillBehavior.Stop)
        {
            var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = fill };
            if (from.HasValue)
            {
                animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from.Value, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                if (delayMs > 0)
                {
                    animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from.Value, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delayMs))));
                }
            }
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delayMs + durationMs)), spline));
            return animation;
        }

        private static KeySpline CreateSpline(double x1, double y1, double x2, double y2)
        {
            var spline = new KeySpline(x1, y1, x2, y2);
            spline.Freeze();
            return spline;
        }
    }

    /// <summary>
    /// Moves the flyout HWND itself (position only) so the DWM acrylic backdrop travels with the
    /// surface, instead of transforming content inside a fixed acrylic pane. <see cref="Offset"/>
    /// is in DIPs along the taskbar axis (0 = rest, positive = toward / behind the taskbar). All
    /// positioning happens in physical pixels; WPF's Top/Left are never animated.
    /// </summary>
    internal sealed class FlyoutSurfaceMotion : Animatable
    {
        public static readonly DependencyProperty OffsetProperty = DependencyProperty.Register(
            nameof(Offset), typeof(double), typeof(FlyoutSurfaceMotion),
            new PropertyMetadata(0.0, (d, e) => ((FlyoutSurfaceMotion)d).Apply()));

        private readonly Window _window;
        private IntPtr _handle;
        private Int32Rect _rest;
        private bool _hasRest;
        private double _scaleX = 1;
        private double _scaleY = 1;
        private int _axisX;
        private int _axisY = 1;
        private int _appliedLeft = int.MinValue;
        private int _appliedTop = int.MinValue;
        private int _generation;
        private bool _suspendApply;
        private int _growthY;

        public FlyoutSurfaceMotion(Window window)
        {
            _window = window;
        }

        /// <summary>Offset from the rest position, in DIPs, along the taskbar axis.</summary>
        public double Offset
        {
            get => (double)GetValue(OffsetProperty);
            set => SetValue(OffsetProperty, value);
        }

        /// <summary>Rest bounds in physical pixels, or null before the first positioning.</summary>
        public Int32Rect? Rest => _hasRest ? _rest : (Int32Rect?)null;

        public double ScaleX => _scaleX;
        public double ScaleY => _scaleY;

        /// <summary>
        /// Extra vertical offset in physical pixels (positive = down), independent of the taskbar
        /// axis. Used while app rows grow in: the window already has its final size and is held
        /// toward its anchored edge, then released as the rows grow (FlyoutWindow.RowEntrance.cs).
        /// </summary>
        public int GrowthOffsetY => _growthY;

        /// <summary>Sets <see cref="GrowthOffsetY"/>; with <paramref name="apply"/> false the next
        /// SetRest or move picks it up.</summary>
        public void SetGrowthOffset(int pixels, bool apply)
        {
            _growthY = pixels;
            if (apply)
            {
                Apply();
            }
        }

        /// <summary>Direction of a positive offset: -1, 0 or 1 on each axis.</summary>
        public int AxisX => _axisX;
        public int AxisY => _axisY;
        public bool IsVerticalAxis => _axisY != 0;

        /// <summary>
        /// Positive offsets point toward the taskbar edge: bottom +Y, top -Y, left -X, right +X.
        /// </summary>
        public void SetAxis(WindowsTaskbar.Position taskbarEdge)
        {
            switch (taskbarEdge)
            {
                case WindowsTaskbar.Position.Left: _axisX = -1; _axisY = 0; break;
                case WindowsTaskbar.Position.Right: _axisX = 1; _axisY = 0; break;
                case WindowsTaskbar.Position.Top: _axisX = 0; _axisY = -1; break;
                default: _axisX = 0; _axisY = 1; break;
            }
        }

        /// <summary>
        /// Updates the rest bounds and places the window at rest + current offset. Called by every
        /// reposition (open, size invalidation, DPI change), so a reposition in the middle of an
        /// animation updates the target instead of snapping. Compares against the real window
        /// rect, so nothing is sent when the window is already where it should be.
        /// </summary>
        public void SetRest(int left, int top, int width, int height, double scaleX, double scaleY)
        {
            _rest = new Int32Rect(left, top, width, height);
            _hasRest = true;
            _scaleX = scaleX > 0 ? scaleX : 1;
            _scaleY = scaleY > 0 ? scaleY : 1;

            if (!EnsureHandle())
            {
                return;
            }

            GetTargetPosition(out var x, out var y);

            bool sizeMatches = false;
            if (User32.GetWindowRect(_handle, out var actual))
            {
                sizeMatches = actual.Right - actual.Left == width && actual.Bottom - actual.Top == height;
                if (sizeMatches && actual.Left == x && actual.Top == y)
                {
                    _appliedLeft = x;
                    _appliedTop = y;
                    return;
                }
            }

            var flags = User32.WindowPosFlags.SWP_NOZORDER | User32.WindowPosFlags.SWP_NOACTIVATE | User32.WindowPosFlags.SWP_NOOWNERZORDER;
            if (sizeMatches)
            {
                flags |= User32.WindowPosFlags.SWP_NOSIZE;
            }

            // Record the target before the call: a resize makes WPF handle WM_SIZE and render
            // synchronously inside SetWindowPos, which ticks the animation and re-enters Apply.
            // The re-entrant move wins; the trailing Apply catches an offset that moved on.
            _appliedLeft = x;
            _appliedTop = y;
            User32.SetWindowPos(_handle, IntPtr.Zero, x, y, width, height, flags);
            Apply();
        }

        /// <summary>Stops any running animation and jumps to <paramref name="offset"/>.</summary>
        public void Snap(double offset)
        {
            _generation++;

            // Removing the clock and setting the base value each raise a change; suspend so the
            // window only ever moves once, straight to the final position.
            _suspendApply = true;
            try
            {
                BeginAnimation(OffsetProperty, null);
                Offset = offset;
            }
            finally
            {
                _suspendApply = false;
            }
            Apply();
        }

        /// <summary>
        /// Animates from the current rendered offset (no From), so a retarget in the middle of an
        /// open or close continues from where the surface is. <paramref name="completed"/> only
        /// runs if this animation was not superseded.
        /// </summary>
        public void AnimateTo(double offset, int durationMs, KeySpline spline, Action completed)
        {
            int generation = ++_generation;
            var animation = FlyoutMotion.Create(null, offset, durationMs, spline, 0, FillBehavior.HoldEnd);
            animation.Completed += (s, e) =>
            {
                if (generation != _generation)
                {
                    return;
                }

                Snap(offset);
                completed?.Invoke();
            };
            BeginAnimation(OffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private void Apply()
        {
            if (_suspendApply || !_hasRest || !EnsureHandle())
            {
                return;
            }

            GetTargetPosition(out var x, out var y);
            if (x == _appliedLeft && y == _appliedTop)
            {
                // Sub-pixel tail of the curve: nothing to send to the window manager.
                return;
            }

            _appliedLeft = x;
            _appliedTop = y;
            WindowExtensions.MoveWindow(_handle, x, y);
        }

        private void GetTargetPosition(out int x, out int y)
        {
            var offset = Offset;
            x = _rest.X + _axisX * (int)Math.Round(offset * _scaleX);
            y = _rest.Y + _axisY * (int)Math.Round(offset * _scaleY) + _growthY;
        }

        private bool EnsureHandle()
        {
            if (_handle == IntPtr.Zero)
            {
                _handle = new WindowInteropHelper(_window).Handle;
            }
            return _handle != IntPtr.Zero;
        }

        protected override Freezable CreateInstanceCore() => new FlyoutSurfaceMotion(_window);
    }
}
