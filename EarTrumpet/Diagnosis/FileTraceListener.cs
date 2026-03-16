using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace EarTrumpet.Diagnosis
{
    /// <summary>
    /// Structured file-based trace listener with log rotation.
    /// Writes logs to disk for post-crash diagnosis.
    /// Rotation: max 5 files of 5MB each (25MB total cap).
    /// 
    /// Log format:
    /// [2026-03-16 14:23:45.123] [INFO ] [UI] Starting BetterTrumpet v3.0.0
    /// [2026-03-16 14:23:45.234] [WARN ] [03] Custom theme has invalid color
    /// [2026-03-16 14:23:45.456] [ERROR] [UI] Media session failed: {exception}
    /// </summary>
    class FileTraceListener : TraceListener
    {
        private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB
        private const int MaxFileCount = 5;

        private readonly string _logDirectory;
        private readonly string _baseFileName;
        private readonly object _lock = new object();
        private StreamWriter _writer;
        private string _currentFilePath;
        private long _currentFileSize;

        public FileTraceListener(string logDirectory, string baseFileName = "bettertrumpet")
        {
            _logDirectory = logDirectory;
            _baseFileName = baseFileName;

            try
            {
                if (!Directory.Exists(_logDirectory))
                    Directory.CreateDirectory(_logDirectory);

                OpenNewLogFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FileTraceListener: Failed to initialize: {ex.Message}");
            }
        }

        public override void Write(string message)
        {
            // Not used — all writes go through WriteLine
        }

        public override void WriteLine(string message)
        {
            if (_writer == null) return;

            var threadId = Thread.CurrentThread.ManagedThreadId;
            var threadLabel = threadId == 1 ? "UI" : threadId.ToString().PadLeft(2, '0');

            // Detect log level from message content
            var level = DetectLevel(message);

            var formatted = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{threadLabel}] {message}";

            lock (_lock)
            {
                try
                {
                    _writer.WriteLine(formatted);
                    _writer.Flush();
                    _currentFileSize += formatted.Length + Environment.NewLine.Length;

                    if (_currentFileSize >= MaxFileSizeBytes)
                    {
                        RotateLogFile();
                    }
                }
                catch
                {
                    // Logging should never crash the app
                }
            }
        }

        /// <summary>
        /// Returns the full path of the current log file (for "Export logs" feature).
        /// </summary>
        public string GetCurrentLogFilePath() => _currentFilePath;

        /// <summary>
        /// Returns the log directory path (for exporting all logs).
        /// </summary>
        public string GetLogDirectory() => _logDirectory;

        private string DetectLevel(string message)
        {
            if (message == null) return "INFO ";

            // Match patterns already used in the codebase
            if (message.Contains("## FATAL") || message.Contains("## UNHANDLED"))
                return "FATAL";
            if (message.Contains("## WARNING") || message.Contains("## Warning") || message.StartsWith("WARN"))
                return "WARN ";
            if (message.Contains("ERROR") || message.Contains("Exception") || message.Contains("failed") || message.Contains("Failed"))
                return "ERROR";
            if (message.Contains("## UI THREAD EXCEPTION") || message.Contains("## UNOBSERVED TASK"))
                return "ERROR";

            return "INFO ";
        }

        private void OpenNewLogFile()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            _currentFilePath = Path.Combine(_logDirectory, $"{_baseFileName}-{timestamp}.log");
            _writer = new StreamWriter(_currentFilePath, append: true) { AutoFlush = false };
            _currentFileSize = new FileInfo(_currentFilePath).Exists ? new FileInfo(_currentFilePath).Length : 0;
        }

        private void RotateLogFile()
        {
            try
            {
                _writer?.Close();
                _writer?.Dispose();

                // Delete oldest files if we exceed the max count
                var files = Directory.GetFiles(_logDirectory, $"{_baseFileName}-*.log");
                Array.Sort(files); // Oldest first (alphabetical = chronological with our naming)

                while (files.Length >= MaxFileCount)
                {
                    File.Delete(files[0]);
                    files = Directory.GetFiles(_logDirectory, $"{_baseFileName}-*.log");
                }

                OpenNewLogFile();
            }
            catch
            {
                // Rotation failure should not crash the app
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_lock)
                {
                    _writer?.Flush();
                    _writer?.Close();
                    _writer?.Dispose();
                    _writer = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
