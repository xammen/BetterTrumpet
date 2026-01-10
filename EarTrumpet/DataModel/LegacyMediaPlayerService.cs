using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using EarTrumpet.DataModel.Audio;
using EarTrumpet.DataModel.WindowsAudio;

namespace EarTrumpet.DataModel
{
    /// <summary>
    /// Service to detect media players that don't use SMTC (VLC, WMP legacy, etc.)
    /// Uses Windows Audio API to detect active audio sessions from known players.
    /// </summary>
    public class LegacyMediaPlayerService : IDisposable
    {
        // Known media player process names that typically don't use SMTC
        private static readonly HashSet<string> KnownLegacyPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "vlc",           // VLC Media Player
            "wmplayer",      // Windows Media Player legacy
            "mpc-hc",        // Media Player Classic - Home Cinema
            "mpc-hc64",
            "mpc-be",        // Media Player Classic - Black Edition  
            "mpc-be64",
            "potplayer",     // PotPlayer
            "potplayer64",
            "potplayermini",
            "potplayermini64",
            "foobar2000",    // foobar2000
            "winamp",        // Winamp
            "aimp",          // AIMP
            "musicbee",      // MusicBee
            "mediamonkey",   // MediaMonkey
            "kmplayer",      // KMPlayer
            "gom",           // GOM Player
            "smplayer",      // SMPlayer
            "mpv",           // mpv
            "clementine",    // Clementine
            "audacity",      // Audacity
        };

        private static LegacyMediaPlayerService _instance;
        private static readonly object _lock = new object();

