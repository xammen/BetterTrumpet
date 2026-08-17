using System;

namespace EarTrumpet.Logic
{
    /// <summary>
    /// Tray toast for GitHub #36. The first observed default device is remembered
    /// silently so startup does not spam a notification.
    /// </summary>
    public static class DefaultDeviceChangePolicy
    {
        public static bool ShouldNotify(string previousDeviceId, string newDeviceId, bool enabled)
        {
            if (!enabled)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(newDeviceId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(previousDeviceId))
            {
                return false;
            }

            return !string.Equals(previousDeviceId, newDeviceId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
