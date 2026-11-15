using EarTrumpet.Logic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace EarTrumpet.DataModel.Storage
{
    /// <summary>
    /// Uses the existing portable/registry/MSIX backend without coupling the policy to it.
    /// Values are cached in memory and written behind a debounce: dragging a slider produces
    /// dozens of changes per second, and the portable backend rewrites its whole file per Set.
    /// </summary>
    public sealed class SettingsAppVolumeStore : IAppVolumeStore, IDisposable
    {
        public static readonly TimeSpan DefaultWriteDelay = TimeSpan.FromSeconds(1);

        private const string KeyPrefix = "AppVolumeMemory:";

        private readonly ISettingsBag _settings;
        private readonly TimeSpan _writeDelay;
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly object _sync = new object();
        // Serializes flushes so a late timer flush can't overwrite a newer value written on shutdown.
        private readonly object _flushSync = new object();
        private readonly Timer _flushTimer;

        public SettingsAppVolumeStore(ISettingsBag settings) : this(settings, DefaultWriteDelay) { }

        public SettingsAppVolumeStore(ISettingsBag settings, TimeSpan writeDelay)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _writeDelay = writeDelay;
            _flushTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public bool TryRead(string appKey, out float volume)
        {
            lock (_sync)
            {
                var current = GetEntry(appKey).Current;
                volume = current.GetValueOrDefault();
                return current.HasValue;
            }
        }

        public void Write(string appKey, float volume)
        {
            lock (_sync)
            {
                var entry = GetEntry(appKey);
                if (entry.Current == volume)
                {
                    return;
                }

                entry.Current = volume;
            }

            // Debounce: persist once the value has settled.
            _flushTimer.Change(_writeDelay, Timeout.InfiniteTimeSpan);
        }

        public void Flush()
        {
            lock (_flushSync)
            {
                List<KeyValuePair<Entry, float>> dirty = null;
                lock (_sync)
                {
                    foreach (var entry in _entries.Values)
                    {
                        if (entry.Current.HasValue && entry.Current != entry.Persisted)
                        {
                            (dirty ??= new List<KeyValuePair<Entry, float>>())
                                .Add(new KeyValuePair<Entry, float>(entry, entry.Current.Value));
                        }
                    }
                }

                if (dirty == null)
                {
                    return;
                }

                // Backend I/O happens outside _sync so audio and UI threads never wait on the disk.
                foreach (var item in dirty)
                {
                    try
                    {
                        _settings.Set(item.Key.StorageKey, item.Value.ToString("R", CultureInfo.InvariantCulture));
                    }
                    catch (Exception ex)
                    {
                        // A read-only portable folder or unavailable settings must not break audio control.
                        Trace.WriteLine($"SettingsAppVolumeStore write failed: {ex.Message}");
                    }

                    // Marked persisted even on failure: retrying a broken backend on every flush is pointless,
                    // and the in-memory value still serves this process.
                    lock (_sync)
                    {
                        item.Key.Persisted = item.Value;
                    }
                }
            }
        }

        public void Dispose()
        {
            _flushTimer.Dispose();
            Flush();
        }

        // Caller holds _sync.
        private Entry GetEntry(string appKey)
        {
            if (!_entries.TryGetValue(appKey, out var entry))
            {
                entry = new Entry(GetStorageKey(appKey));
                entry.Persisted = entry.Current = Load(entry.StorageKey);
                _entries.Add(appKey, entry);
            }

            return entry;
        }

        private float? Load(string storageKey)
        {
            try
            {
                return float.TryParse(_settings.Get(storageKey, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var volume)
                    ? volume
                    : (float?)null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SettingsAppVolumeStore read failed: {ex.Message}");
                return null;
            }
        }

        private static string GetStorageKey(string appKey)
        {
            // Bounded key length for every backend; don't expose executable paths in key names.
            return KeyPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(appKey)));
        }

        private sealed class Entry
        {
            public Entry(string storageKey) => StorageKey = storageKey;

            public string StorageKey { get; }
            public float? Persisted { get; set; }
            public float? Current { get; set; }
        }
    }
}
