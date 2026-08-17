using System;
using System.IO;

namespace EarTrumpet.Logic
{
    /// <summary>
    /// Portable app/device matching used by the CLI, typed app-rule names, and the Linux self-test.
    /// Live session ExeName is the file name without extension.
    /// </summary>
    public static class AppIdentity
    {
        public static string NormalizeExeName(string typed)
        {
            var value = (typed ?? "").Trim().Trim('"');
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            try
            {
                var slashNormalized = value.Replace('\\', '/');
                var withoutExtension = Path.GetFileNameWithoutExtension(slashNormalized);
                return string.IsNullOrWhiteSpace(withoutExtension) ? value : withoutExtension;
            }
            catch (ArgumentException)
            {
                return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? value.Substring(0, value.Length - 4)
                    : value;
            }
        }

        public static string NormalizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            return Path.GetFileNameWithoutExtension(value.Trim().Replace('\\', '/')).ToLowerInvariant();
        }

        public static bool MatchesExact(string exeName, string displayName, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            exeName = exeName ?? "";
            var exeNoExt = NormalizeExeName(exeName);
            return string.Equals(exeName, query, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(exeNoExt, query, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(displayName, query, StringComparison.OrdinalIgnoreCase);
        }

        public static bool MatchesPartial(string exeName, string displayName, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            exeName = exeName ?? "";
            displayName = displayName ?? "";
            return exeName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool MatchesApp(string exeName, string displayName, string query)
        {
            return MatchesExact(exeName, displayName, query) || MatchesPartial(exeName, displayName, query);
        }

        public static bool MatchesDevice(string displayName, string query)
        {
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            return string.Equals(displayName, query, StringComparison.OrdinalIgnoreCase) ||
                   displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static int Score(string exeName, string displayName, string appId, string query)
        {
            var normalizedQuery = NormalizeToken(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return 0;
            }

            var best = 0;
            ScoreCandidate(exeName, normalizedQuery, ref best);
            ScoreCandidate(NormalizeExeName(exeName), normalizedQuery, ref best);
            ScoreCandidate(displayName, normalizedQuery, ref best);
            ScoreCandidate(appId, normalizedQuery, ref best);
            return best;
        }

        private static void ScoreCandidate(string value, string normalizedQuery, ref int best)
        {
            var token = NormalizeToken(value);
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            int score;
            if (string.Equals(token, normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                score = 100;
            }
            else if (token.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                score = 80;
            }
            else if (token.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score = 60;
            }
            else
            {
                score = 0;
            }

            if (score > best)
            {
                best = score;
            }
        }
    }
}
