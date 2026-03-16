using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace EarTrumpet.DataModel.Storage.Internal
{
    /// <summary>
    /// Settings backend for Portable mode.
    /// Stores all settings in a JSON file next to the executable.
    /// Zero registry writes — can run from a USB stick.
    /// </summary>
    class JsonFileSettingsBag : ISettingsBag
    {
        private readonly string _filePath;
        private Dictionary<string, object> _data;
        private readonly object _lock = new object();

        public string Namespace => "";

        public event EventHandler<string> SettingChanged;

        public JsonFileSettingsBag(string filePath)
        {
            _filePath = filePath;
            _data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Load();
        }

        public bool HasKey(string key)
        {
            lock (_lock)
            {
                return _data.ContainsKey(key);
            }
        }

        public T Get<T>(string key, T defaultValue)
        {
            lock (_lock)
            {
                if (!_data.ContainsKey(key))
                    return defaultValue;

                try
                {
                    var raw = _data[key];

                    // Direct type match
                    if (raw is T typed)
                        return typed;

                    // Handle Newtonsoft deserializing numbers as long/double
                    if (typeof(T) == typeof(int) && raw is long longVal)
                        return (T)(object)(int)longVal;
                    if (typeof(T) == typeof(float) && raw is double doubleVal)
                        return (T)(object)(float)doubleVal;
                    if (typeof(T) == typeof(bool) && raw is bool boolVal)
                        return (T)(object)boolVal;

                    // String conversion for complex types (match Registry/XML behavior)
                    if (raw is string strVal)
                    {
                        if (typeof(T) == typeof(string))
                            return (T)(object)strVal;

                        return Serializer.FromString<T>(strVal);
                    }

                    // Last resort: serialize then deserialize via JSON
                    var json = JsonConvert.SerializeObject(raw);
                    return JsonConvert.DeserializeObject<T>(json);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"JsonFileSettingsBag: Failed to read '{key}': {ex.Message}");
                    return defaultValue;
                }
            }
        }

        public void Set<T>(string key, T value)
        {
            lock (_lock)
            {
                if (value is string strVal)
                {
                    _data[key] = strVal;
                }
                else
                {
                    // Store complex types as XML string (compat with Registry/WindowsStorage backends)
                    _data[key] = Serializer.ToString(key, value);
                }
            }

            Save();
            SettingChanged?.Invoke(this, key);
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    _data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json)
                            ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"JsonFileSettingsBag: Failed to load '{_filePath}': {ex.Message}");
                _data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonConvert.SerializeObject(_data, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"JsonFileSettingsBag: Failed to save '{_filePath}': {ex.Message}");
            }
        }
    }
}
