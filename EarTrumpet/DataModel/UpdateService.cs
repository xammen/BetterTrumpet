using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace EarTrumpet.DataModel
{
    /// <summary>
    /// Controls which version bumps trigger an update notification.
    /// </summary>
    public enum UpdateChannel
    {
        /// <summary>All updates: patch (3.0.0→3.0.1), minor (3.0→3.1), major (3→4)</summary>
        All = 0,
        /// <summary>Minor and major only (3.0→3.1, 3→4) — skips patch releases</summary>
        MinorAndMajor = 1,
        /// <summary>Major only (3→4) — skips minor and patch</summary>
        MajorOnly = 2,
        /// <summary>Never notify</summary>
        None = 3,
    }

    /// <summary>
    /// Checks GitHub releases for new versions of BetterTrumpet.
    /// Checks at startup (after 10s delay) then every 6 hours.
    /// Can download and install updates silently via Inno Setup /VERYSILENT.
    /// </summary>
    public class UpdateService : INotifyPropertyChanged
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/xammen/BetterTrumpet/releases/latest";
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action UpdateAvailableChanged;

        private readonly HttpClient _httpClient;
        private readonly DispatcherTimer _timer;
        private readonly Dispatcher _dispatcher;

        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            private set
            {
                if (_isUpdateAvailable != value)
                {
                    _isUpdateAvailable = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAvailable)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateText)));
                    UpdateAvailableChanged?.Invoke();
                }
            }
        }

        private string _latestVersion;
        public string LatestVersion
        {
            get => _latestVersion;
            private set
            {
                if (_latestVersion != value)
                {
                    _latestVersion = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LatestVersion)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateText)));
                }
            }
        }

        private string _releaseUrl;
        public string ReleaseUrl
        {
            get => _releaseUrl;
            private set
            {
                _releaseUrl = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReleaseUrl)));
            }
        }

        private string _releaseNotes;
        public string ReleaseNotes
        {
            get => _releaseNotes;
            private set
            {
                _releaseNotes = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReleaseNotes)));
            }
        }

        private bool _isChecking;
        public bool IsChecking
        {
            get => _isChecking;
            private set
            {
                if (_isChecking != value)
                {
                    _isChecking = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecking)));
                }
            }
        }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            private set
            {
                if (_isDownloading != value)
                {
                    _isDownloading = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDownloading)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateText)));
                }
            }
        }

        private DateTime _lastCheckTime;
        public DateTime LastCheckTime
        {
            get => _lastCheckTime;
            private set
            {
                _lastCheckTime = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastCheckTime)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastCheckText)));
            }
        }

        /// <summary>Direct download URL for the setup .exe from GitHub release assets.</summary>
        private string _setupDownloadUrl;

        public string UpdateText
        {
            get
            {
                if (IsDownloading) return "Downloading update...";
                return IsUpdateAvailable ? $"Update available: v{LatestVersion}" : "Up to date";
            }
        }

        public string LastCheckText => LastCheckTime == DateTime.MinValue
            ? "Never checked"
            : $"Last checked: {LastCheckTime:g}";

        public UpdateService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BetterTrumpet-UpdateChecker");
            _httpClient.Timeout = TimeSpan.FromSeconds(15);

            _timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = CheckInterval
            };
            _timer.Tick += (_, __) => CheckForUpdateAsync();
        }

        /// <summary>
        /// The channel filter — set before calling Start() or CheckForUpdateAsync().
        /// </summary>
        public UpdateChannel Channel { get; set; } = UpdateChannel.All;

        /// <summary>
        /// Start the update check cycle: delay then check, then every 6h.
        /// </summary>
        public void Start()
        {
            // Delayed first check
            var startupTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = StartupDelay
            };
            startupTimer.Tick += (_, __) =>
            {
                startupTimer.Stop();
                CheckForUpdateAsync();
                _timer.Start();
            };
            startupTimer.Start();

            Trace.WriteLine($"UpdateService: Started, first check in {StartupDelay.TotalSeconds}s, then every {CheckInterval.TotalHours}h");
        }

        public void Stop()
        {
            _timer.Stop();
        }

        /// <summary>
        /// Manual check (from Settings) or periodic check.
        /// </summary>
        public async void CheckForUpdateAsync()
        {
            if (IsChecking) return;
            IsChecking = true;

            try
            {
                Trace.WriteLine("UpdateService: Checking for updates...");
                var response = await _httpClient.GetStringAsync(GitHubApiUrl);
                var json = JObject.Parse(response);

                var tagName = json["tag_name"]?.ToString() ?? "";
                var htmlUrl = json["html_url"]?.ToString() ?? "";
                var body = json["body"]?.ToString() ?? "";

                // Strip leading 'v' from tag
                var versionStr = tagName.TrimStart('v', 'V');

                ReleaseUrl = htmlUrl;
                ReleaseNotes = body;
                LastCheckTime = DateTime.Now;

                // Find the setup .exe asset URL
                _setupDownloadUrl = null;
                var assets = json["assets"] as JArray;
                if (assets != null)
                {
                    var setupAsset = assets.FirstOrDefault(a =>
                        (a["name"]?.ToString() ?? "").EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase));
                    if (setupAsset != null)
                    {
                        _setupDownloadUrl = setupAsset["browser_download_url"]?.ToString();
                    }
                }

                if (Version.TryParse(versionStr, out var remoteVersion))
                {
                    var localVersion = App.PackageVersion;
                    LatestVersion = versionStr;

                    if (localVersion != null && remoteVersion > localVersion && IsRelevantUpdate(localVersion, remoteVersion, Channel))
                    {
                        Trace.WriteLine($"UpdateService: Update available! {localVersion} → {remoteVersion} (channel={Channel})");
                        IsUpdateAvailable = true;
                    }
                    else
                    {
                        Trace.WriteLine($"UpdateService: Up to date or filtered ({localVersion}, channel={Channel})");
                        IsUpdateAvailable = false;
                    }
                }
                else
                {
                    Trace.WriteLine($"UpdateService: Could not parse version from tag '{tagName}'");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"UpdateService: Check failed — {ex.Message}");
            }
            finally
            {
                IsChecking = false;
            }
        }

        /// <summary>
        /// Determines if the remote version is relevant given the user's channel preference.
        /// Major: 3.x.x → 4.x.x  |  Minor: 3.0.x → 3.1.x  |  Patch: 3.0.0 → 3.0.1
        /// </summary>
        private static bool IsRelevantUpdate(Version local, Version remote, UpdateChannel channel)
        {
            switch (channel)
            {
                case UpdateChannel.None:
                    return false;

                case UpdateChannel.MajorOnly:
                    return remote.Major > local.Major;

                case UpdateChannel.MinorAndMajor:
                    return remote.Major > local.Major
                        || (remote.Major == local.Major && remote.Minor > local.Minor);

                case UpdateChannel.All:
                default:
                    return true; // Any newer version (already checked remoteVersion > localVersion)
            }
        }

        /// <summary>
        /// Downloads the setup installer and runs it with /VERYSILENT.
        /// The installer will kill the running instance, install, and relaunch.
        /// Falls back to opening the GitHub release page if download fails.
        /// </summary>
        public async void DownloadAndInstallAsync()
        {
            if (IsDownloading) return;

            if (string.IsNullOrEmpty(_setupDownloadUrl))
            {
                Trace.WriteLine("UpdateService: No setup download URL — falling back to release page");
                OpenReleasePage();
                return;
            }

            IsDownloading = true;
            string tempPath = null;

            try
            {
                var fileName = $"BetterTrumpet-{LatestVersion}-setup.exe";
                tempPath = Path.Combine(Path.GetTempPath(), fileName);

                Trace.WriteLine($"UpdateService: Downloading {_setupDownloadUrl} → {tempPath}");

                // Use a separate HttpClient with longer timeout for download
                using (var downloadClient = new HttpClient())
                {
                    downloadClient.DefaultRequestHeaders.Add("User-Agent", "BetterTrumpet-Updater");
                    downloadClient.Timeout = TimeSpan.FromMinutes(5);

                    using (var stream = await downloadClient.GetStreamAsync(_setupDownloadUrl))
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }

                var fileInfo = new FileInfo(tempPath);
                Trace.WriteLine($"UpdateService: Downloaded {fileInfo.Length / 1024}KB — launching installer");

                // Launch the installer silently — it will kill us, install, and relaunch
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                    UseShellExecute = true
                });

                // The installer's PrepareToInstall() will taskkill us
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"UpdateService: Download/install failed — {ex.Message}");

                // Clean up failed download
                try { if (tempPath != null && File.Exists(tempPath)) File.Delete(tempPath); }
                catch { }

                // Fallback: open release page
                OpenReleasePage();
            }
            finally
            {
                IsDownloading = false;
            }
        }

        public void OpenReleasePage()
        {
            if (!string.IsNullOrEmpty(ReleaseUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = ReleaseUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"UpdateService: Failed to open release page — {ex.Message}");
                }
            }
        }
    }
}
