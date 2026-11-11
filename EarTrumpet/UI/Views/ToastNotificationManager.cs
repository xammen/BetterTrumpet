using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace EarTrumpet.UI.Views
{
    public static class ToastNotificationManager
    {
        private static readonly List<ToastNotification> _activeToasts = new List<ToastNotification>();
        private const int ToastSpacing = 10;
        private const int BottomMargin = 20;

        public static ToastNotification Show(string message, string icon = "\xE946", int durationMs = ToastNotification.DefaultDisplayDurationMs, string title = null)
        {
            var toast = new ToastNotification(message, icon, durationMs, title);

            // Register this toast
            _activeToasts.Add(toast);

            // Position based on existing toasts
            PositionToast(toast);

            // When toast closes, remove it and reposition others
            toast.Closed += (s, e) =>
            {
                _activeToasts.Remove(toast);
                RepositionAllToasts();
            };

            toast.Show();
            return toast;
        }

        private static void PositionToast(ToastNotification toast)
        {
            var workingArea = SystemParameters.WorkArea;

            // Calculate vertical offset based on existing toasts
            double totalHeight = BottomMargin;

            // Sum up heights of toasts below this one
            var index = _activeToasts.IndexOf(toast);
            for (int i = 0; i < index; i++)
            {
                var existingToast = _activeToasts[i];
                if (existingToast.IsLoaded)
                {
                    totalHeight += existingToast.ActualHeight + ToastSpacing;
                }
                else
                {
                    // Estimate height before loaded (from XAML default)
                    totalHeight += 80 + ToastSpacing;
                }
            }

            // Position horizontally at right edge, vertically stacked from bottom
            toast.Left = workingArea.Right - toast.Width - 20;
            toast.Top = workingArea.Bottom - totalHeight - toast.Height;

            // Update position after loaded for accurate sizing
            toast.Loaded += (s, e) =>
            {
                toast.Left = workingArea.Right - toast.ActualWidth - 20;
                RepositionAllToasts();
            };
        }

        private static void RepositionAllToasts()
        {
            var workingArea = SystemParameters.WorkArea;
            double currentBottom = BottomMargin;

            // Position toasts from bottom to top
            for (int i = 0; i < _activeToasts.Count; i++)
            {
                var toast = _activeToasts[i];
                if (!toast.IsLoaded) continue;

                var targetTop = workingArea.Bottom - currentBottom - toast.ActualHeight;

                // Smooth animation if position changed significantly
                if (Math.Abs(toast.Top - targetTop) > 5)
                {
                    toast.AnimateToPosition(targetTop);
                }
                else
                {
                    toast.Top = targetTop;
                }

                currentBottom += toast.ActualHeight + ToastSpacing;
            }
        }
    }
}
