using EarTrumpet.Properties;
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
        private bool _started;

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

        private bool _isInstalling;
        public bool IsInstalling
        {
            get => _isInstalling;
            private set
            {
                if (_isInstalling != value)
                {
                    _isInstalling = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstalling)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateText)));
                }
            }
        }

        public bool IsBusy => IsDownloading || IsInstalling;

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

        /// <summary>Direct download URL for the portable .zip from GitHub release assets.</summary>
        private string _portableDownloadUrl;

        /// <summary>Expected SHA-256 (lowercase hex) of each asset, from the GitHub API "digest" field.</summary>
        private string _setupSha256;
        private string _portableSha256;

        public string UpdateText
        {
            get
            {
                if (IsInstalling) return Resources.UpdateTextInstalling;
                if (IsDownloading) return Resources.UpdateTextDownloading;
                return IsUpdateAvailable ? string.Format(Resources.UpdateTextAvailable, LatestVersion) : Resources.UpdateTextUpToDate;
            }
        }

        public string LastCheckText => LastCheckTime == DateTime.MinValue
            ? Resources.UpdateTextNeverChecked
            : string.Format(Resources.UpdateTextLastChecked, LastCheckTime.ToString("g"));

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
        /// Only runs if auto-updates are enabled.
        /// </summary>
        public void Start()
        {
            if (Storage.StorageFactory.IsPortableMode)
            {
                PortableUpdateApplier.CleanupLeftovers();
            }

            // Don't start timer if updates are disabled
            if (Channel == UpdateChannel.None)
            {
                Trace.WriteLine("UpdateService: Auto-updates disabled (Channel=None), skipping timer");
                return;
            }

            if (_started)
            {
                return;
            }
            _started = true;

            // Delayed first check
            var startupTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = StartupDelay
            };
            startupTimer.Tick += (_, __) =>
            {
                startupTimer.Stop();
                CheckForUpdateAsync();
                // Don't start recurring timer if updates are disabled
                if (Channel != UpdateChannel.None)
                {
                    _timer.Start();
                }
            };
            startupTimer.Start();

            Trace.WriteLine($"UpdateService: Started, first check in {StartupDelay.TotalSeconds}s, then every {CheckInterval.TotalHours}h (Channel={Channel})");
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

                // Find the setup .exe and portable .zip asset URLs
                _setupDownloadUrl = null;
                _portableDownloadUrl = null;
                _setupSha256 = null;
                _portableSha256 = null;
                var assets = json["assets"] as JArray;
                if (assets != null)
                {
                    var setupAsset = assets.FirstOrDefault(a =>
                        (a["name"]?.ToString() ?? "").EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase));
                    if (setupAsset != null)
                    {
                        _setupDownloadUrl = setupAsset["browser_download_url"]?.ToString();
                        _setupSha256 = ParseSha256Digest(setupAsset["digest"]?.ToString());
                    }

                    var portableAsset = assets.FirstOrDefault(a =>
                        (a["name"]?.ToString() ?? "").EndsWith("-portable.zip", StringComparison.OrdinalIgnoreCase));
                    if (portableAsset != null)
                    {
                        _portableDownloadUrl = portableAsset["browser_download_url"]?.ToString();
                        _portableSha256 = ParseSha256Digest(portableAsset["digest"]?.ToString());
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
        /// In portable mode, the portable .zip is applied in place instead — we must not
        /// run the setup, which would install a separate copy system-wide.
        /// Falls back to opening the GitHub release page if download fails.
        /// </summary>
        public async void DownloadAndInstallAsync()
        {
            if (IsBusy) return;

            if (Storage.StorageFactory.IsPortableMode)
            {
                DownloadAndUpdatePortableAsync();
                return;
            }

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

                VerifyDownload(tempPath, _setupSha256);

                var fileInfo = new FileInfo(tempPath);
                Trace.WriteLine($"UpdateService: Downloaded {fileInfo.Length / 1024}KB, SHA-256 verified — launching installer");

                // Launch the installer silently — it will kill us, install, and relaunch
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                    UseShellExecute = true
                });

                // The installer's PrepareToInstall() will taskkill us. If it never does
                // (installer failed or was blocked), release the banner after a while.
                IsInstalling = true;
                _ = ResetInstallingAfterTimeoutAsync();
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

        private async Task ResetInstallingAfterTimeoutAsync()
        {
            await Task.Delay(TimeSpan.FromMinutes(2));
            Trace.WriteLine("UpdateService: Installer did not replace this instance within 2 minutes");
            IsInstalling = false;
        }

        /// <summary>
        /// Portable mode: downloads and extracts the portable .zip, then starts the extracted
        /// exe, which waits for this process to exit, replaces the app's files in place
        /// (keeping ./config) and relaunches (<see cref="PortableUpdateApplier"/>).
        /// Falls back to opening the GitHub release page if anything fails.
        /// </summary>
        private async void DownloadAndUpdatePortableAsync()
        {
            if (string.IsNullOrEmpty(_portableDownloadUrl))
            {
                Trace.WriteLine("UpdateService: No portable download URL — falling back to release page");
                OpenReleasePage();
                return;
            }

            IsDownloading = true;
            string zipPath = null;
            string extractDir = null;

            try
            {
                zipPath = Path.Combine(Path.GetTempPath(), $"BetterTrumpet-{LatestVersion}-portable.zip");
                extractDir = Path.Combine(Path.GetTempPath(), $"BetterTrumpet-{LatestVersion}-update");

                Trace.WriteLine($"UpdateService: Portable mode — downloading {_portableDownloadUrl} → {zipPath}");

                using (var downloadClient = new HttpClient())
                {
                    downloadClient.DefaultRequestHeaders.Add("User-Agent", "BetterTrumpet-Updater");
                    downloadClient.Timeout = TimeSpan.FromMinutes(5);

                    using (var stream = await downloadClient.GetStreamAsync(_portableDownloadUrl))
                    using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }

                VerifyDownload(zipPath, _portableSha256);

                Trace.WriteLine($"UpdateService: Downloaded {new FileInfo(zipPath).Length / 1024}KB, SHA-256 verified — extracting");

                if (Directory.Exists(extractDir))
                {
                    Directory.Delete(extractDir, true);
                }
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir);

                // The zip's content is laid out exactly like the portable install folder.
                var sourceDir = extractDir;
                if (!File.Exists(Path.Combine(sourceDir, "BetterTrumpet.exe")))
                {
                    var singleDir = Directory.GetDirectories(extractDir).FirstOrDefault();
                    if (singleDir != null && File.Exists(Path.Combine(singleDir, "BetterTrumpet.exe")))
                    {
                        sourceDir = singleDir;
                    }
                    else
                    {
                        throw new InvalidDataException("Portable zip has an unexpected layout (BetterTrumpet.exe not found)");
                    }
                }

                var targetDir = Path.GetDirectoryName(Environment.ProcessPath);

                Trace.WriteLine("UpdateService: Starting the new release to apply itself, then exiting");

                // The new exe waits for this process, mirrors itself over targetDir (keeping
                // ./config) and relaunches; see PortableUpdateApplier.
                Process.Start(PortableUpdateApplier.CreateStartInfo(Path.Combine(sourceDir, "BetterTrumpet.exe"), targetDir));
                IsInstalling = true;

                // Exit via the dispatcher so the app shuts down cleanly; the new exe
                // waits for this process before touching the files.
                _ = _dispatcher.BeginInvoke(new Action(() => System.Windows.Application.Current.Shutdown()));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"UpdateService: Portable download/update failed — {ex.Message}");

                // Clean up intermediates so a later attempt starts fresh
                try { if (zipPath != null && File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                try { if (extractDir != null && Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch { }

                // Fallback: open release page
                OpenReleasePage();
            }
            finally
            {
                IsDownloading = false;
            }
        }

        /// <summary>
        /// GitHub publishes "sha256:&lt;hex&gt;" for every release asset. Returns the hex part,
        /// or null when the field is missing or uses another algorithm.
        /// </summary>
        private static string ParseSha256Digest(string digest)
        {
            const string prefix = "sha256:";
            if (string.IsNullOrEmpty(digest) || !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var hex = digest.Substring(prefix.Length).Trim().ToLowerInvariant();
            return hex.Length == 64 && hex.All(Uri.IsHexDigit) ? hex : null;
        }

        /// <summary>
        /// Refuses to run or extract anything whose hash does not match the published digest.
        /// Fails closed: no digest means no silent install (the caller falls back to the release page).
        /// </summary>
        private static void VerifyDownload(string path, string expectedSha256)
        {
            if (expectedSha256 == null)
            {
                throw new InvalidDataException("Release asset has no SHA-256 digest; refusing to run it unverified");
            }

            string actual;
            using (var stream = File.OpenRead(path))
            {
                actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
            }

            if (actual != expectedSha256)
            {
                throw new InvalidDataException($"SHA-256 mismatch (expected {expectedSha256}, got {actual})");
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
