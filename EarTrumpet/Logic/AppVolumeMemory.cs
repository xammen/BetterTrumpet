using System;

namespace EarTrumpet.Logic
{
    /// <summary>Persistence port; values are Windows audio scalars, not slider percentages.</summary>
    public interface IAppVolumeStore
    {
        bool TryRead(string appKey, out float volume);
        void Write(string appKey, float volume);
    }

    /// <summary>
    /// Remembers user intent, independently of processes, sessions, endpoints and UI lifetime.
    /// Only deliberate volume writes belong here, never observed OS changes or temporary effects.
    /// </summary>
    public sealed class AppVolumeMemory
    {
        private readonly IAppVolumeStore _store;

        public AppVolumeMemory(IAppVolumeStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public static string CreateKey(bool isDesktopApp, string executablePath, string appUserModelId, string exeName)
        {
            // Full executable paths distinguish unrelated apps with the same filename.
            // Packaged apps use AUMID, which doesn't change when the package version changes.
            var identity = isDesktopApp ? executablePath : appUserModelId;
            var kind = isDesktopApp ? "desktop:" : "packaged:";
            if (string.IsNullOrWhiteSpace(identity))
            {
                identity = exeName;
                kind = "fallback:";
            }

            return string.IsNullOrWhiteSpace(identity)
                ? null
                : kind + identity.Trim().Replace('/', '\\').ToLowerInvariant();
        }

        public void Remember(string appKey, float volume)
        {
            if (!string.IsNullOrWhiteSpace(appKey) && IsValid(volume))
            {
                _store.Write(appKey, volume);
            }
        }

        public bool TryRecall(string appKey, out float volume)
        {
            volume = 0;
            return !string.IsNullOrWhiteSpace(appKey) &&
                   _store.TryRead(appKey, out volume) && IsValid(volume);
        }

        private static bool IsValid(float volume)
        {
            return !float.IsNaN(volume) && !float.IsInfinity(volume) && volume >= 0 && volume <= 1;
        }
    }
}
