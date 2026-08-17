using EarTrumpet.DataModel.Audio;
using EarTrumpet.DataModel.WindowsAudio;
using EarTrumpet.DataModel.WindowsAudio.Internal;
using EarTrumpet.Extensions;
using EarTrumpet.Interop.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace EarTrumpet.Diagnosis
{
    public class LocalDataExporter
    {
        private static readonly string LineText = "--------------------------------------------------------------------";

        public static void DumpAndShowData(string logText)
        {
            var fileName = $"{Path.GetTempFileName()}.txt";
            File.WriteAllText(fileName, PathSanitizer.Sanitize(BuildDiagnosticText(logText, null, "manual-text-export", includeLiveSnapshot: true)));
            ProcessHelper.StartNoThrow(fileName);
        }

        public static string CreateSupportBundle(string logText, string logDirectory, Exception exception = null, string reason = null, bool includeLiveSnapshot = true)
        {
            var diagnosticsDir = GetDiagnosticsDirectory(logDirectory);
            Directory.CreateDirectory(diagnosticsDir);
            CleanupOldBundles(diagnosticsDir);

            var fileName = $"BetterTrumpet-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss-fff}.zip";
            var zipPath = Path.Combine(diagnosticsDir, fileName);

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                WriteBundleEntries((name, text) => WriteTextEntry(archive, name, text), logText, logDirectory, exception, reason, includeLiveSnapshot);
            }

            return zipPath;
        }

        public static string CreateStagingFolder(string logText, string logDirectory, Exception exception = null, string reason = null, bool includeLiveSnapshot = true)
        {
            var diagnosticsDir = GetDiagnosticsDirectory(logDirectory);
            Directory.CreateDirectory(diagnosticsDir);
            CleanupOldBundles(diagnosticsDir);

            var stagingDir = Path.Combine(diagnosticsDir, $"BetterTrumpet-diagnostics-staging-{DateTime.Now:yyyyMMdd-HHmmss-fff}");
            Directory.CreateDirectory(stagingDir);

            WriteBundleEntries((name, text) =>
            {
                var path = Path.Combine(stagingDir, name.Replace('/', Path.DirectorySeparatorChar));
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(path, text ?? "", Encoding.UTF8);
            }, logText, logDirectory, exception, reason, includeLiveSnapshot);

            return stagingDir;
        }

        public static string ZipDirectory(string stagingDir)
        {
            if (string.IsNullOrWhiteSpace(stagingDir) || !Directory.Exists(stagingDir))
            {
                throw new DirectoryNotFoundException(stagingDir);
            }

            var zipName = Path.GetFileName(stagingDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            zipName = zipName.Replace("-staging-", "-") + ".zip";
            var zipPath = Path.Combine(Path.GetDirectoryName(stagingDir), zipName);
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(stagingDir, zipPath, CompressionLevel.Fastest, includeBaseDirectory: false);

            try
            {
                Directory.Delete(stagingDir, true);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"LocalDataExporter ZipDirectory cleanup failed: {ex.Message}");
            }

            return zipPath;
        }

        public static void ShowInExplorer(string filePath)
        {
            try
            {
                if (Directory.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{filePath}\"")
                    {
                        UseShellExecute = true
                    });
                    return;
                }

                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"LocalDataExporter ShowInExplorer failed: {ex.Message}");
                ProcessHelper.StartNoThrow(Directory.Exists(filePath) ? filePath : Path.GetDirectoryName(filePath));
            }
        }

        private static void WriteBundleEntries(Action<string, string> writeText, string logText, string logDirectory, Exception exception, string reason, bool includeLiveSnapshot)
        {
            void Write(string name, string text) => writeText(name, PathSanitizer.Sanitize(text ?? ""));

            var addErrors = new StringBuilder();

            Write("README.txt",
                "BetterTrumpet diagnostic bundle\r\n\r\n" +
                "Attach this zip file when reporting an issue.\r\n" +
                "It can contain app names, device names, process IDs, Windows audio endpoint IDs, settings state, and recent BetterTrumpet logs.\r\n" +
                "User folder paths are replaced with %USERPROFILE% / %APPDATA% / %LOCALAPPDATA% / %TEMP%.\r\n" +
                "Review it before sharing if your audio setup contains sensitive names.\r\n");

            Write("diagnostic-summary.txt", BuildDiagnosticText(logText, exception, reason, includeLiveSnapshot));

            if (exception != null)
            {
                Write("exception.txt", exception.ToString());
            }

            AddLogFiles(Write, logDirectory, addErrors);

            if (addErrors.Length > 0)
            {
                Write("log-copy-errors.txt", addErrors.ToString());
            }
        }

        private static string BuildDiagnosticText(string logText, Exception exception, string reason, bool includeLiveSnapshot)
        {
            var ret = new StringBuilder();
            ret.AppendLine("BetterTrumpet diagnostic snapshot");
            ret.AppendLine(LineText);
            ret.AppendLine($"createdAt: {DateTimeOffset.Now:O}");
            ret.AppendLine($"reason: {reason ?? "manual"}");

            if (exception != null)
            {
                ret.AppendLine($"exceptionType: {exception.GetType().FullName}");
                ret.AppendLine($"exceptionMessage: {exception.Message}");
            }

            ret.AppendLine(LineText);

            SafeSection(ret, "App", () => Populate(ret, SnapshotData.App));
            SafeSection(ret, "Device", () => Populate(ret, SnapshotData.Device));
            SafeSection(ret, "AppSettings", () => Populate(ret, SnapshotData.AppSettings));
            SafeSection(ret, "LocalOnly", () => Populate(ret, SnapshotData.LocalOnly));

            if (includeLiveSnapshot)
            {
                SafeSection(ret, "Windows audio snapshot", () => DumpDeviceManager(ret, WindowsAudioFactory.Create(AudioDeviceKind.Playback)));
            }
            else
            {
                ret.AppendLine("Windows audio snapshot: skipped");
                ret.AppendLine(LineText);
            }

            ret.AppendLine("Recent in-memory trace");
            ret.AppendLine(LineText);
            ret.AppendLine(string.IsNullOrWhiteSpace(logText) ? "(empty)" : logText);

            return ret.ToString();
        }

        private static void SafeSection(StringBuilder builder, string title, Action action)
        {
            builder.AppendLine(title);
            builder.AppendLine(LineText);

            try
            {
                action();
            }
            catch (Exception ex)
            {
                builder.AppendLine($"Section failed: {ex}");
            }

            builder.AppendLine(LineText);
        }

        private static void WriteTextEntry(ZipArchive archive, string entryName, string text)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
            using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
            {
                writer.Write(PathSanitizer.Sanitize(text ?? ""));
            }
        }

        private static void AddLogFiles(Action<string, string> writeText, string logDirectory, StringBuilder addErrors)
        {
            if (string.IsNullOrWhiteSpace(logDirectory) || !Directory.Exists(logDirectory))
            {
                addErrors.AppendLine($"Log directory not found: {PathSanitizer.Sanitize(logDirectory ?? "(null)")}");
                return;
            }

            foreach (var file in Directory.GetFiles(logDirectory, "*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(10))
            {
                try
                {
                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                    {
                        writeText($"logs/{Path.GetFileName(file)}", reader.ReadToEnd());
                    }
                }
                catch (Exception ex)
                {
                    addErrors.AppendLine($"{PathSanitizer.Sanitize(file)}: {ex.Message}");
                }
            }
        }

        private static string GetDiagnosticsDirectory(string logDirectory)
        {
            var root = !string.IsNullOrWhiteSpace(logDirectory)
                ? Path.GetDirectoryName(logDirectory)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterTrumpet");

            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(Path.GetTempPath(), "BetterTrumpet");
            }

            return Path.Combine(root, "diagnostics");
        }

        private static void CleanupOldBundles(string diagnosticsDir)
        {
            try
            {
                foreach (var file in Directory.GetFiles(diagnosticsDir, "BetterTrumpet-diagnostics-*.zip")
                    .OrderByDescending(File.GetCreationTimeUtc)
                    .Skip(10))
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"LocalDataExporter CleanupOldBundles failed: {ex.Message}");
            }
        }

        private static void Populate(StringBuilder builder, Dictionary<string, Func<object>> source)
        {
            foreach (var key in source.Keys)
            {
                builder.AppendLine($"{key}: {SnapshotData.InvokeNoThrow(source[key])}");
            }
        }

        private static void DumpDeviceManager(StringBuilder builder, IAudioDeviceManager manager)
        {
            foreach (var device in manager.Devices)
            {
                DumpDevice(builder, device);
            }
        }

        private static void DumpDevice(StringBuilder builder, IAudioDevice device)
        {
            builder.Append(device == device.Parent.Default ? $"(Default Device) " : "");
            builder.AppendLine($"{device.DisplayName} {device.Volume.ToVolumeInt()}%{(device.IsMuted ? " (Muted)" : "")} Id: {device.Id}");

            foreach (AudioDeviceSessionGroup appGroup in device.Groups)
            {
                builder.AppendLine(LineText);
                foreach (AudioDeviceSessionGroup appSession in appGroup.Children)
                {
                    foreach (IAudioDeviceSession rawSession in appSession.Children)
                    {
                        DumpSession(builder,
                            appSession.Children.Count == 1 ? "  " : "| ", 
                            (IAudioDeviceSessionInternal)rawSession);
                    }
                }
            }
            builder.AppendLine(LineText);
        }

        private static void DumpSession(StringBuilder builder, string indent, IAudioDeviceSessionInternal session)
        {
            var typeText = session.IsSystemSoundsSession ? "SystemSounds" : (session.IsDesktopApp ? "Desktop" : "Modern");

            builder.AppendLine(indent + $"{session.DisplayName}");
            builder.AppendLine(indent + $"  ({typeText}) ({session.State}) {session.Volume.ToVolumeInt()}%{(session.IsMuted ? " (Muted)" : "")} Id: {session.Id}");
            builder.AppendLine(indent + $"  AppId: {session.AppId} ProcessId: {session.ProcessId} Alive: {IsProcessAlive(session.ProcessId)}");
            builder.AppendLine(indent + $"  IconPath: {session.IconPath}");
            builder.AppendLine(indent + $"  GroupingParam: {session.GroupingParam}");

            var persisted = ((IAudioDeviceManagerWindowsAudio)session.Parent.Parent).GetDefaultEndPoint(session.ProcessId);
            if (!string.IsNullOrWhiteSpace(persisted))
            {
                builder.AppendLine(indent + $"  Persisted Endpoint Id: {persisted}");
            }
        }

        private static bool IsProcessAlive(int processId)
        {
            bool isAlive = false;
            try
            {
                using (Process.GetProcessById(processId))
                {
                }
                isAlive = true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"IsProcessAlive: Process {processId} check failed: {ex.Message}");
            }
            return isAlive;
        }
    }
}
