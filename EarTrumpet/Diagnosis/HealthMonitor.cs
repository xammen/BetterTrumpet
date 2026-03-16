using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace EarTrumpet.Diagnosis
{
    /// <summary>
    /// Lightweight health monitoring for BetterTrumpet.
    /// Checks every 5 minutes for resource leaks and performance issues.
    /// 
    /// Thresholds:
    ///   - Memory: warn at 200MB, critical at 400MB
    ///   - GDI handles: warn at 5000, critical at 8000 (Windows limit ~10000)
    ///   - User handles: warn at 5000, critical at 8000
    /// 
    /// When critical thresholds are exceeded, logs an alert.
    /// In v3 Phase 3, these will also be reported to Sentry as breadcrumbs.
    /// </summary>
    static class HealthMonitor
    {
        private static DispatcherTimer _timer;
        private static long _peakMemoryMB;
        private static int _peakGdiHandles;
        private static int _peakUserHandles;

        private const long MemoryWarnMB = 200;
        private const long MemoryCriticalMB = 400;
        private const int HandleWarn = 5000;
        private const int HandleCritical = 8000;
        private const int CheckIntervalMinutes = 5;

        /// <summary>
        /// Starts the health monitoring timer. Call once during startup Phase 3.
        /// </summary>
        public static void Start()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(CheckIntervalMinutes)
            };
            _timer.Tick += (_, __) => CheckHealth();
            _timer.Start();

            // Initial check
            CheckHealth();
            Trace.WriteLine("HealthMonitor: Started (checking every 5 minutes)");
        }

        public static void Stop()
        {
            _timer?.Stop();
            _timer = null;
        }

        private static void CheckHealth()
        {
            try
            {
                var process = Process.GetCurrentProcess();

                // Memory check
                var memoryMB = process.WorkingSet64 / (1024 * 1024);
                if (memoryMB > _peakMemoryMB) _peakMemoryMB = memoryMB;

                if (memoryMB > MemoryCriticalMB)
                {
                    Trace.WriteLine($"HealthMonitor: CRITICAL — Memory {memoryMB}MB exceeds {MemoryCriticalMB}MB limit (peak: {_peakMemoryMB}MB)");
                }
                else if (memoryMB > MemoryWarnMB)
                {
                    Trace.WriteLine($"HealthMonitor: WARN — Memory {memoryMB}MB exceeds {MemoryWarnMB}MB warning (peak: {_peakMemoryMB}MB)");
                }

                // GDI handle check (Windows has a per-process limit of ~10000)
                var gdiHandles = GetGuiResources(process.Handle, 0); // GR_GDIOBJECTS
                if (gdiHandles > _peakGdiHandles) _peakGdiHandles = gdiHandles;

                if (gdiHandles > HandleCritical)
                {
                    Trace.WriteLine($"HealthMonitor: CRITICAL — GDI handles {gdiHandles} exceeds {HandleCritical} (peak: {_peakGdiHandles})");
                }
                else if (gdiHandles > HandleWarn)
                {
                    Trace.WriteLine($"HealthMonitor: WARN — GDI handles {gdiHandles} exceeds {HandleWarn} (peak: {_peakGdiHandles})");
                }

                // User handle check
                var userHandles = GetGuiResources(process.Handle, 1); // GR_USEROBJECTS
                if (userHandles > _peakUserHandles) _peakUserHandles = userHandles;

                if (userHandles > HandleCritical)
                {
                    Trace.WriteLine($"HealthMonitor: CRITICAL — User handles {userHandles} exceeds {HandleCritical} (peak: {_peakUserHandles})");
                }
                else if (userHandles > HandleWarn)
                {
                    Trace.WriteLine($"HealthMonitor: WARN — User handles {userHandles} exceeds {HandleWarn} (peak: {_peakUserHandles})");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"HealthMonitor: Check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns a snapshot of current health metrics (for diagnostics/export).
        /// </summary>
        public static string GetHealthSummary()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var memMB = process.WorkingSet64 / (1024 * 1024);
                var gdi = GetGuiResources(process.Handle, 0);
                var user = GetGuiResources(process.Handle, 1);
                var threads = process.Threads.Count;
                var uptime = DateTime.Now - process.StartTime;

                return $"Memory: {memMB}MB (peak: {_peakMemoryMB}MB) | " +
                       $"GDI: {gdi} (peak: {_peakGdiHandles}) | " +
                       $"User: {user} (peak: {_peakUserHandles}) | " +
                       $"Threads: {threads} | " +
                       $"Uptime: {uptime:hh\\:mm\\:ss}";
            }
            catch
            {
                return "Health data unavailable";
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetGuiResources(IntPtr hProcess, int uiFlags);
    }
}
