using EarTrumpet.Logic;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EarTrumpet.DataModel.Storage
{
    /// <summary>Uses the existing portable/registry/MSIX backend without coupling the policy to it.</summary>
    public sealed class SettingsAppVolumeStore : IAppVolumeStore
    {
        private readonly ISettingsBag _settings;
        private readonly object _sync = new object();

        public SettingsAppVolumeStore(ISettingsBag settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public bool TryRead(string appKey, out float volume)
        {
            volume = 0;
            try
            {
                lock (_sync)
                {
                    return float.TryParse(_settings.Get(GetStorageKey(appKey), ""),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out volume);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SettingsAppVolumeStore read failed: {ex.Message}");
                return false;
            }
        }

        public void Write(string appKey, float volume)
        {
            try
            {
                lock (_sync)
                {
                    var key = GetStorageKey(appKey);
                    var value = volume.ToString("R", CultureInfo.InvariantCulture);
                    if (_settings.Get(key, "") != value)
                    {
                        _settings.Set(key, value);
                    }
                }
            }
            catch (Exception ex)
            {
                // A read-only portable folder or unavailable settings must not break audio control.
                Trace.WriteLine($"SettingsAppVolumeStore write failed: {ex.Message}");
            }
        }

        private static string GetStorageKey(string appKey)
        {
            // Bounded key length for every backend; don't expose executable paths in key names.
            using (var sha = SHA256.Create())
            {
                return "AppVolumeMemory:" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(appKey))).Replace("-", "");
            }
        }
    }
}
