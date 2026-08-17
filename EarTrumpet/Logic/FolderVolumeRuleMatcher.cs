using System;
using System.Collections.Generic;
using System.IO;

namespace EarTrumpet.Logic
{
    public readonly struct FolderVolumeRule
    {
        public FolderVolumeRule(string folderPath, int volumePercent, DateTime createdAtUtc)
        {
            FolderPath = folderPath;
            VolumePercent = volumePercent;
            CreatedAtUtc = createdAtUtc;
        }

        public string FolderPath { get; }
        public int VolumePercent { get; }
        public DateTime CreatedAtUtc { get; }
    }

    /// <summary>
    /// Deepest matching folder wins. Explicit app rules stay higher priority at the call site.
    /// Windows-style paths are matched logically so the Linux self-test can cover them.
    /// </summary>
    public static class FolderVolumeRuleMatcher
    {
        public static bool TryMatch(string executablePath, IEnumerable<FolderVolumeRule> rules, out int volumePercent)
        {
            volumePercent = 0;
            if (!IsUsableExecutablePath(executablePath) || rules == null)
            {
                return false;
            }

            FolderVolumeRule? matchingRule = null;
            foreach (var entry in rules)
            {
                if (string.IsNullOrWhiteSpace(entry.FolderPath) || !IsExecutableUnderFolder(executablePath, entry.FolderPath))
                {
                    continue;
                }

                if (!matchingRule.HasValue)
                {
                    matchingRule = entry;
                    continue;
                }

                var current = matchingRule.Value;
                var entryLength = NormalizeFolderPath(entry.FolderPath).Length;
                var currentLength = NormalizeFolderPath(current.FolderPath).Length;
                if (entryLength > currentLength ||
                    (entryLength == currentLength && entry.CreatedAtUtc < current.CreatedAtUtc))
                {
                    matchingRule = entry;
                }
            }

            if (!matchingRule.HasValue)
            {
                return false;
            }

            volumePercent = Math.Max(0, Math.Min(100, matchingRule.Value.VolumePercent));
            return true;
        }

        public static bool IsUsableExecutablePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (IsWindowsPath(path) || IsUncPath(path))
            {
                return true;
            }

            try
            {
                return Path.IsPathFullyQualified(path);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static bool IsExecutableUnderFolder(string executablePath, string folderPath)
        {
            var folder = NormalizeFolderPath(folderPath);
            var executable = NormalizeExecutablePath(executablePath);
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(executable))
            {
                return false;
            }

            var separator = UsesWindowsSeparators(folder) || UsesWindowsSeparators(executable) ? '\\' : '/';
            var prefix = EndsWithSeparator(folder) ? folder : folder + separator;
            if (executable.Equals(folder, StringComparison.OrdinalIgnoreCase) ||
                executable.Equals(TrimSeparators(folder), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (executable.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var altSeparator = separator == '\\' ? '/' : '\\';
            var altPrefix = prefix.Replace(separator, altSeparator);
            return executable.StartsWith(altPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeFolderPath(string folder)
        {
            var trimmed = TrimPath(folder);
            if (string.IsNullOrEmpty(trimmed))
            {
                return "";
            }

            if (IsWindowsPath(trimmed) || IsUncPath(trimmed))
            {
                return TrimSeparators(NormalizeSlashes(trimmed, '\\'));
            }

            try
            {
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                return "";
            }
        }

        public static string NormalizeExecutablePath(string executablePath)
        {
            var trimmed = TrimPath(executablePath);
            if (string.IsNullOrEmpty(trimmed))
            {
                return "";
            }

            if (IsWindowsPath(trimmed) || IsUncPath(trimmed))
            {
                return NormalizeSlashes(trimmed, '\\');
            }

            try
            {
                return Path.GetFullPath(trimmed);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                return "";
            }
        }

        private static string TrimPath(string path)
        {
            return (path ?? "").Trim().Trim('"');
        }

        private static bool IsWindowsPath(string path)
        {
            return path.Length >= 3 &&
                   char.IsLetter(path[0]) &&
                   path[1] == ':' &&
                   (path[2] == '\\' || path[2] == '/');
        }

        private static bool IsUncPath(string path)
        {
            return path.StartsWith(@"\\", StringComparison.Ordinal) ||
                   path.StartsWith("//", StringComparison.Ordinal);
        }

        private static bool UsesWindowsSeparators(string path)
        {
            return path.IndexOf('\\') >= 0;
        }

        private static bool EndsWithSeparator(string path)
        {
            return path.EndsWith("\\", StringComparison.Ordinal) || path.EndsWith("/", StringComparison.Ordinal);
        }

        private static string TrimSeparators(string path)
        {
            return path.TrimEnd('\\', '/');
        }

        private static string NormalizeSlashes(string path, char separator)
        {
            var other = separator == '\\' ? '/' : '\\';
            return path.Replace(other, separator);
        }
    }
}
