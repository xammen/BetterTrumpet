using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace EarTrumpet.DataModel
{
    /// <summary>
    /// Applies a portable update without a script. The running app extracts the new release to
    /// %TEMP%, starts that new BetterTrumpet.exe with <see cref="Verb"/>, and exits; the new exe
    /// waits for it, mirrors its own folder over the portable folder (keeping ./config), and
    /// relaunches the updated app. Runs before any app initialization, so it logs to a file.
    /// </summary>
    internal static class PortableUpdateApplier
    {
        public const string Verb = "--apply-portable-update";
        private const string ExeName = "BetterTrumpet.exe";
        private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "BetterTrumpet-update.log");

        public static ProcessStartInfo CreateStartInfo(string newExePath, string targetDir)
        {
            var info = new ProcessStartInfo(newExePath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(newExePath),
            };
            info.ArgumentList.Add(Verb);
            info.ArgumentList.Add(Environment.ProcessId.ToString());
            info.ArgumentList.Add(targetDir);
            return info;
        }

        /// <returns>True if the arguments asked for an update, whether or not it succeeded.</returns>
        public static bool TryRun(string[] args)
        {
            if (args == null || args.Length < 3 || args[0] != Verb)
            {
                return false;
            }

            try
            {
                Run(int.Parse(args[1]), args[2]);
            }
            catch (Exception ex)
            {
                Log($"failed: {ex}");
                RelaunchExisting(args[2]);
            }

            return true;
        }

        /// <summary>Deletes what a previous update left in %TEMP% (the applier cannot delete its own folder).</summary>
        public static void CleanupLeftovers()
        {
            try
            {
                var temp = Path.GetTempPath();
                foreach (var dir in Directory.GetDirectories(temp, "BetterTrumpet-*-update"))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }

                foreach (var zip in Directory.GetFiles(temp, "BetterTrumpet-*-portable.zip"))
                {
                    try { File.Delete(zip); } catch { }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"PortableUpdateApplier CleanupLeftovers failed: {ex.Message}");
            }
        }

        private static void Run(int oldProcessId, string targetDir)
        {
            var sourceDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            targetDir = Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar);
            Log($"applying {sourceDir} -> {targetDir}, waiting for pid {oldProcessId}");

            // Only ever touch a folder that is a portable BetterTrumpet install.
            if (string.Equals(sourceDir, targetDir, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(Path.Combine(targetDir, ExeName)) ||
                !(File.Exists(Path.Combine(targetDir, "portable.marker")) || File.Exists(Path.Combine(targetDir, ".portable"))))
            {
                throw new InvalidOperationException("target is not a portable BetterTrumpet folder");
            }

            WaitForExit(oldProcessId);

            var configDir = Path.Combine(targetDir, "config");
            var newFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var source in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, source);
                newFiles.Add(relative);
                var target = Path.Combine(targetDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                Retry(() => File.Copy(source, target, overwrite: true));
            }

            // Prune files the new release no longer ships, so no stale DLL is loaded. User data lives in ./config.
            foreach (var existing in Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories))
            {
                if (existing.StartsWith(configDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(targetDir, existing);
                if (!newFiles.Contains(relative) &&
                    !relative.Equals("portable.marker", StringComparison.OrdinalIgnoreCase) &&
                    !relative.Equals(".portable", StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(existing); } catch (Exception ex) { Log($"could not delete {relative}: {ex.Message}"); }
                }
            }

            Log("files replaced, relaunching");
            Process.Start(new ProcessStartInfo(Path.Combine(targetDir, ExeName))
            {
                UseShellExecute = false,
                WorkingDirectory = targetDir,
            });
        }

        // A failed update must not leave the user without the app.
        private static void RelaunchExisting(string targetDir)
        {
            try
            {
                var exe = Path.Combine(targetDir, ExeName);
                if (File.Exists(exe))
                {
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = targetDir });
                }
            }
            catch (Exception ex)
            {
                Log($"relaunch failed: {ex.Message}");
            }
        }

        private static void WaitForExit(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (!process.WaitForExit(60000))
                {
                    Log("old instance still running after 60 s, killing it");
                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
            catch (ArgumentException)
            {
                // Already gone.
            }

            // Let antivirus and the loader release the old files.
            Thread.Sleep(500);
        }

        private static void Retry(Action action)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (IOException) when (attempt < 10)
                {
                    Thread.Sleep(500);
                }
                catch (UnauthorizedAccessException) when (attempt < 10)
                {
                    Thread.Sleep(500);
                }
            }
        }

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }
    }
}
