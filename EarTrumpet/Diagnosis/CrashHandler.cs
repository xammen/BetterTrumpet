using Sentry;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace EarTrumpet.Diagnosis
{
    /// <summary>
    /// Global unhandled exception handler for BetterTrumpet.
    /// Captures crashes from all sources: UI thread, background threads, async tasks.
    /// Logs to Trace (picked up by CircularBufferTraceListener) and shows a user-friendly dialog.
    /// In v3 Phase 3, these will also be forwarded to Sentry.
    /// </summary>
    static class CrashHandler
    {
        private static bool _isHandlingCrash;

        /// <summary>
        /// Call once during startup, AFTER ErrorReporter is initialized.
        /// </summary>
        public static void Initialize()
        {
            // 1. Non-UI thread exceptions (ThreadPool, finalizers, etc.)
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

            // 2. UI thread exceptions (WPF Dispatcher)
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
            }

            // 3. Async Task exceptions that were never observed
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            Trace.WriteLine("CrashHandler: Global exception handlers registered");
        }

        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            var message = ex?.ToString() ?? e.ExceptionObject?.ToString() ?? "Unknown exception";

            Trace.WriteLine($"## FATAL UNHANDLED EXCEPTION ##: IsTerminating={e.IsTerminating}\n{message}");

            // Forward to Sentry before the process dies
            if (ErrorReporter.IsSentryActive && ex != null)
            {
                try { SentrySdk.CaptureException(ex); SentrySdk.FlushAsync(TimeSpan.FromSeconds(2)).Wait(); }
                catch { }
            }

            if (e.IsTerminating)
            {
                ShowCrashDialog(ex, isFatal: true);
            }
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Trace.WriteLine($"## UI THREAD EXCEPTION ##: {e.Exception}");

            // Don't let it kill the app — mark as handled and show dialog
            e.Handled = true;

            // Prevent re-entrant crash dialogs
            if (_isHandlingCrash) return;

            try
            {
                _isHandlingCrash = true;
                ErrorReporter.LogWarning(e.Exception);
                ShowCrashDialog(e.Exception, isFatal: false);
            }
            finally
            {
                _isHandlingCrash = false;
            }
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            // These are fire-and-forget async tasks that threw.
            // Don't crash the app, just log them.
            Trace.WriteLine($"## UNOBSERVED TASK EXCEPTION ##: {e.Exception?.Flatten()}");
            e.SetObserved(); // Prevent process termination

            try
            {
                ErrorReporter.LogWarning(e.Exception?.Flatten());
            }
            catch
            {
                // Don't let logging itself crash
            }
        }

        /// <summary>
        /// Shows a user-friendly crash dialog. Non-fatal errors offer "Continue", fatal ones only "Quit".
        /// </summary>
        private static void ShowCrashDialog(Exception ex, bool isFatal)
        {
            try
            {
                var title = "BetterTrumpet";
                var body = isFatal
                    ? "BetterTrumpet a rencontr\u00e9 une erreur critique et doit fermer.\n\n"
                      + "Le rapport a \u00e9t\u00e9 enregistr\u00e9 dans les logs.\n\n"
                      + $"D\u00e9tails : {ex?.Message ?? "Erreur inconnue"}"
                    : "BetterTrumpet a rencontr\u00e9 un probl\u00e8me.\n\n"
                      + "L'application va tenter de continuer.\n\n"
                      + $"D\u00e9tails : {ex?.Message ?? "Erreur inconnue"}";

                var buttons = isFatal ? MessageBoxButton.OK : MessageBoxButton.OKCancel;

                // Run on a new thread to avoid deadlocking the dispatcher
                var dialogThread = new Thread(() =>
                {
                    var result = MessageBox.Show(body, title, buttons, MessageBoxImage.Error);

                    if (isFatal || result == MessageBoxResult.Cancel)
                    {
                        Environment.Exit(1);
                    }
                });
                dialogThread.SetApartmentState(ApartmentState.STA);
                dialogThread.IsBackground = true;
                dialogThread.Start();
            }
            catch
            {
                // Last resort — if even the dialog fails, just exit
                if (isFatal)
                {
                    Environment.Exit(1);
                }
            }
        }
    }
}
