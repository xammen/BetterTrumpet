using System.Diagnostics;
using System.IO;

namespace EarTrumpet.DataModel.Storage
{
    public class StorageFactory
    {
        private static ISettingsBag s_globalSettings;
        private static bool s_isPortable;

        /// <summary>
        /// True if a 'portable.marker' file was detected next to the exe.
        /// In portable mode, settings are stored in ./config/settings.json (zero registry).
        /// </summary>
        public static bool IsPortableMode => s_isPortable;

        static StorageFactory()
        {
            var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var markerPath = Path.Combine(exeDir, "portable.marker");
            s_isPortable = File.Exists(markerPath);

            if (s_isPortable)
            {
                var configDir = Path.Combine(exeDir, "config");
                var settingsPath = Path.Combine(configDir, "settings.json");
                s_globalSettings = new Internal.JsonFileSettingsBag(settingsPath);
                Trace.WriteLine($"StorageFactory: Portable mode — settings at {settingsPath}");
            }
            else
            {
                s_globalSettings = App.HasIdentity
                    ? (ISettingsBag)new Internal.WindowsStorageSettingsBag()
                    : new Internal.RegistrySettingsBag();
            }
        }

        public static ISettingsBag GetSettings(string nameSpace = null)
        {
            return (nameSpace == null) ? s_globalSettings :
                new Internal.NamespacedSettingsBag(nameSpace, s_globalSettings);
        }
    }
}
