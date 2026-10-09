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

        // GetGuiResources flags. The *_PEAK flags return the kernel's own high-water mark, so peaks
        // stay correct between checks (tracking them on the 5-minute timer showed peaks below the
        // live value in the diagnostics view, #76).
        private const int GR_GDIOBJECTS = 0;
        private const int GR_USEROBJECTS = 1;
        private const int GR_GDIOBJECTS_PEAK = 2;
        private const int GR_USEROBJECTS_PEAK = 4;

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
                var peakMemoryMB = process.PeakWorkingSet64 / (1024 * 1024);

                if (memoryMB > MemoryCriticalMB)
                {
                    Trace.WriteLine($"HealthMonitor: CRITICAL — Memory {memoryMB}MB exceeds {MemoryCriticalMB}MB limit (peak: {peakMemoryMB}MB)");
                }
                else if (memoryMB > MemoryWarnMB)
                {
                    Trace.WriteLine($"HealthMonitor: WARN — Memory {memoryMB}MB exceeds {MemoryWarnMB}MB warning (peak: {peakMemoryMB}MB)");
                }

                // GDI handle check (Windows has a per-process limit of ~10000)
                var gdiHandles = GetGuiResources(process.Handle, GR_GDIOBJECTS);
                var peakGdiHandles = GetGuiResources(process.Handle, GR_GDIOBJECTS_PEAK);

                if (gdiHandles > HandleCritical)
                {
                    Trace.WriteLine($"HealthMonitor: CRITICAL — GDI handles {gdiHandles} exceeds {HandleCritical} (peak: {peakGdiHandles})");
                }
                else if (gdiHandles > HandleWarn)
                {
                    Trace.WriteLine($"HealthMonitor: WARN — GDI handles {gdiHandles} exceeds {HandleWarn} (peak: {peakGdiHandles})");
                }

                // User handle check
                var userHandles = GetGuiResources(process.Handle, GR_USEROBJECTS);
                var peakUserHandles = GetGuiResources(process.Handle, GR_USEROBJECTS_PEAK);

                if (userHandles > HandleCritical)
                {
                    Trace.WriteLine($"HealthMonitor: CRITICAL — User handles {userHandles} exceeds {HandleCritical} (peak: {peakUserHandles})");
                }
                else if (userHandles > HandleWarn)
                {
                    Trace.WriteLine($"HealthMonitor: WARN — User handles {userHandles} exceeds {HandleWarn} (peak: {peakUserHandles})");
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
                var gdi = GetGuiResources(process.Handle, GR_GDIOBJECTS);
                var user = GetGuiResources(process.Handle, GR_USEROBJECTS);
                var threads = process.Threads.Count;
                var uptime = DateTime.Now - process.StartTime;

                return $"Memory: {memMB}MB (peak: {process.PeakWorkingSet64 / (1024 * 1024)}MB) | " +
                       $"GDI: {gdi} (peak: {GetGuiResources(process.Handle, GR_GDIOBJECTS_PEAK)}) | " +
                       $"User: {user} (peak: {GetGuiResources(process.Handle, GR_USEROBJECTS_PEAK)}) | " +
                       $"Threads: {threads} | " +
                       $"Uptime: {uptime:d\\.hh\\:mm\\:ss}";
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
