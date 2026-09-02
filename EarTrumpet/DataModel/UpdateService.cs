using EarTrumpet.Properties;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
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

        public string UpdateText
        {
            get
            {
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

                // Find the setup .exe and portable .zip asset URLs for our architecture.
                _setupDownloadUrl = null;
                _portableDownloadUrl = null;
                var assets = json["assets"] as JArray;
                if (assets != null)
                {
                    _setupDownloadUrl = FindAssetUrl(assets, "-setup", ".exe");
                    _portableDownloadUrl = FindAssetUrl(assets, "-portable", ".zip");
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
        /// Release-asset suffix for the architecture this process is running as. x86 keeps the
        /// historical unsuffixed name ("BetterTrumpet-3.4.0-setup.exe"); x64 and arm64 releases
        /// carry "-x64"/"-arm64" so each install updates to a binary of its own architecture.
        /// This is deliberately the *process* architecture, not the machine's: an x86 install
        /// running under ARM64 emulation keeps updating to x86 rather than swapping the user's
        /// install out from under them for a different binary. Migrating to a native build is a
        /// manual reinstall.
        /// </summary>
        private static string ArchAssetSuffix
        {
            get
            {
                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.X64: return "-x64";
                    case Architecture.Arm64: return "-arm64";
                    default: return "";
                }
            }
        }

        /// <summary>
        /// Picks the release asset matching this process's architecture, e.g.
        /// "BetterTrumpet-3.4.0-setup-arm64.exe". Falls back to the unsuffixed x86 asset so
        /// releases that ship x86 only (every release before 3.4.0, or one where an arch build
        /// was dropped) still update — x86 runs everywhere, just not natively.
        /// </summary>
        private static string FindAssetUrl(JArray assets, string kind, string extension)
        {
            string UrlForSuffix(string suffix)
            {
                var wanted = kind + suffix + extension;
                var asset = assets.FirstOrDefault(a =>
                    (a["name"]?.ToString() ?? "").EndsWith(wanted, StringComparison.OrdinalIgnoreCase));
                return asset?["browser_download_url"]?.ToString();
            }

            var suffixed = UrlForSuffix(ArchAssetSuffix);
            if (suffixed != null || ArchAssetSuffix.Length == 0)
            {
                return suffixed;
            }

            var fallback = UrlForSuffix("");
            if (fallback != null)
            {
                Trace.WriteLine($"UpdateService: No {ArchAssetSuffix} '{kind}' asset in this release — falling back to x86");
            }
            return fallback;
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
            if (IsDownloading) return;

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
                var fileName = $"BetterTrumpet-{LatestVersion}-setup{ArchAssetSuffix}.exe";
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

        /// <summary>
        /// Portable mode: downloads the portable .zip and hands off to a small PowerShell
        /// script that waits for this process to exit, replaces the app's files in place
        /// (keeping ./config, portable.marker and any other user data), and relaunches.
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
            string scriptPath = null;

            try
            {
                zipPath = Path.Combine(Path.GetTempPath(), $"BetterTrumpet-{LatestVersion}-portable{ArchAssetSuffix}.zip");
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

                Trace.WriteLine($"UpdateService: Downloaded {new FileInfo(zipPath).Length / 1024}KB — extracting");

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

                var exePath = Process.GetCurrentProcess().MainModule.FileName;
                var targetDir = Path.GetDirectoryName(exePath);

                // robocopy mirrors the new release over the app folder:
                //   /MIR  — also prunes files removed from the new release (no stale DLLs);
                //   /XD   — keeps ./config, the only user data in portable mode;
                //   /R /W — bound retries so a transient lock (AV, lingering child process)
                //           can't hang the script forever (robocopy defaults to ~1M retries).
                var script = string.Join(Environment.NewLine, new[]
                {
                    "$ErrorActionPreference = 'Stop'",
                    // Wait for this process to exit, capped at 60s; force-kill as a safety net
                    // if shutdown hangs (e.g. a modal dialog is still up).
                    $"$proc = Get-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue",
                    $"if ($proc -and -not $proc.WaitForExit(60000)) {{ Stop-Process -Id {Environment.ProcessId} -Force }}",
                    "Start-Sleep -Milliseconds 500",
                    $"robocopy '{sourceDir}' '{targetDir}' /MIR /XD '{targetDir}\\config' /XJ /R:3 /W:5 /NFL /NDL /NJH /NJS /NP | Out-Null",
                    "if ($LASTEXITCODE -ge 8) { throw \"robocopy failed with exit code $LASTEXITCODE\" }",
                    $"Remove-Item '{extractDir}' -Recurse -Force -ErrorAction SilentlyContinue",
                    $"Remove-Item '{zipPath}' -Force -ErrorAction SilentlyContinue",
                    $"Start-Process '{exePath}' -WorkingDirectory '{targetDir}'",
                    "Remove-Item $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue",
                });

                scriptPath = Path.Combine(Path.GetTempPath(), $"BetterTrumpet-update-{LatestVersion}.ps1");
                File.WriteAllText(scriptPath, script);

                Trace.WriteLine("UpdateService: Launching portable update script, then exiting");

                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                    UseShellExecute = true
                });

                // Exit via the dispatcher so the app shuts down cleanly; the script
                // waits for this process before touching the files.
                _ = _dispatcher.BeginInvoke(new Action(() => System.Windows.Application.Current.Shutdown()));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"UpdateService: Portable download/update failed — {ex.Message}");

                // Clean up intermediates so a later attempt starts fresh
                try { if (zipPath != null && File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                try { if (extractDir != null && Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch { }
                try { if (scriptPath != null && File.Exists(scriptPath)) File.Delete(scriptPath); } catch { }

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
