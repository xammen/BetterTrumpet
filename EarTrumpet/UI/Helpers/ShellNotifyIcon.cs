using EarTrumpet.DataModel;
using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace EarTrumpet.UI.Helpers
{
    public class ShellNotifyIcon
    {
        private const double TrayContextMenuGap = 8;

        public class SecondaryInvokeArgs
        {
            public InputType InputType { get; set; }
            public Point Point { get; set; }
        }

        public event EventHandler<InputType> PrimaryInvoke;
        public event EventHandler<SecondaryInvokeArgs> SecondaryInvoke;
        public event EventHandler<InputType> TertiaryInvoke;
        public event EventHandler<int> Scrolled;
        public event EventHandler<bool> MouseHoverChanged;

        public IShellNotifyIconSource IconSource { get; private set; }
        public bool IsMouseOver { get; private set; }
        public Rect IconBounds => new Rect(_iconLocation.Left, _iconLocation.Top, _iconLocation.Right - _iconLocation.Left, _iconLocation.Bottom - _iconLocation.Top);

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (value != _isVisible)
                {
                    _isVisible = value;
                    Update();
                    Trace.WriteLine($"ShellNotifyIcon IsVisible {_isVisible}");
                }
            }
        }

        private const int WM_CALLBACKMOUSEMSG = User32.WM_USER + 1024;

        private readonly Win32Window _window;
        private readonly DispatcherTimer _invalidationTimer;
        private bool _isCreated;
        private bool _isVisible;
        private bool _isListeningForInput;
        private bool _isContextMenuOpen;
        private string _text;
        private RECT _iconLocation;
        private System.Drawing.Point _cursorPosition;
        private int _remainingTicks;
        private bool _hasAlreadyProcessedButtonUp;
        private bool HasAlreadyProcessedButtonUp
        {
            get
            {
                var val = _hasAlreadyProcessedButtonUp;
                _hasAlreadyProcessedButtonUp = false;
                return val;
            }
            set
            {
                _hasAlreadyProcessedButtonUp = value;
            }
        }

        public ShellNotifyIcon(IShellNotifyIconSource icon)
        {
            IconSource = icon;
            IconSource.Changed += (_) => Update();
            _window = new Win32Window();
            _window.Initialize(WndProc);
            _invalidationTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Normal, (_, __) => OnDelayedIconCheckForUpdate(), Dispatcher.CurrentDispatcher);

            Themes.Manager.Current.PropertyChanged += (_, __) => ScheduleDelayedIconInvalidation();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, __) => ScheduleDelayedIconInvalidation();
        }

        public void SetFocus()
        {
            Trace.WriteLine("ShellNotifyIcon SetFocus");
            var data = MakeData();
            if (!Shell32.Shell_NotifyIconW(Shell32.NotifyIconMessage.NIM_SETFOCUS, ref data))
            {
                Trace.WriteLine($"ShellNotifyIcon NIM_SETFOCUS Failed: {(uint)Marshal.GetLastWin32Error()}");
            }
        }

        public void SetTooltip(string text)
        {
            _text = text;
            Update();
        }

        public bool TryGetIconBounds(out Rect bounds)
        {
            if (TryRefreshIconLocation())
            {
                bounds = IconBounds;
                return true;
            }

            bounds = Rect.Empty;
            return false;
        }

        public void ShowNotification(string title, string message)
        {
            if (!_isVisible) return;

            // Use custom toast notification instead of native Windows notification
            // This allows proper stacking when multiple notifications appear quickly
            var fullMessage = string.IsNullOrEmpty(title) ? message : $"{title}\n{message}";
            EarTrumpet.UI.Views.ToastNotificationManager.Show(fullMessage, "\xE767");
        }

        public void ShowToast(string message, string icon = null)
        {
            EarTrumpet.UI.Views.ToastNotificationManager.Show(message, icon ?? "\xE767");
        }

        private NOTIFYICONDATAW MakeData()
        {
            var icon = IconSource?.Current;
            return new NOTIFYICONDATAW
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATAW)),
                hWnd = _window.Handle,
                uFlags = NotifyIconFlags.NIF_MESSAGE | NotifyIconFlags.NIF_ICON | NotifyIconFlags.NIF_TIP | NotifyIconFlags.NIF_SHOWTIP,
                uCallbackMessage = WM_CALLBACKMOUSEMSG,
                hIcon = icon?.Handle ?? IntPtr.Zero,
                szTip = _text
            };
        }

        private void Update()
        {
            var data = MakeData();
            if (_isVisible)
            {
                if (_isCreated)
                {
                    if (!Shell32.Shell_NotifyIconW(Shell32.NotifyIconMessage.NIM_MODIFY, ref data))
                    {
                        // Modification will fail when the shell restarts, or if message processing times out
                        Trace.WriteLine($"ShellNotifyIcon Update NIM_MODIFY Failed: {(uint)Marshal.GetLastWin32Error()}");
                        _isCreated = false;
                        Update();
                    }
                }
                else
                {
                    if (!Shell32.Shell_NotifyIconW(Shell32.NotifyIconMessage.NIM_ADD, ref data))
                    {
                        Trace.WriteLine($"ShellNotifyIcon Update NIM_ADD Failed {(uint)Marshal.GetLastWin32Error()}");
                    }

                    _isCreated = true;
                    data.uTimeoutOrVersion = Shell32.NOTIFYICON_VERSION_4;
                    if (!Shell32.Shell_NotifyIconW(Shell32.NotifyIconMessage.NIM_SETVERSION, ref data))
                    {
                        Trace.WriteLine($"ShellNotifyIcon Update NIM_SETVERSION Failed: {(uint)Marshal.GetLastWin32Error()}");
                    }
                }
            }
            else if (_isCreated)
            {
                if (!Shell32.Shell_NotifyIconW(Shell32.NotifyIconMessage.NIM_DELETE, ref data))
                {
                    Trace.WriteLine($"ShellNotifyIcon Update NIM_DELETE Failed: {(uint)Marshal.GetLastWin32Error()}");
                }
                _isCreated = false;
            }
        }

        private void WndProc(System.Windows.Forms.Message msg)
        {
            if (msg.Msg == WM_CALLBACKMOUSEMSG)
            {
                CallbackMsgWndProc(msg);
            }
            else if (msg.Msg == Shell32.WM_TASKBARCREATED ||
                    (msg.Msg == User32.WM_SETTINGCHANGE && (int)msg.WParam == User32.SPI_SETWORKAREA))
            {
                ScheduleDelayedIconInvalidation();
            }
            else if (msg.Msg == User32.WM_INPUT)
            {
                _cursorPosition = System.Windows.Forms.Cursor.Position;
                if (InputHelper.ProcessMouseInputMessage(msg.LParam, ref _cursorPosition, out int wheelDelta) &&
                                IsCursorWithinNotifyIconBounds() && wheelDelta != 0)
                {
                    Scrolled?.Invoke(this, wheelDelta);
                }
            }
        }

        private void CallbackMsgWndProc(System.Windows.Forms.Message msg)
        {
            switch ((short)msg.LParam)
            {
                case (short)Shell32.NotifyIconNotification.NIN_SELECT:
                case User32.WM_LBUTTONUP:
                    // Observed double WM_CALLBACKMOUSEMSG/WM_LBUTTONUP pairs on Windows 11 22533
                    // Could be a result of XAML island use in Taskbar. Or a bug elsewhere.
                    // For now, swallow the duplicate to improve flyout UX.
                    if (!HasAlreadyProcessedButtonUp)
                    {
                        HasAlreadyProcessedButtonUp = true;
                        PrimaryInvoke?.Invoke(this, InputType.Mouse);
                    }
                    break;
                case (short)Shell32.NotifyIconNotification.NIN_KEYSELECT:
                    PrimaryInvoke?.Invoke(this, InputType.Keyboard);
                    break;
                case User32.WM_MBUTTONUP:
                    TertiaryInvoke?.Invoke(this, InputType.Mouse);
                    break;
                case User32.WM_CONTEXTMENU:
                    SecondaryInvoke?.Invoke(this, CreateSecondaryInvokeArgs(InputType.Keyboard, msg.WParam));
                    break;
                case User32.WM_RBUTTONUP:
                    SecondaryInvoke?.Invoke(this, CreateSecondaryInvokeArgs(InputType.Mouse, msg.WParam));
                    break;
                case User32.WM_MOUSEMOVE:
                    OnNotifyIconMouseMove();
                    IconSource.CheckForUpdate();
                    break;
            }
        }

        private SecondaryInvokeArgs CreateSecondaryInvokeArgs(InputType type, IntPtr wParam) => new SecondaryInvokeArgs
        {
            InputType = type,
            Point = PointFromPackedCoords(wParam)
        };

        // The notify-icon callback packs the anchor point into wParam as MAKEWPARAM(x, y). On
        // 64-bit that DWORD arrives zero-extended in a UINT_PTR, so any negative y — keyboard
        // invocation sends (-1,-1), and monitors above the primary are negative too — sets bit 31
        // and IntPtr.ToInt32() would throw OverflowException (it throws regardless of the
        // checked/unchecked context). Truncate explicitly, then sign-extend each 16-bit half.
        private static Point PointFromPackedCoords(IntPtr wParam)
        {
            var packed = unchecked((int)wParam.ToInt64());
            return new Point((short)packed, (short)(packed >> 16));
        }

        private void OnNotifyIconMouseMove()
        {
            TryRefreshIconLocation();
            _cursorPosition = System.Windows.Forms.Cursor.Position;
            IsCursorWithinNotifyIconBounds();
        }

        private bool TryRefreshIconLocation()
        {
            var id = new NOTIFYICONIDENTIFIER
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONIDENTIFIER)),
                hWnd = _window.Handle
            };

            if (Shell32.Shell_NotifyIconGetRect(ref id, out RECT location) == 0)
            {
                _iconLocation = location;
                return true;
            }

            _iconLocation = default(RECT);
            return false;
        }

        private bool IsCursorWithinNotifyIconBounds()
        {
            bool isInBounds = _iconLocation.Contains(_cursorPosition);
            if (isInBounds)
            {
                if (!_isListeningForInput)
                {
                    _isListeningForInput = true;
                    InputHelper.RegisterForMouseInput(_window.Handle);
                }
            }
            else
            {
                if (_isListeningForInput)
                {
                    _isListeningForInput = false;
                    InputHelper.UnregisterForMouseInput();
                }
            }

            bool isChanged = (IsMouseOver != isInBounds);
            IsMouseOver = isInBounds;
            if (isChanged)
            {
                IconSource.OnMouseOverChanged(IsMouseOver);
                MouseHoverChanged?.Invoke(this, IsMouseOver);
            }

            return isInBounds;
        }

        private void ScheduleDelayedIconInvalidation()
        {
            _remainingTicks = 10;
            _invalidationTimer.Start();

            IconSource.CheckForUpdate();
        }

        private void OnDelayedIconCheckForUpdate()
        {
            _remainingTicks--;
            if (_remainingTicks <= 0)
            {
                _invalidationTimer.Stop();
                // Force a final update to protect us from the shell doing implicit work
                Update();
            }

            IconSource.CheckForUpdate();
        }

        public void ShowContextMenu(IEnumerable itemsSource, Point point)
        {
            if (!_isContextMenuOpen)
            {
                _isContextMenuOpen = true;
                Trace.WriteLine("ShellNotifyIcon ShowContextMenu");
                var contextMenu = new ContextMenu
                {
                    FlowDirection = SystemSettings.IsRTL ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                    HasDropShadow = false,
                    StaysOpen = true,
                    ItemsSource = itemsSource
                };

                if (HasValidContextMenuPoint(point))
                {
                    ConfigureTrayContextMenuPlacement(contextMenu, point);
                }

                Themes.Options.SetSource(contextMenu, Themes.Options.SourceKind.System);
                Themes.Brush.SetBackground(contextMenu, "AcrylicMenuBackground");
                Themes.Brush.SetBorderBrush(contextMenu, "AcrylicMenuBorder");
                contextMenu.PreviewKeyDown += (_, e) =>
                {
                    if (e.Key == Key.Escape)
                    {
                        SetFocus();
                    }
                };
                contextMenu.Opened += (_, __) =>
                {
                    Trace.WriteLine("ShellNotifyIcon ContextMenu.Opened");
                    // Workaround: The framework expects there to already be a WPF window open and thus fails to take focus.
                    var popupSource = (HwndSource)HwndSource.FromVisual(contextMenu);
                    User32.SetForegroundWindow(popupSource.Handle);
                    contextMenu.Focus();
                    contextMenu.StaysOpen = false;
                    // Disable only the exit animation.
                    var popup = (Popup)contextMenu.Parent;
                    popup.PopupAnimation = PopupAnimation.None;
                    UI.Behaviors.PopupEx.ApplyMenuAcrylic(popup, contextMenu);
                    if (HasValidContextMenuPoint(point))
                    {
                        PositionTrayContextMenu(popupSource.Handle, point);
                    }
                };
                contextMenu.Closed += (_, __) =>
                {
                    Trace.WriteLine("ShellNotifyIcon ContextMenu.Closed");
                    _isContextMenuOpen = false;
                };
                contextMenu.IsOpen = true;
            }
        }

        private static void ConfigureTrayContextMenuPlacement(ContextMenu contextMenu, Point point)
        {
            var screen = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point((int)point.X, (int)point.Y));
            var taskbarPosition = GetTaskbarPosition(screen);
            var dpiScale = Math.Max(1, WindowsTaskbar.Dpi / (double)96);
            var horizontalOffset = point.X / dpiScale;
            var verticalOffset = point.Y / dpiScale;

            contextMenu.PlacementRectangle = Rect.Empty;
            contextMenu.PlacementTarget = null;

            switch (taskbarPosition)
            {
                case WindowsTaskbar.Position.Top:
                    contextMenu.Placement = PlacementMode.Bottom;
                    verticalOffset = (screen.WorkingArea.Top / dpiScale) + TrayContextMenuGap;
                    break;

                case WindowsTaskbar.Position.Left:
                    contextMenu.Placement = PlacementMode.Right;
                    horizontalOffset = (screen.WorkingArea.Left / dpiScale) + TrayContextMenuGap;
                    break;

                case WindowsTaskbar.Position.Right:
                    contextMenu.Placement = PlacementMode.Left;
                    horizontalOffset = (screen.WorkingArea.Right / dpiScale) - TrayContextMenuGap;
                    break;

                default:
                    contextMenu.Placement = PlacementMode.Top;
                    verticalOffset = (screen.WorkingArea.Bottom / dpiScale) - TrayContextMenuGap;
                    break;
            }

            contextMenu.HorizontalOffset = horizontalOffset;
            contextMenu.VerticalOffset = verticalOffset;
        }

        private static bool HasValidContextMenuPoint(Point point)
        {
            // WM_CONTEXTMENU uses (-1,-1) for keyboard invocation. Negative screen
            // coordinates are otherwise valid on monitors left of or above primary.
            return point.X != -1 || point.Y != -1;
        }

        private static void PositionTrayContextMenu(IntPtr popupHwnd, Point point)
        {
            if (popupHwnd == IntPtr.Zero || !User32.GetWindowRect(popupHwnd, out var popupRect))
            {
                return;
            }

            var screen = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point((int)point.X, (int)point.Y));
            var workArea = screen.WorkingArea;
            var taskbarPosition = GetTaskbarPosition(screen);
            var dpiScale = Math.Max(1, User32.GetDpiForWindow(popupHwnd) / (double)96);
            var gap = Math.Max(1, (int)Math.Round(TrayContextMenuGap * dpiScale));
            var width = popupRect.Right - popupRect.Left;
            var height = popupRect.Bottom - popupRect.Top;
            var left = popupRect.Left;
            var top = popupRect.Top;

            switch (taskbarPosition)
            {
                case WindowsTaskbar.Position.Top:
                    top = workArea.Top + gap;
                    break;

                case WindowsTaskbar.Position.Left:
                    left = workArea.Left + gap;
                    break;

                case WindowsTaskbar.Position.Right:
                    left = workArea.Right - width - gap;
                    break;

                default:
                    top = workArea.Bottom - height - gap;
                    break;
            }

            var minLeft = workArea.Left + gap;
            var maxLeft = Math.Max(minLeft, workArea.Right - width - gap);
            var minTop = workArea.Top + gap;
            var maxTop = Math.Max(minTop, workArea.Bottom - height - gap);
            left = Math.Max(minLeft, Math.Min(left, maxLeft));
            top = Math.Max(minTop, Math.Min(top, maxTop));

            User32.SetWindowPos(
                popupHwnd,
                IntPtr.Zero,
                left,
                top,
                0,
                0,
                User32.WindowPosFlags.SWP_NOSIZE |
                User32.WindowPosFlags.SWP_NOZORDER |
                User32.WindowPosFlags.SWP_NOACTIVATE);

            Trace.WriteLine(
                $"ShellNotifyIcon ContextMenu positioned: Edge={taskbarPosition}, Gap={gap}px, Rect=[{left},{top},{left + width},{top + height}], WorkArea={workArea}");
        }

        private static WindowsTaskbar.Position GetTaskbarPosition(System.Windows.Forms.Screen screen)
        {
            var bounds = screen.Bounds;
            var workArea = screen.WorkingArea;

            if (workArea.Top > bounds.Top)
            {
                return WindowsTaskbar.Position.Top;
            }

            if (workArea.Left > bounds.Left)
            {
                return WindowsTaskbar.Position.Left;
            }

            if (workArea.Right < bounds.Right)
            {
                return WindowsTaskbar.Position.Right;
            }

            if (workArea.Bottom < bounds.Bottom)
            {
                return WindowsTaskbar.Position.Bottom;
            }

            return WindowsTaskbar.Current.Location;
        }
    }
}
