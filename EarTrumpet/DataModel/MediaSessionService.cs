using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace EarTrumpet.DataModel
{
    /// <summary>
    /// Service that monitors media playback using the Windows Media Session API.
    /// This is the modern Windows 10+ API that tracks media playback status from
    /// any app that integrates with Windows media controls (Spotify, Chrome, VLC, etc.)
    /// </summary>
    public class MediaSessionService
    {
        private static MediaSessionService _instance;
        private static readonly object _lock = new object();

        public static MediaSessionService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new MediaSessionService();
                        }
                    }
                }
                return _instance;
            }
        }

        private GlobalSystemMediaTransportControlsSessionManager _sessionManager;
        private bool _isInitialized;
        private bool _isMediaPlaying;
        private readonly Dispatcher _dispatcher;
        private LegacyMediaPlayerService _legacyService;
        private string _lastTracedSessionId;

        // The one session the popup shows and controls. Re-chosen on the UI thread whenever
        // sessions or playback states change; events from other sessions are ignored, so a
        // background tab can no longer swap the title, artwork or controls under the user.
        private volatile GlobalSystemMediaTransportControlsSession _pinnedSession;

        // Providers raise MediaPropertiesChanged several times per track (title, then artwork...).
        private readonly DispatcherTimer _trackChangeDebounce;

        // Thumbnail cache — prevents redundant SMTC stream reads when multiple consumers
        // request the thumbnail on the same track (MediaPopup, AlbumArt theme, PreloadThumbnail).
        private BitmapImage _cachedThumbnail;
        private volatile bool _thumbnailDirty = true;
        private int _thumbnailCacheVersion;
        private readonly object _thumbnailLock = new object();

        /// <summary>
        /// Event fired when media playback state changes (play/pause/stop)
        /// </summary>
        public event Action<bool> MediaPlaybackChanged;

        /// <summary>
        /// Event fired when media track changes (new song)
        /// </summary>
        public event Action MediaTrackChanged;

        /// <summary>
        /// Event fired when timeline/position changes
        /// </summary>
        public event Action TimelineChanged;

        /// <summary>
        /// Gets whether any media is currently playing
        /// </summary>
        public bool IsMediaPlaying => _isMediaPlaying;

        /// <summary>
        /// Gets whether the service is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets whether Windows still exposes a media session that can be resumed.
        /// </summary>
        public bool HasControllableSession
        {
            get
            {
                try
                {
                    return GetCurrentSession() != null || _legacyService?.IsPlaying == true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private MediaSessionService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _trackChangeDebounce = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _trackChangeDebounce.Tick += (_, __) =>
            {
                _trackChangeDebounce.Stop();
                InvalidateThumbnailCache();
                MediaTrackChanged?.Invoke();
            };
            Initialize();
            InitializeLegacyService();
        }

        private void InitializeLegacyService()
        {
            try
            {
                _legacyService = LegacyMediaPlayerService.Instance;
                _legacyService.PlaybackChanged += OnLegacyPlaybackChanged;
                _legacyService.TrackChanged += OnLegacyTrackChanged;
                Trace.WriteLine("MediaSessionService: Legacy service initialized");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: Failed to init legacy service - {ex.Message}");
            }
        }

        private void OnLegacyPlaybackChanged(bool isPlaying)
        {
            // Only use legacy if SMTC has no playing session
            if (!CheckIfAnyMediaPlaying())
            {
                bool wasPlaying = _isMediaPlaying;
                _isMediaPlaying = isPlaying;

                if (wasPlaying != _isMediaPlaying)
                {
                    _dispatcher.BeginInvoke(new Action(() =>
                    {
                        MediaPlaybackChanged?.Invoke(_isMediaPlaying);
                    }));
                }
            }
        }

        private void OnLegacyTrackChanged()
        {
            // Only use legacy if SMTC has no playing session
            if (!CheckIfAnyMediaPlaying())
            {
                InvalidateThumbnailCache();
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    MediaTrackChanged?.Invoke();
                }));
            }
        }

        /// <summary>
        /// Check if using legacy player (for fallback UI)
        /// </summary>
        public bool IsUsingLegacyPlayer => _legacyService?.IsPlaying == true && !CheckIfAnyMediaPlaying();

        /// <summary>
        /// Get legacy player name
        /// </summary>
        public string LegacyPlayerName => _legacyService?.CurrentPlayerName;

        /// <summary>
        /// Gets the Windows app identifier for the media session currently shown by the popup.
        /// </summary>
        public string CurrentSourceAppId
        {
            get
            {
                try
                {
                    return GetCurrentSession()?.SourceAppUserModelId;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"MediaSessionService: CurrentSourceAppId failed - {ex.Message}");
                    return null;
                }
            }
        }

        private void Initialize()
        {
            try
            {
                var operation = GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                operation.Completed = OnSessionManagerReady;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: Initialization failed - {ex.Message}");
            }
        }

        private void OnSessionManagerReady(IAsyncOperation<GlobalSystemMediaTransportControlsSessionManager> asyncInfo, AsyncStatus asyncStatus)
        {
            try
            {
                if (asyncStatus == AsyncStatus.Completed)
                {
                    _sessionManager = asyncInfo.GetResults();
                    
                    if (_sessionManager != null)
                    {
                        _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                        _sessionManager.SessionsChanged += OnSessionsChanged;
                        
                        // Completes on a background thread; session choice and its timer live on the UI thread.
                        _dispatcher.BeginInvoke(new Action(() =>
                        {
                            SubscribeToAllSessions();
                            UpdatePlaybackState();
                        }));
                        
                        _isInitialized = true;
                        Trace.WriteLine("MediaSessionService: Initialized successfully");
                    }
                    else
                    {
                        Trace.WriteLine("MediaSessionService: Session manager is null");
                    }
                }
                else
                {
                    Trace.WriteLine($"MediaSessionService: Async operation status = {asyncStatus}");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: OnSessionManagerReady failed - {ex.Message}");
            }
        }

        private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            Trace.WriteLine("MediaSessionService: Current session changed");
            _dispatcher.BeginInvoke(new Action(() =>
            {
                SubscribeToAllSessions();
                UpdatePlaybackState();
            }));
        }

        private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            Trace.WriteLine("MediaSessionService: Sessions changed");
            _dispatcher.BeginInvoke(new Action(() =>
            {
                SubscribeToAllSessions();
                UpdatePlaybackState();
            }));
        }

        private void SubscribeToAllSessions()
        {
            try
            {
                var sessionList = _sessionManager?.GetSessions();
                if (sessionList == null) return;

                GlobalSystemMediaTransportControlsSession[] sessions;
                try
                {
                    sessions = new GlobalSystemMediaTransportControlsSession[sessionList.Count];
                    for (int i = 0; i < sessionList.Count; i++)
                        sessions[i] = sessionList[i];
                }
                catch { return; }

                foreach (var session in sessions)
                {
                    try
                    {
                        // Unsubscribe first to avoid duplicates
                        session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                        session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                        session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                        session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                        session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
                        session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"MediaSessionService: Error subscribing to session - {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: Error in SubscribeToAllSessions - {ex.Message}");
            }
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            _dispatcher.BeginInvoke(new Action(() => UpdatePlaybackState()));
        }

        private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            if (!IsPinned(sender)) return;
            _dispatcher.BeginInvoke(new Action(() =>
            {
                TimelineChanged?.Invoke();
            }));
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            if (!IsPinned(sender)) return;

            Trace.WriteLine("MediaSessionService: Media properties changed (track change)");
            _dispatcher.BeginInvoke(new Action(() =>
            {
                // Restart: one notification per burst, once the provider settled.
                _trackChangeDebounce.Stop();
                _trackChangeDebounce.Start();
            }));
        }

        private bool IsPinned(GlobalSystemMediaTransportControlsSession session) => SameSession(_pinnedSession, session);

        // The projection may hand out a new wrapper for the same session, so fall back to the app id.
        private static bool SameSession(GlobalSystemMediaTransportControlsSession a, GlobalSystemMediaTransportControlsSession b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            try
            {
                return string.Equals(a.SourceAppUserModelId, b.SourceAppUserModelId, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private GlobalSystemMediaTransportControlsSession[] GetSessionsSnapshot()
        {
            try
            {
                var sessionList = _sessionManager?.GetSessions();
                if (sessionList == null) return Array.Empty<GlobalSystemMediaTransportControlsSession>();

                var sessions = new GlobalSystemMediaTransportControlsSession[sessionList.Count];
                for (int i = 0; i < sessionList.Count; i++)
                    sessions[i] = sessionList[i];
                return sessions;
            }
            catch
            {
                return Array.Empty<GlobalSystemMediaTransportControlsSession>();
            }
        }

        private static bool IsPlaying(GlobalSystemMediaTransportControlsSession session)
        {
            try
            {
                return session?.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Playing beats paused; among playing sessions the pinned one stays, then the one Windows
        /// calls current. With nothing playing, the pinned session stays (so Play resumes it).
        /// </summary>
        private GlobalSystemMediaTransportControlsSession ChooseSession(GlobalSystemMediaTransportControlsSession[] sessions)
        {
            GlobalSystemMediaTransportControlsSession pinned = null;
            foreach (var session in sessions)
            {
                if (SameSession(_pinnedSession, session)) { pinned = session; break; }
            }

            bool pinnedAlive = pinned != null;
            if (pinnedAlive && IsPlaying(pinned)) return pinned;

            GlobalSystemMediaTransportControlsSession current = null;
            try { current = _sessionManager?.GetCurrentSession(); } catch { }
            if (IsPlaying(current)) return current;

            foreach (var session in sessions)
            {
                if (IsPlaying(session)) return session;
            }

            if (pinnedAlive) return pinned;
            return current ?? (sessions.Length > 0 ? sessions[0] : null);
        }

        // UI thread only.
        private void RefreshPinnedSession()
        {
            var chosen = ChooseSession(GetSessionsSnapshot());
            bool same = SameSession(chosen, _pinnedSession) || (chosen == null && _pinnedSession == null);
            _pinnedSession = chosen;
            if (same) return;

            TraceSelectedSession(chosen);

            // Different app, different content: same path as a track change.
            _trackChangeDebounce.Stop();
            InvalidateThumbnailCache();
            MediaTrackChanged?.Invoke();
        }

        private void UpdatePlaybackState()
        {
            try
            {
                RefreshPinnedSession();

                bool wasPlaying = _isMediaPlaying;
                _isMediaPlaying = CheckIfAnyMediaPlaying();

                if (wasPlaying != _isMediaPlaying)
                {
                    Trace.WriteLine($"MediaSessionService: Playback state changed - IsPlaying={_isMediaPlaying}");
                    
                    // Fire event on dispatcher thread
                    _dispatcher.BeginInvoke(new Action(() =>
                    {
                        MediaPlaybackChanged?.Invoke(_isMediaPlaying);
                    }));
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: Error updating playback state - {ex.Message}");
            }
        }

        private bool CheckIfAnyMediaPlaying()
        {
            foreach (var session in GetSessionsSnapshot())
            {
                if (IsPlaying(session)) return true;
            }

            return false;
        }

        /// <summary>
        /// Forces a refresh of the playback state. Useful for polling.
        /// </summary>
        public void RefreshPlaybackState()
        {
            UpdatePlaybackState();
        }

        /// <summary>
        /// The session the popup shows and controls (see <see cref="ChooseSession"/>). Before the
        /// first refresh, or from a background thread racing it, falls back to choosing on the spot.
        /// </summary>
        private GlobalSystemMediaTransportControlsSession GetCurrentSession()
        {
            return _pinnedSession ?? ChooseSession(GetSessionsSnapshot());
        }

        // Logged only when the choice changes; logging every lookup flooded the log (#69).
        private void TraceSelectedSession(GlobalSystemMediaTransportControlsSession session)
        {
            var id = session?.SourceAppUserModelId;
            if (!string.Equals(id, _lastTracedSessionId, StringComparison.Ordinal))
            {
                _lastTracedSessionId = id;
                Trace.WriteLine($"MediaSessionService: Selected playing session - {id}");
            }
        }

        /// <summary>
        /// Toggle play/pause on the current media session
        /// </summary>
        public void PlayPause()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    // Fallback to legacy WMP
                    _legacyService?.PlayPause();
                    return;
                }

                var playbackInfo = session.GetPlaybackInfo();
                if (playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    var _ = session.TryPauseAsync();
                }
                else
                {
                    var _ = session.TryPlayAsync();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: PlayPause failed - {ex.Message}");
                // Try legacy fallback
                _legacyService?.PlayPause();
            }
        }

        /// <summary>
        /// Play the current media session
        /// </summary>
        public void Play()
        {
            try
            {
                var session = GetCurrentSession();
                if (session != null) { var _ = session.TryPlayAsync(); }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: Play failed - {ex.Message}");
            }
        }

        /// <summary>
        /// Pause the current media session
        /// </summary>
        public void Pause()
        {
            try
            {
                var session = GetCurrentSession();
                if (session != null) { var _ = session.TryPauseAsync(); }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: Pause failed - {ex.Message}");
            }
        }

        /// <summary>
        /// Skip to next track
        /// </summary>
        public void Next()
        {
            try
            {
                var session = GetCurrentSession();
                if (session != null) 
                { 
                    var _ = session.TrySkipNextAsync(); 
                }
                else
                {
                    _legacyService?.Next();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: Next failed - {ex.Message}");
                _legacyService?.Next();
            }
        }

        /// <summary>
        /// Skip to previous track
        /// </summary>
        public void Previous()
        {
            try
            {
                var session = GetCurrentSession();
                if (session != null) 
                { 
                    var _ = session.TrySkipPreviousAsync(); 
                }
                else
                {
                    _legacyService?.Previous();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: Previous failed - {ex.Message}");
                _legacyService?.Previous();
            }
        }

        /// <summary>
        /// Gets info about the currently playing media
        /// </summary>
        public string GetCurrentMediaInfo()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    // Try legacy WMP
                    return _legacyService?.GetCurrentMediaInfo();
                }

                GlobalSystemMediaTransportControlsSessionMediaProperties mediaProps = null;
                var operation = session.TryGetMediaPropertiesAsync();
                using var waitHandle = new System.Threading.ManualResetEventSlim(false);
                operation.Completed = (info, status) =>
                {
                    if (status == AsyncStatus.Completed)
                        mediaProps = info.GetResults();
                    SetQuietly(waitHandle);
                };

                // Callers are background threads. 100 ms was often too short for Spotify and
                // browsers, which made the popup show "No media playing" over a playing track.
                waitHandle.Wait(TimeSpan.FromMilliseconds(750));

                if (mediaProps != null)
                {
                    var title = mediaProps.Title;
                    var artist = mediaProps.Artist;
                    if (!string.IsNullOrEmpty(artist))
                        return $"{artist} - {title}";
                    return title;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: GetCurrentMediaInfo failed - {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Gets the album art/thumbnail for the currently playing media.
        /// Results are cached until the next track change to avoid redundant SMTC stream reads.
        /// Thread-safe: multiple callers will wait for a single fetch, then share the result.
        /// </summary>
        public BitmapImage GetCurrentThumbnail()
        {
            // Fast path: return cached thumbnail if still valid
            if (!_thumbnailDirty && _cachedThumbnail != null)
                return _cachedThumbnail;

            lock (_thumbnailLock)
            {
                // Double-check after acquiring lock (another thread may have fetched it)
                if (!_thumbnailDirty && _cachedThumbnail != null)
                    return _cachedThumbnail;

                var cacheVersion = System.Threading.Volatile.Read(ref _thumbnailCacheVersion);
                var result = FetchThumbnailCore();
                _cachedThumbnail = result;
                _thumbnailDirty = cacheVersion != System.Threading.Volatile.Read(ref _thumbnailCacheVersion);
                return result;
            }
        }

        /// <summary>
        /// Forces the next thumbnail request to read the current media session.
        /// This is used when a provider changes tracks without raising its media
        /// properties event.
        /// </summary>
        public void InvalidateThumbnailCache()
        {
            System.Threading.Interlocked.Increment(ref _thumbnailCacheVersion);
            _thumbnailDirty = true;
        }

        /// <summary>
        /// Core thumbnail fetching logic — expensive, only called when cache is dirty.
        /// </summary>
        private static void SetQuietly(System.Threading.ManualResetEventSlim waitHandle)
        {
            try { waitHandle.Set(); } catch (ObjectDisposedException) { }
        }

        private BitmapImage FetchThumbnailCore()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                    return GetLegacyPlayerIcon();

                // Get media properties (timeout 1.5s — reduced from 2s)
                var propsOp = session.TryGetMediaPropertiesAsync();
                using var propsWait = new System.Threading.ManualResetEventSlim(false);
                propsOp.Completed = (_, __) => SetQuietly(propsWait);
                if (propsOp.Status == AsyncStatus.Started)
                    propsWait.Wait(TimeSpan.FromMilliseconds(1500));

                if (propsOp.Status != AsyncStatus.Completed)
                    return GetLegacyPlayerIcon();

                var mediaProps = propsOp.GetResults();
                if (mediaProps?.Thumbnail == null)
                    return GetLegacyPlayerIcon();

                // Open the thumbnail stream (timeout 1.5s)
                var streamOp = mediaProps.Thumbnail.OpenReadAsync();
                using var streamWait = new System.Threading.ManualResetEventSlim(false);
                streamOp.Completed = (_, __) => SetQuietly(streamWait);
                if (streamOp.Status == AsyncStatus.Started)
                    streamWait.Wait(TimeSpan.FromMilliseconds(1500));

                if (streamOp.Status != AsyncStatus.Completed)
                    return GetLegacyPlayerIcon();

                using var stream = streamOp.GetResults();
                if (stream == null || stream.Size == 0)
                    return GetLegacyPlayerIcon();

                // Read the stream (timeout 1.5s)
                var size = (uint)stream.Size;
                var buffer = new Windows.Storage.Streams.Buffer(size);

                var readOp = stream.ReadAsync(buffer, size, InputStreamOptions.None);
                using var readWait = new System.Threading.ManualResetEventSlim(false);
                readOp.Completed = (_, __) => SetQuietly(readWait);
                if (readOp.Status == AsyncStatus.Started)
                    readWait.Wait(TimeSpan.FromMilliseconds(1500));

                if (readOp.Status != AsyncStatus.Completed)
                    return GetLegacyPlayerIcon();

                var readBuffer = readOp.GetResults();
                if (readBuffer == null || readBuffer.Length == 0)
                    return GetLegacyPlayerIcon();

                var bytes = new byte[readBuffer.Length];
                using var dataReader = DataReader.FromBuffer(readBuffer);
                dataReader.ReadBytes(bytes);

                // Decode at reduced resolution — covers are displayed small, no need for full-res
                using (var memStream = new MemoryStream(bytes))
                {
                    // Cap large covers at 300 px, but never upscale small ones: the popup shows
                    // those through its low-resolution path, which upscaling made unreachable.
                    int sourceWidth = 0;
                    try
                    {
                        sourceWidth = BitmapDecoder.Create(memStream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0].PixelWidth;
                    }
                    catch { }
                    memStream.Position = 0;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    if (sourceWidth == 0 || sourceWidth > 300)
                    {
                        bitmap.DecodePixelWidth = 300;
                    }
                    bitmap.StreamSource = memStream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    Trace.WriteLine($"MediaSessionService: Thumbnail fetched {bitmap.PixelWidth}x{bitmap.PixelHeight} from {bytes.Length} bytes");
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: GetCurrentThumbnail failed - {ex.Message}");
            }
            return GetLegacyPlayerIcon();
        }

        /// <summary>
        /// Gets the icon for a media player as fallback cover art when SMTC thumbnail is not available
        /// </summary>
        private BitmapImage GetLegacyPlayerIcon()
        {
            try
            {
                // Get player name from SMTC session's SourceAppUserModelId
                var session = GetCurrentSession();
                if (session == null)
                {
                    // No SMTC session - try to find any running media player
                    var playerPath = _legacyService?.FindRunningMediaPlayerPath();
                    if (!string.IsNullOrEmpty(playerPath))
                    {
                        return ExtractIconFromPath(playerPath);
                    }
                    return null;
                }

                var sourceAppId = session.SourceAppUserModelId;
                if (string.IsNullOrEmpty(sourceAppId))
                    return null;

                // sourceAppId used for player exe path lookup

                // Try to get executable path from legacy service
                var exePath = _legacyService?.GetPlayerExecutablePath(sourceAppId);
                if (!string.IsNullOrEmpty(exePath))
                {
                    return ExtractIconFromPath(exePath);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: GetLegacyPlayerIcon failed - {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract icon from executable path and return as BitmapImage
        /// </summary>
        private BitmapImage ExtractIconFromPath(string exePath)
        {
            try
            {
                if (string.IsNullOrEmpty(exePath) || !System.IO.File.Exists(exePath))
                    return null;

                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon != null)
                {
                    using (var bmp = icon.ToBitmap())
                    using (var memStream = new MemoryStream())
                    {
                        bmp.Save(memStream, System.Drawing.Imaging.ImageFormat.Png);
                        memStream.Position = 0;

                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = memStream;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        // Icon extracted from player exe
                        return bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: ExtractIconFromPath failed - {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets timeline info (position, duration) for current media
        /// </summary>
        public void GetTimelineInfo(out TimeSpan position, out TimeSpan duration)
        {
            position = TimeSpan.Zero;
            duration = TimeSpan.Zero;

            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    // Try legacy WMP
                    _legacyService?.GetTimelineInfo(out position, out duration);
                    return;
                }

                var timeline = session.GetTimelineProperties();
                if (timeline == null) return;

                duration = timeline.EndTime - timeline.StartTime;
                position = timeline.Position - timeline.StartTime;

                // SMTC reports the position as of LastUpdatedTime, which providers refresh only
                // every few seconds (sometimes much later). Bring it to now while playing.
                var playback = session.GetPlaybackInfo();
                if (playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing &&
                    timeline.LastUpdatedTime > DateTimeOffset.MinValue)
                {
                    var elapsed = DateTimeOffset.Now - timeline.LastUpdatedTime;
                    if (elapsed > TimeSpan.Zero)
                    {
                        position += TimeSpan.FromTicks((long)(elapsed.Ticks * (playback.PlaybackRate ?? 1.0)));
                    }
                }

                if (position < TimeSpan.Zero) position = TimeSpan.Zero;
                if (duration > TimeSpan.Zero && position > duration) position = duration;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: GetTimelineInfo failed - {ex.Message}");
            }
        }

        /// <summary>
        /// Gets shuffle/repeat state
        /// </summary>
        public void GetPlaybackControls(out bool? isShuffleEnabled, out bool? isRepeatEnabled, out bool shuffleSupported, out bool repeatSupported)
        {
            isShuffleEnabled = null;
            isRepeatEnabled = null;
            shuffleSupported = false;
            repeatSupported = false;

            try
            {
                var session = GetCurrentSession();
                if (session == null) return;

                var playbackInfo = session.GetPlaybackInfo();
                if (playbackInfo == null) return;

                var controls = playbackInfo.Controls;
                shuffleSupported = controls.IsShuffleEnabled;
                repeatSupported = controls.IsRepeatEnabled;

                isShuffleEnabled = playbackInfo.IsShuffleActive;

                // Convert repeat mode to simple bool
                if (playbackInfo.AutoRepeatMode.HasValue)
                {
                    isRepeatEnabled = playbackInfo.AutoRepeatMode.Value != Windows.Media.MediaPlaybackAutoRepeatMode.None;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: GetPlaybackControls failed - {ex.Message}");
            }
        }

        /// <summary>
        /// Toggle shuffle mode
        /// </summary>
        public void ToggleShuffle()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null) return;

                var playbackInfo = session.GetPlaybackInfo();
                bool currentShuffle = playbackInfo?.IsShuffleActive ?? false;
                var _ = session.TryChangeShuffleActiveAsync(!currentShuffle);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: ToggleShuffle failed - {ex.Message}");
            }
        }

        /// <summary>
        /// Toggle repeat mode (None -> All -> None)
        /// </summary>
        public void ToggleRepeat()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null) return;

                var playbackInfo = session.GetPlaybackInfo();
                var currentMode = playbackInfo?.AutoRepeatMode ?? Windows.Media.MediaPlaybackAutoRepeatMode.None;

                // Cycle: None -> List -> Track -> None
                Windows.Media.MediaPlaybackAutoRepeatMode newMode;
                switch (currentMode)
                {
                    case Windows.Media.MediaPlaybackAutoRepeatMode.None:
                        newMode = Windows.Media.MediaPlaybackAutoRepeatMode.List;
                        break;
                    case Windows.Media.MediaPlaybackAutoRepeatMode.List:
                        newMode = Windows.Media.MediaPlaybackAutoRepeatMode.Track;
                        break;
                    default:
                        newMode = Windows.Media.MediaPlaybackAutoRepeatMode.None;
                        break;
                }

                var _ = session.TryChangeAutoRepeatModeAsync(newMode);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: ToggleRepeat failed - {ex.Message}");
            }
        }

        /// <summary>
        /// Get current repeat mode (0=None, 1=List, 2=Track)
        /// </summary>
        public int GetRepeatMode()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null) return 0;

                var playbackInfo = session.GetPlaybackInfo();
                var mode = playbackInfo?.AutoRepeatMode ?? Windows.Media.MediaPlaybackAutoRepeatMode.None;

                switch (mode)
                {
                    case Windows.Media.MediaPlaybackAutoRepeatMode.List: return 1;
                    case Windows.Media.MediaPlaybackAutoRepeatMode.Track: return 2;
                    default: return 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Seek to position
        /// </summary>
        public async Task<bool> SeekToAsync(TimeSpan position)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    _legacyService?.SeekTo(position);
                    return _legacyService != null;
                }

                return await session.TryChangePlaybackPositionAsync(position.Ticks);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: SeekTo failed - {ex.Message}");
                _legacyService?.SeekTo(position);
                return false;
            }
        }

    }
}
