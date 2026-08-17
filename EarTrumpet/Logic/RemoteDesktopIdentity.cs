using System;

namespace EarTrumpet.Logic
{
    /// <summary>
    /// GitHub #7: mstsc/msrdc sessions get a new process id on reconnect, so a
    /// Launch rule keyed on pid cannot restore the last volume by itself.
    /// </summary>
    public static class RemoteDesktopIdentity
    {
        public static bool IsRemoteDesktopExe(string exeName)
        {
            var token = AppIdentity.NormalizeExeName(exeName);
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            return string.Equals(token, "mstsc", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "msrdc", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "rdpclip", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "wfreerdp", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryGetRememberedVolume(int storedVolume, out int volumePercent)
        {
            if (storedVolume < 0 || storedVolume > 100)
            {
                volumePercent = 0;
                return false;
            }

            volumePercent = storedVolume;
            return true;
        }

        public static int ClampStoredVolume(int volumePercent)
        {
            if (volumePercent < 0)
            {
                return -1;
            }

            if (volumePercent > 100)
            {
                return 100;
            }

            return volumePercent;
        }
    }
}