        public static LegacyMediaPlayerService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LegacyMediaPlayerService();
                        }
                    }
                }
                return _instance;
            }
        }

        private readonly DispatcherTimer _pollTimer;
        private IAudioDeviceManager _playbackDeviceManager;
        private bool _isPlaying;
        private string _currentPlayerExeName;
        private string _currentPlayerDisplayName;
        private bool _disposed;
        private bool _isInitialized;

        public event Action<bool> PlaybackChanged;
        public event Action TrackChanged;

        public bool IsPlaying => _isPlaying;
        public string CurrentPlayerName => _currentPlayerExeName;
        public string CurrentPlayerDisplayName => _currentPlayerDisplayName;

        private LegacyMediaPlayerService()
        {
            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _pollTimer.Tick += PollTimer_Tick;

            // Delay initialization to let EarTrumpet's audio system start first
            var initTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            initTimer.Tick += (s, e) =>
            {
                initTimer.Stop();
                Initialize();
            };
            initTimer.Start();

            Trace.WriteLine("LegacyMediaPlayerService: Created, waiting for init...");
        }

        private void Initialize()
        {
            try
            {
                _playbackDeviceManager = WindowsAudioFactory.Create(AudioDeviceKind.Playback);
                _isInitialized = true;
                _pollTimer.Start();
                Trace.WriteLine("LegacyMediaPlayerService: Initialized and polling started");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"LegacyMediaPlayerService: Init failed - {ex.Message}");
            }
        }

        private void PollTimer_Tick(object sender, EventArgs e)
        {
            if (!_isInitialized || _playbackDeviceManager == null)
                return;

            try
            {
                bool wasPlaying = _isPlaying;
                string previousPlayer = _currentPlayerExeName;

                // Find active legacy media player
                FindActiveLegacyPlayer(out _isPlaying, out _currentPlayerExeName, out _currentPlayerDisplayName);

                // Playback state changed
                if (wasPlaying != _isPlaying)
                {
                    Trace.WriteLine($"LegacyMediaPlayerService: State changed - IsPlaying={_isPlaying}, Player={_currentPlayerExeName}");
                    PlaybackChanged?.Invoke(_isPlaying);
                }

                // Player changed while playing
                if (_isPlaying && previousPlayer != _currentPlayerExeName && previousPlayer != null)
                {
                    Trace.WriteLine($"LegacyMediaPlayerService: Player changed from {previousPlayer} to {_currentPlayerExeName}");
                    TrackChanged?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"LegacyMediaPlayerService: Poll error - {ex.Message}");
            }
        }

        private void FindActiveLegacyPlayer(out bool isPlaying, out string exeName, out string displayName)
        {
            isPlaying = false;
            exeName = null;
            displayName = null;

            try
            {
                var devices = _playbackDeviceManager?.Devices;
                if (devices == null) return;

                foreach (var device in devices.ToArray())
                {
                    var groups = device.Groups;
                    if (groups == null) continue;

                    foreach (var group in groups.ToArray())
                    {
                        // Only check ACTIVE sessions (actually playing audio)
                        if (group.State != SessionState.Active)
                            continue;

                        var procExeName = group.ExeName;
                        if (string.IsNullOrEmpty(procExeName))
                            continue;

                        // Get clean process name
                        var cleanName = procExeName.ToLowerInvariant();
                        if (cleanName.EndsWith(".exe"))
                            cleanName = cleanName.Substring(0, cleanName.Length - 4);

                        // Check if it's a known legacy player
                        if (KnownLegacyPlayers.Contains(cleanName))
                        {
                            isPlaying = true;
                            exeName = procExeName;
                            displayName = group.DisplayName;
                            Trace.WriteLine($"LegacyMediaPlayerService: Found active player - {exeName} ({displayName})");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"LegacyMediaPlayerService: FindActiveLegacyPlayer error - {ex.Message}");
            }
        }

        /// <summary>
        /// Get executable path for a player (for icon extraction)
        /// </summary>
        public string GetPlayerExecutablePath(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return null;

            var cleanName = playerName.ToLowerInvariant();
            if (cleanName.EndsWith(".exe"))
                cleanName = cleanName.Substring(0, cleanName.Length - 4);

            // Handle complex app IDs
            if (cleanName.Contains("!"))
                cleanName = cleanName.Substring(cleanName.LastIndexOf("!") + 1);
            if (cleanName.Contains(".") && !cleanName.Contains("\\"))
            {
                var parts = cleanName.Split('.');
                cleanName = parts[parts.Length - 1];
            }

            try
            {
                // Try running process first
                var processes = Process.GetProcessesByName(cleanName);
                if (processes.Length > 0)
                {
                    try
                    {
                        var path = processes[0].MainModule?.FileName;
                        if (!string.IsNullOrEmpty(path))
                            return path;
                    }
                    catch { }
                }

                // Try known paths
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                var knownPaths = new[]
                {
                    System.IO.Path.Combine(programFiles, "VideoLAN", "VLC", "vlc.exe"),
                    System.IO.Path.Combine(programFilesX86, "VideoLAN", "VLC", "vlc.exe"),
                    System.IO.Path.Combine(programFiles, "Windows Media Player", "wmplayer.exe"),
                    System.IO.Path.Combine(programFilesX86, "Windows Media Player", "wmplayer.exe"),
                    System.IO.Path.Combine(programFiles, "MPC-HC", "mpc-hc64.exe"),
                    System.IO.Path.Combine(programFilesX86, "MPC-HC", "mpc-hc.exe"),
                    System.IO.Path.Combine(programFiles, "MPC-BE x64", "mpc-be64.exe"),
                    System.IO.Path.Combine(programFilesX86, "MPC-BE", "mpc-be.exe"),
                    System.IO.Path.Combine(programFilesX86, "foobar2000", "foobar2000.exe"),
                    System.IO.Path.Combine(programFiles, "DAUM", "PotPlayer", "PotPlayerMini64.exe"),
                };

                foreach (var path in knownPaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        var fileName = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                        if (cleanName.Contains(fileName) || fileName.Contains(cleanName))
                            return path;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"LegacyMediaPlayerService: GetPlayerExecutablePath error - {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get media info from window title
        /// </summary>
        public string GetCurrentMediaInfo()
        {
            if (!_isPlaying || string.IsNullOrEmpty(_currentPlayerExeName))
                return null;

            try
            {
                var processName = _currentPlayerExeName;
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    processName = processName.Substring(0, processName.Length - 4);

                var processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    var title = processes[0].MainWindowTitle;
                    if (!string.IsNullOrEmpty(title))
                    {
                        // Clean up common suffixes
                        title = title
                            .Replace(" - VLC media player", "")
                            .Replace(" - Windows Media Player", "")
                            .Replace(" - foobar2000", "")
                            .Replace(" - MPC-HC", "")
                            .Replace(" - MPC-BE", "")
                            .Replace(" [foobar2000]", "")
                            .Trim();

                        if (!string.IsNullOrWhiteSpace(title))
                            return title;
                    }
                }
            }
            catch { }

            return _currentPlayerDisplayName ?? _currentPlayerExeName;
        }

        public void GetTimelineInfo(out TimeSpan position, out TimeSpan duration)
        {
            position = TimeSpan.Zero;
            duration = TimeSpan.Zero;
            // Not supported for legacy players
        }

        // Control methods - not supported for legacy players but kept for API compatibility
        public void PlayPause() { }
        public void Play() { }
        public void Pause() { }
        public void Next() { }
        public void Previous() { }
        public void SeekTo(TimeSpan position) { }

        /// <summary>
        /// Find any running media player and return its path (for icon extraction)
        /// </summary>
        public string FindRunningMediaPlayerPath()
        {
            if (_isPlaying && !string.IsNullOrEmpty(_currentPlayerExeName))
            {
                return GetPlayerExecutablePath(_currentPlayerExeName);
            }
            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pollTimer?.Stop();
        }
    }
}
