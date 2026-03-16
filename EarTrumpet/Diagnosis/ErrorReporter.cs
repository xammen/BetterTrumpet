using EarTrumpet.DataModel.Storage;
using System;
using System.Diagnostics;
using System.IO;

namespace EarTrumpet.Diagnosis
{
    class ErrorReporter
    {
        private static ErrorReporter s_instance;
        private readonly CircularBufferTraceListener _listener;
        private readonly FileTraceListener _fileListener;

        public ErrorReporter(AppSettings settings)
        {
            Debug.Assert(s_instance == null);
            s_instance = this;

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

            // Telemetry: Sentry will be initialized here in v3 (Phase 3)
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

        public static void LogWarning(Exception ex) => s_instance?.LogWarningInstance(ex);
        
        private void LogWarningInstance(Exception ex)
        {
            Trace.WriteLine($"## Warning Notify ##: {ex}");
        }
    }
}
