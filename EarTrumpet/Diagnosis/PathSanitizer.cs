using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace EarTrumpet.Diagnosis
{
    internal static class PathSanitizer
    {
        public static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            text = ReplacePath(text, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%");
            text = ReplacePath(text, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%APPDATA%");
            text = ReplacePath(text, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%");
            text = ReplacePath(text, Path.GetTempPath(), "%TEMP%");

            return Regex.Replace(
                text,
                @"([A-Za-z]:\\Users\\)([^\\\/\s""']+)",
                "$1%USERNAME%",
                RegexOptions.IgnoreCase);
        }

        private static string ReplacePath(string text, string path, string token)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return text;
            }

            path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (path.Length < 4)
            {
                return text;
            }

            return ReplaceIgnoreCase(text, path, token);
        }

        private static string ReplaceIgnoreCase(string text, string oldValue, string newValue)
        {
            var result = new StringBuilder(text.Length);
            var start = 0;
            while (true)
            {
                var index = text.IndexOf(oldValue, start, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    result.Append(text, start, text.Length - start);
                    return result.ToString();
                }

                result.Append(text, start, index - start);
                result.Append(newValue);
                start = index + oldValue.Length;
            }
        }
    }
}
