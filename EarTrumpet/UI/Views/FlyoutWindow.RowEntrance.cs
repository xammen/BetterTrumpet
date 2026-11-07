using EarTrumpet.Extensions;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.Helpers;
using EarTrumpet.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace EarTrumpet.UI.Views
{
    /// <summary>
    /// App rows that appear while the flyout is open (e.g. BetterTrumpet's own session, created
    /// by the first volume tick sound) grow in as one motion with the window edge.
    ///
    /// The window takes its final size once, up front, and is held toward its anchored edge so
    /// nothing moves at that moment; the overhang sits behind the taskbar or past the monitor
    /// edge. Each tick the row containers grow and the window is released by the same amount,
    /// so the far edge and everything on that side of the new row travel together while the
    /// anchored edge and the rows on the other side stay still. The window is only moved
    /// (never resized per frame: a layered window resize can present blank frames), and the move
    /// lands one tick after the layout it matches, because WPF presents a frame about one tick
    /// after laying it out while a window move shows immediately; moving on the same tick makes
    /// the rows that should stay still shiver.
    ///
    /// Rows that arrive while the pointer is captured (a slider being dragged) are held at height
    /// 0 until the capture ends, so a new row never shifts the slider under the cursor.
    /// </summary>
    public partial class FlyoutWindow
    {
        private sealed class EnteringRow
        {
            public AppItemView View;
            public ListViewItem Container;
            public bool Animate;
            public double NaturalHeight; // DIPs
            public double Height;        // DIPs, pixel-snapped value currently applied
            public TimeSpan? Start;
        }

        private readonly List<EnteringRow> _pendingRows = new List<EnteringRow>();
        private readonly List<EnteringRow> _growingRows = new List<EnteringRow>();
        private bool _isRowStartScheduled;
        private bool _isRowLoopRunning;
        private TimeSpan _lastRowTick = TimeSpan.MinValue;
        private int? _rowWindowOffsetPending;
        private DispatcherTimer _rowRetryTimer;
        private TranslateTransform _rowContentShift;
        private bool _isRepositioning;

        // Layout of the last reposition, with every growing row counted at its natural height.
        private bool _isRowLayoutValid;
        private double _rowFinalContentDip;
        private int _rowFinalHeightPx;
        private double _rowScale = 1;
        private bool _rowAnchorTop;
        private int _rowRoomPx;

        private void InitializeRowEntrance()
        {
            // Capture ends when a slider drag ends: start the rows that waited for it.
            AddHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler((_, __) => OnPointerCaptureLost()), true);
            AddHandler(LostTouchCaptureEvent, new EventHandler<TouchEventArgs>((_, __) => OnPointerCaptureLost()), true);
        }

        /// <summary>
        /// Called by an app row the first time it is realized for a session that appeared at
        /// runtime. Returns false when the row should simply appear at its natural height (flyout
        /// not open, or no animation and nothing to wait for); the window then resizes once
        /// through the regular size invalidation, as before.
        /// </summary>
        internal bool TryAdmitEnteringRow(AppItemView view, ListViewItem container, bool animate)
        {
            if (container == null || _viewModel.State != FlyoutViewState.Open)
            {
                return false;
            }

            if (!animate && !IsPointerInteractionActive())
            {
                return false;
            }

            // Loaded runs after layout and before the first render, so the container's desired
            // height is its natural one and the full-height row is never drawn.
            var natural = container.DesiredSize.Height > 0 ? container.DesiredSize.Height : container.ActualHeight;
            if (natural <= 0)
            {
                return false;
            }

            container.BeginAnimation(HeightProperty, null);
            container.ClipToBounds = true;
            container.Height = 0;
            view.PrepareEntrance();

            _pendingRows.Add(new EnteringRow
            {
                View = view,
                Container = container,
                Animate = animate,
                NaturalHeight = natural,
            });
            ScheduleRowStart();
            return true;
        }

        /// <summary>The row left the list (session gone, container reused) mid-entrance.</summary>
        internal void RemoveEnteringRow(AppItemView view)
        {
            for (int i = _pendingRows.Count - 1; i >= 0; i--)
            {
                if (_pendingRows[i].View == view)
                {
                    _pendingRows.RemoveAt(i);
                }
            }

            for (int i = _growingRows.Count - 1; i >= 0; i--)
            {
                if (_growingRows[i].View == view)
                {
                    // Both its current height and its remaining growth leave the layout. The
                    // window keeps its size until the size invalidation that follows the removal;
                    // meanwhile the offset computed from this keeps the anchored side still.
                    _rowFinalContentDip -= _growingRows[i].NaturalHeight;
                    _growingRows.RemoveAt(i);
                }
            }
        }

        private bool IsPointerInteractionActive()
        {
            return IsMouseCaptureWithin || AreAnyTouchesCapturedWithin;
        }

        private void OnPointerCaptureLost()
        {
            if (_pendingRows.Count > 0)
            {
                ScheduleRowStart();
            }
        }

        private void ScheduleRowStart()
        {
            if (_isRowStartScheduled)
            {
                return;
            }

            // Loaded priority: after the current layout pass (rows realized in the same pass
            // start together) and before the 16 ms size-invalidation debounce.
            _isRowStartScheduled = true;
            _ = Dispatcher.InvokeAsync(StartPendingRows, DispatcherPriority.Loaded);
        }

        private void StartPendingRows()
        {
            _isRowStartScheduled = false;
            if (_pendingRows.Count == 0 || _viewModel.State != FlyoutViewState.Open)
            {
                // A close in progress keeps them at height 0; ResetRowEntrance restores them.
                return;
            }

            if (IsPointerInteractionActive())
            {
                EnsureRowRetryTimer();
                return;
            }
            StopRowRetryTimer();

            bool animate = Themes.Manager.Current.AnimationsEnabled;
            bool anyGrowing = false;
            foreach (var row in _pendingRows)
            {
                if (animate && row.Animate)
                {
                    row.Start = null;
                    row.Height = 0;
                    row.View.BeginEntranceReveal();
                    _growingRows.Add(row);
                    anyGrowing = true;
                }
                else
                {
                    RestoreRow(row);
                }
            }
            _pendingRows.Clear();

            // One reposition for the batch: the window takes its final size now and the offset
            // computed in UpdateRowGrowthLayout keeps every visible pixel where it was.
            _isRowLayoutValid = false;
            TryReposition();

            if (!anyGrowing)
            {
                return;
            }

            if (_surface.GrowthOffsetY != 0 && IsRowOverhangBehindTaskbar())
            {
                _isBelowTaskbar = true;
                this.PlaceBelow(WindowsTaskbar.GetHwnd());
            }

            StartRowLoop();
        }

        private void TryReposition()
        {
            try
            {
                PositionWindowRelativeToTaskbar(WindowsTaskbar.Current);
            }
            catch (InvalidOperationException ex)
            {
                // Same ItemsSource inconsistency OnWindowsSizeInvalidated guards against; the
                // next size invalidation repositions again.
                Trace.WriteLine($"FlyoutWindow: row entrance reposition failed: {ex.Message}");
            }
        }

        private bool IsRowOverhangBehindTaskbar()
        {
            var taskbar = WindowsTaskbar.Current;
            if (!IsTaskbarOnFlyoutScreen(taskbar))
            {
                return false;
            }

            return _rowAnchorTop
                ? taskbar.Location == WindowsTaskbar.Position.Top
                : taskbar.Location == WindowsTaskbar.Position.Bottom;
        }

        private void StartRowLoop()
        {
            if (_isRowLoopRunning)
            {
                return;
            }

            _isRowLoopRunning = true;
            _lastRowTick = TimeSpan.MinValue;
            CompositionTarget.Rendering += OnRowRendering;
        }

        private void StopRowLoop()
        {
            if (!_isRowLoopRunning)
            {
                return;
            }

            _isRowLoopRunning = false;
            CompositionTarget.Rendering -= OnRowRendering;
        }

        private void OnRowRendering(object sender, EventArgs e)
        {
            // A resize inside PositionWindowRelativeToTaskbar renders synchronously and can raise
            // Rendering re-entrantly (pitfall 27); progress is time-based, so skipping is free.
            if (_isRepositioning)
            {
                return;
            }

            var time = e is RenderingEventArgs args ? args.RenderingTime : TimeSpan.FromTicks(DateTime.UtcNow.Ticks);
            if (time == _lastRowTick)
            {
                // Rendering can be raised more than once per frame.
                return;
            }
            _lastRowTick = time;

            // 1. The window position for the layout of the previous tick, which is the frame
            //    being presented now.
            if (_rowWindowOffsetPending.HasValue)
            {
                _surface.SetGrowthOffset(_rowWindowOffsetPending.Value, apply: true);
                _rowWindowOffsetPending = null;
            }

            // 2. Grow the rows (pixel-snapped so the layout and the window offset agree).
            for (int i = _growingRows.Count - 1; i >= 0; i--)
            {
                var row = _growingRows[i];
                if (!row.Start.HasValue)
                {
                    row.Start = time;
                }

                double linear = (time - row.Start.Value).TotalMilliseconds / FlyoutMotion.RowGrowMs;
                if (linear >= 1)
                {
                    row.Height = row.NaturalHeight;
                    row.Container.Height = double.NaN;
                    _growingRows.RemoveAt(i);
                    continue;
                }

                double eased = FlyoutMotion.Decelerate.GetSplineProgress(Math.Max(0, linear));
                double height = Math.Round(row.NaturalHeight * eased * _rowScale) / _rowScale;
                if (height != row.Height)
                {
                    row.Height = height;
                    row.Container.Height = height;
                }
            }

            // 3. Content shift now (rendered with this layout), window offset on the next tick.
            ComputeRowOffsets(out var windowPx, out var shiftDip);
            SetRowContentShift(shiftDip);
            if (windowPx != _surface.GrowthOffsetY)
            {
                _rowWindowOffsetPending = windowPx;
            }

            if (_growingRows.Count == 0 && !_rowWindowOffsetPending.HasValue)
            {
                StopRowLoop();
                SetRowContentShift(0);
                RaiseAboveTaskbar();
            }
        }

        /// <summary>DIPs the growing rows still have to grow; counted in the window size.</summary>
        private double GetRemainingRowGrowth()
        {
            double remaining = 0;
            for (int i = _growingRows.Count - 1; i >= 0; i--)
            {
                var row = _growingRows[i];
                if (!row.Container.IsLoaded)
                {
                    // Left the tree without telling us: its growth must not size the window.
                    _rowFinalContentDip -= row.NaturalHeight;
                    _growingRows.RemoveAt(i);
                    continue;
                }
                remaining += Math.Max(0, row.NaturalHeight - row.Height);
            }
            return remaining;
        }

        /// <summary>
        /// Called by every reposition with the layout counted at its final size. When the final
        /// window size changed (batch start, another row, a removal, DPI), the offsets are
        /// recomputed and applied by the SetRest that follows, so the resize keeps every visible
        /// pixel in place. Otherwise the tick loop keeps driving them.
        /// </summary>
        private void UpdateRowGrowthLayout(double finalContentDip, int finalHeightPx, double scale, bool anchorTop, int roomPx)
        {
            bool changed = !_isRowLayoutValid ||
                           finalHeightPx != _rowFinalHeightPx ||
                           Math.Abs(scale - _rowScale) > 0.0001 ||
                           anchorTop != _rowAnchorTop ||
                           roomPx != _rowRoomPx;

            _rowFinalContentDip = finalContentDip;
            _rowFinalHeightPx = finalHeightPx;
            _rowScale = scale > 0 ? scale : 1;
            _rowAnchorTop = anchorTop;
            _rowRoomPx = roomPx;
            _isRowLayoutValid = true;

            if (_growingRows.Count == 0)
            {
                _rowWindowOffsetPending = null;
                _surface.SetGrowthOffset(0, apply: false);
                SetRowContentShift(0);
                return;
            }

            if (changed)
            {
                ComputeRowOffsets(out var windowPx, out var shiftDip);
                _rowWindowOffsetPending = null;
                _surface.SetGrowthOffset(windowPx, apply: false);
                SetRowContentShift(shiftDip);
            }
        }

        /// <summary>
        /// need = final window height - height the content takes right now. A bottom-anchored
        /// window (bottom, left and right taskbars) is held down by it; a top-anchored one is
        /// held up and its content shifted back down by the same amount. What does not fit in the
        /// room toward the anchored edge (another monitor there) is shown as a content shift
        /// inside the final-size window instead.
        /// </summary>
        private void ComputeRowOffsets(out int windowPx, out double shiftDip)
        {
            int contentPx = (int)Math.Ceiling((_rowFinalContentDip - GetRemainingRowGrowth()) * _rowScale);
            int need = Math.Max(0, _rowFinalHeightPx - Math.Min(_rowFinalHeightPx, contentPx));
            int held = Math.Min(need, Math.Max(0, _rowRoomPx));

            int shiftPx;
            if (_rowAnchorTop)
            {
                windowPx = -held;
                shiftPx = held;
            }
            else
            {
                windowPx = held;
                shiftPx = need - held;
            }
            shiftDip = shiftPx / _rowScale;
        }

        /// <summary>
        /// Room past the anchored edge of the rest rect: gap and taskbar, then beyond the monitor
        /// edge, which is off-screen unless another monitor sits there.
        /// </summary>
        private static int GetRowGrowthRoom(Forms.Screen screen, int left, int top, int width, int height, bool anchorTop)
        {
            var bounds = screen.Bounds;
            int room = anchorTop ? top - bounds.Top : bounds.Bottom - (top + height);
            var beyond = anchorTop
                ? new System.Drawing.Rectangle(left, bounds.Top - 8192, width, 8192)
                : new System.Drawing.Rectangle(left, bounds.Bottom, width, 8192);

            foreach (var other in Forms.Screen.AllScreens)
            {
                if (other.DeviceName != screen.DeviceName && other.Bounds.IntersectsWith(beyond))
                {
                    return Math.Max(0, room);
                }
            }

            return int.MaxValue / 4;
        }

        private void SetRowContentShift(double dip)
        {
            if (dip == 0)
            {
                if (_rowContentShift != null)
                {
                    if (ReferenceEquals(ContentGrid.RenderTransform, _rowContentShift))
                    {
                        ContentGrid.RenderTransform = null;
                    }
                    _rowContentShift = null;
                }
                return;
            }

            if (_rowContentShift == null || !ReferenceEquals(ContentGrid.RenderTransform, _rowContentShift))
            {
                _rowContentShift = new TranslateTransform();
                ContentGrid.RenderTransform = _rowContentShift;
            }
            _rowContentShift.Y = dip;
        }

        private void EnsureRowRetryTimer()
        {
            // Safety net in case a capture ends without LostMouseCapture reaching the window.
            if (_rowRetryTimer == null)
            {
                _rowRetryTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
                _rowRetryTimer.Tick += (_, __) =>
                {
                    if (_pendingRows.Count == 0)
                    {
                        StopRowRetryTimer();
                        return;
                    }
                    ScheduleRowStart();
                };
            }

            if (!_rowRetryTimer.IsEnabled)
            {
                _rowRetryTimer.Start();
            }
        }

        private void StopRowRetryTimer()
        {
            _rowRetryTimer?.Stop();
        }

        /// <summary>Close started: stop driving, leave the visuals as they are for the exit.</summary>
        private void FreezeRowEntrance()
        {
            StopRowLoop();
            StopRowRetryTimer();
        }

        /// <summary>
        /// Window hidden or about to reopen: every row at its natural height, no offsets. The
        /// next PositionWindowRelativeToTaskbar sizes the window for them.
        /// </summary>
        private void ResetRowEntrance()
        {
            StopRowLoop();
            StopRowRetryTimer();

            foreach (var row in _pendingRows)
            {
                RestoreRow(row);
            }
            foreach (var row in _growingRows)
            {
                RestoreRow(row);
            }
            _pendingRows.Clear();
            _growingRows.Clear();

            _rowWindowOffsetPending = null;
            _isRowLayoutValid = false;
            _surface.SetGrowthOffset(0, apply: false);
            SetRowContentShift(0);
        }

        private static void RestoreRow(EnteringRow row)
        {
            row.Container.BeginAnimation(HeightProperty, null);
            row.Container.Height = double.NaN;
            row.View.EndEntrance();
        }
    }
}
