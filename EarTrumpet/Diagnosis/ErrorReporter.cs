using EarTrumpet.DataModel.Storage;
using Sentry;
using System;
using System.Diagnostics;
using System.IO;

namespace EarTrumpet.Diagnosis
{
    class ErrorReporter
    {
        // ═══════════════════════════════════════════════════════════════
        // SENTRY DSN — Replace with your project's DSN from sentry.io
        // Free plan: 5K events/month, 1 user, 30 days retention
        // Set to empty string to disable Sentry entirely
        // ═══════════════════════════════════════════════════════════════
        private const string SentryDsn = "https://7a99ae65aeea77906126eb19b94782a6@o4511055986884608.ingest.de.sentry.io/4511055991668816";

        private static ErrorReporter s_instance;
        private readonly CircularBufferTraceListener _listener;
        private readonly FileTraceListener _fileListener;
        private readonly AppSettings _settings;
        private static IDisposable _sentryDisposable;

        /// <summary>
        /// True if Sentry is currently active (DSN configured + user opted in).
        /// </summary>
        public static bool IsSentryActive => _sentryDisposable != null;

        public ErrorReporter(AppSettings settings)
        {
            Debug.Assert(s_instance == null);
            s_instance = this;
            _settings = settings;

            _listener = new CircularBufferTraceListener();
            Trace.Listeners.Clear();
            Trace.Listeners.Add(_listener);

            // File logging with rotation (5 files x 5MB)
            var logDir = GetLogDirectory();
            _fileListener = new FileTraceListener(logDir);
            Trace.Listeners.Add(_fileListener);

            Trace.WriteLine($"BetterTrumpet v{App.PackageVersion} starting — " +
                $"OS: {Environment.OSVersion}, " +
                $".NET: {Environment.Version}, " +
                $"Portable: {StorageFactory.IsPortableMode}");

            // Initialize Sentry if user has opted in (GDPR)
            InitializeSentry();

            // Re-initialize when user toggles telemetry consent in Settings
            settings.TelemetryConsentChanged += () =>
            {
                Trace.WriteLine($"Sentry: Telemetry consent changed to {settings.IsTelemetryEnabled}");
                InitializeSentry();
            };
        }

        /// <summary>
        /// Initializes Sentry crash reporting if:
        /// 1. A DSN is configured (not empty)
        /// 2. The user has opted in via Settings (IsTelemetryEnabled)
        /// Can be called again to re-initialize after opt-in changes.
        /// </summary>
        public void InitializeSentry()
        {
            // Dispose previous instance if re-initializing
            _sentryDisposable?.Dispose();
            _sentryDisposable = null;

            if (string.IsNullOrEmpty(SentryDsn))
            {
                Trace.WriteLine("Sentry: DSN not configured — crash reporting disabled");
                return;
            }

            if (!_settings.IsTelemetryEnabled)
            {
                Trace.WriteLine("Sentry: User has not opted in — crash reporting disabled (GDPR)");
                return;
            }

            try
            {
                _sentryDisposable = SentrySdk.Init(options =>
                {
                    options.Dsn = SentryDsn;
                    options.Release = $"bettertrumpet@{App.PackageVersion}";
                    options.Environment = 
#if DEBUG
                        "development";
#else
                        "production";
#endif

                    // Debug mode: logs Sentry internals to Trace output
                    // Enable for first-time setup, disable in production
#if DEBUG
                    options.Debug = true;
                    options.DiagnosticLevel = SentryLevel.Info;
#endif

                    // Tracing: capture 100% in dev, 20% in prod
#if DEBUG
                    options.TracesSampleRate = 1.0;
#else
                    options.TracesSampleRate = 0.2;
#endif

                    // Privacy: don't send PII
                    options.SendDefaultPii = false;

                    // Attach log breadcrumbs (last 100 trace messages)
                    options.MaxBreadcrumbs = 100;

                    // Auto session tracking (Release Health)
                    options.AutoSessionTracking = true;

                    // Stack traces on all events
                    options.AttachStacktrace = true;

                    // Shutdown timeout — ensure events flush on exit
                    options.ShutdownTimeout = TimeSpan.FromSeconds(3);
                });

                // Set context tags
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("portable_mode", StorageFactory.IsPortableMode.ToString());
                    scope.SetTag("os_version", Environment.OSVersion.Version.ToString());
                    scope.SetTag("dotnet_version", Environment.Version.ToString());
                    scope.SetTag("architecture", Environment.Is64BitProcess ? "x64" : "x86");
                    scope.SetTag("windows_build", Environment.OSVersion.Version.Build.ToString());
                });

                Trace.WriteLine("Sentry: Initialized — crash reporting active");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Sentry: Failed to initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// Shuts down Sentry cleanly (call on app exit).
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                _sentryDisposable?.Dispose();
                _sentryDisposable = null;
            }
            catch { }
        }

        public void DisplayDiagnosticData()
        {
            LocalDataExporter.DumpAndShowData(_listener.GetLogText());
        }

        /// <summary>
        /// Returns the log directory path. For portable mode, logs go next to the exe.
        /// For normal mode, logs go in %APPDATA%\BetterTrumpet\logs.
        /// </summary>
        public static string GetLogDirectory()
        {
            if (StorageFactory.IsPortableMode)
            {
                var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                return Path.Combine(exeDir, "config", "logs");
            }
            else
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BetterTrumpet", "logs");
            }
        }

        /// <summary>
        /// Returns the log directory path for the "Export logs" feature.
        /// </summary>
        public string GetLogDirectoryPath() => _fileListener?.GetLogDirectory();

        public static void LogWarning(Exception ex)
        {
            s_instance?.LogWarningInstance(ex);
        }
        
        private void LogWarningInstance(Exception ex)
        {
            Trace.WriteLine($"## Warning Notify ##: {ex}");

            // Forward to Sentry as a non-fatal event
            if (IsSentryActive)
            {
                try { SentrySdk.CaptureException(ex); }
                catch { /* Sentry should never crash the app */ }
            }
        }
    }
}
