using EarTrumpet.DataModel.Storage;
using EarTrumpet.DataModel.Storage.Internal;
using EarTrumpet.Logic;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

internal static class Program
{
    private static int _passed;

    private static void Main()
    {
        Test("stable desktop identity across sessions and PID changes", () =>
        {
            Equal(Desktop(" C:/Riot/League of Legends/Game/League of Legends.exe "),
                Desktop(@"c:\riot\league of legends\game\league of legends.exe"));
            Check(Desktop(@"c:\one\game.exe") != Desktop(@"c:\two\game.exe"));
            Check(Desktop(@"c:\riot\LeagueClient.exe") != Desktop(@"c:\riot\Game\League of Legends.exe"));
        });
        Test("packaged identity survives package version changes", () =>
        {
            Equal(AppVolumeMemory.CreateKey(false, "package_v1", "family!app", "package_v1"),
                AppVolumeMemory.CreateKey(false, "package_v2", "FAMILY!APP", "package_v2"));
        });
        Test("missing identity does not share a global preference", () =>
        {
            Equal<string>(null, AppVolumeMemory.CreateKey(true, null, null, " "));
            var store = new MemorySettings();
            var memory = Memory(store);
            memory.Remember(null, 0.4f);
            Check(!memory.TryRecall(null, out _));
            Equal(0, store.Writes);
        });
        Test("new app has no preference; zero and full volume are valid", () =>
        {
            var memory = Memory(new MemorySettings());
            Check(!memory.TryRecall("new", out _));
            foreach (var value in new[] { 0f, 0.23f, 1f })
            {
                memory.Remember("app", value);
                Check(memory.TryRecall("app", out var recalled));
                Equal(value, recalled);
            }
        });
        Test("invalid writes and corrupt stored scalars are ignored", () =>
        {
            var store = new MemorySettings();
            var memory = Memory(store);
            memory.Remember("app", 0.23f);
            foreach (var value in new[] { -1f, 2f, float.NaN, float.PositiveInfinity })
            {
                memory.Remember("app", value);
            }
            Equal(1, store.Writes);
            foreach (var value in new[] { "garbage", "NaN", "Infinity", "-0.1", "1.1" })
            {
                store.ReplaceValues(value);
                Check(!memory.TryRecall("app", out _));
            }
        });
        Test("duplicate writes don't repeatedly flush settings", () =>
        {
            var store = new MemorySettings();
            var memory = Memory(store);
            memory.Remember("app", 0.3f);
            memory.Remember("app", 0.3f);
            Equal(1, store.Writes);
            Equal(64 + "AppVolumeMemory:".Length, store.LastKey.Length);
        });
        Test("unavailable storage cannot break volume control", () =>
        {
            var memory = Memory(new MemorySettings { Fail = true });
            memory.Remember("app", 0.2f);
            Check(!memory.TryRecall("app", out _));
        });
        Test("portable restart recalls the last adjustment without culture/slider-scale loss", () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "BetterTrumpet-tests-" + Guid.NewGuid());
            var path = Path.Combine(directory, "settings.json");
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
                var key = Desktop(@"C:\Riot\Game\League of Legends.exe");
                var memory = Memory(new JsonFileSettingsBag(path));
                // Store the real Windows scalar (including logarithmic-scale values).
                memory.Remember(key, 0.00012345678f);
                // A fresh backend + service represents a BetterTrumpet restart or a new match.
                var restarted = Memory(new JsonFileSettingsBag(path));
                Check(restarted.TryRecall(key, out var recalled));
                Equal(0.00012345678f, recalled);
                restarted.Remember(key, 0.42f);
                Check(Memory(new JsonFileSettingsBag(path)).TryRecall(key, out recalled));
                Equal(0.42f, recalled);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        });
        Console.WriteLine(_passed + " tests passed.");
    }

    private static string Desktop(string path) => AppVolumeMemory.CreateKey(true, path, null, "game");
    private static AppVolumeMemory Memory(ISettingsBag settings) => new AppVolumeMemory(new SettingsAppVolumeStore(settings));
    private static void Check(bool condition)
    {
        if (!condition) throw new Exception("Assertion failed");
    }
    private static void Equal<T>(T expected, T actual) => Check(EqualityComparer<T>.Default.Equals(expected, actual));
    private static void Test(string name, Action action)
    {
        action();
        _passed++;
        Console.WriteLine("PASS " + name);
    }

    private sealed class MemorySettings : ISettingsBag
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();
        public string Namespace => "";
        public int Writes { get; private set; }
        public string LastKey { get; private set; }
        public bool Fail { get; set; }
        public event EventHandler<string> SettingChanged;
        public bool HasKey(string key) => _data.ContainsKey(key);
        public T Get<T>(string key, T defaultValue)
        {
            if (Fail) throw new IOException("Unavailable settings");
            return _data.TryGetValue(key, out var value) ? (T)value : defaultValue;
        }
        public void Set<T>(string key, T value)
        {
            if (Fail) throw new IOException("Read-only settings");
            Writes++;
            LastKey = key;
            _data[key] = value;
            SettingChanged?.Invoke(this, key);
        }
        public void ReplaceValues(string value)
        {
            foreach (var key in new List<string>(_data.Keys)) _data[key] = value;
        }
    }
}
