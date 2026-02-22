using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
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

        private MediaSessionService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
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
            Trace.WriteLine($"MediaSessionService: Legacy playback changed - isPlaying={isPlaying}");
            
            // Only use legacy if SMTC has no playing session
            if (!CheckIfAnyMediaPlaying())
            {
                bool wasPlaying = _isMediaPlaying;
                _isMediaPlaying = isPlaying;

                if (wasPlaying != _isMediaPlaying)
                {
                    Trace.WriteLine($"MediaSessionService: Using legacy player state: {_isMediaPlaying}");
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
                Trace.WriteLine("MediaSessionService: Legacy track changed");
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
                        
                        // Subscribe to existing sessions
                        SubscribeToAllSessions();
                        
                        // Check initial state
                        UpdatePlaybackState();
                        
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
            SubscribeToAllSessions();
            UpdatePlaybackState();
        }

        private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            Trace.WriteLine("MediaSessionService: Sessions changed");
            SubscribeToAllSessions();
            UpdatePlaybackState();
        }

        private void SubscribeToAllSessions()
        {
            try
            {
                var sessions = _sessionManager?.GetSessions();
                if (sessions == null) return;

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
            UpdatePlaybackState();
        }

        private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                TimelineChanged?.Invoke();
            }));
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            Trace.WriteLine("MediaSessionService: Media properties changed (track change)");
            _dispatcher.BeginInvoke(new Action(() =>
            {
                MediaTrackChanged?.Invoke();
            }));
        }

        private void UpdatePlaybackState()
        {
            try
            {
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
            try
            {
                var sessions = _sessionManager?.GetSessions();
                if (sessions == null) return false;

                foreach (var session in sessions)
                {
                    try
                    {
                        var playbackInfo = session.GetPlaybackInfo();
                        if (playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                        {
                            // Get source app info for debugging
                            var sourceAppId = session.SourceAppUserModelId;
                            Trace.WriteLine($"MediaSessionService: Media playing from {sourceAppId}");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"MediaSessionService: Error checking session - {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: Error in CheckIfAnyMediaPlaying - {ex.Message}");
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
        /// Gets the current media session (if any)
        /// </summary>
        private GlobalSystemMediaTransportControlsSession GetCurrentSession()
        {
            try
            {
                return _sessionManager?.GetCurrentSession();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: Error getting current session - {ex.Message}");
                return null;
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
                var waitHandle = new System.Threading.ManualResetEventSlim(false);
                operation.Completed = (info, status) =>
                {
                    if (status == AsyncStatus.Completed)
                        mediaProps = info.GetResults();
                    waitHandle.Set();
                };

                // Wait briefly for result with proper signaling
                waitHandle.Wait(TimeSpan.FromMilliseconds(100));

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
        /// Gets the album art/thumbnail for the currently playing media
        /// </summary>
        public BitmapImage GetCurrentThumbnail()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    Trace.WriteLine("MediaSessionService: GetCurrentThumbnail - No SMTC session, trying legacy...");
                    return GetLegacyPlayerIcon();
                }

                // Get media properties
                var propsOp = session.TryGetMediaPropertiesAsync();
                var propsWait = new System.Threading.ManualResetEventSlim(false);
                propsOp.Completed = (_, __) => propsWait.Set();
                if (propsOp.Status == AsyncStatus.Started)
                    propsWait.Wait(TimeSpan.FromMilliseconds(2000));

                if (propsOp.Status != AsyncStatus.Completed)
                {
                    Trace.WriteLine($"MediaSessionService: GetCurrentThumbnail - Props failed: {propsOp.Status}");
                    return GetLegacyPlayerIcon();
                }

                var mediaProps = propsOp.GetResults();
                if (mediaProps == null)
                {
                    Trace.WriteLine("MediaSessionService: GetCurrentThumbnail - No media props");
                    return GetLegacyPlayerIcon();
                }

                if (mediaProps.Thumbnail == null)
                {
                    Trace.WriteLine("MediaSessionService: GetCurrentThumbnail - No thumbnail in props, trying legacy...");
                    return GetLegacyPlayerIcon();
                }

                Trace.WriteLine("MediaSessionService: GetCurrentThumbnail - Opening thumbnail stream...");

                // Open the thumbnail stream
                var streamOp = mediaProps.Thumbnail.OpenReadAsync();
                var streamWait = new System.Threading.ManualResetEventSlim(false);
                streamOp.Completed = (_, __) => streamWait.Set();
                if (streamOp.Status == AsyncStatus.Started)
                    streamWait.Wait(TimeSpan.FromMilliseconds(2000));

                if (streamOp.Status != AsyncStatus.Completed)
                {
                    Trace.WriteLine($"MediaSessionService: GetCurrentThumbnail - Stream open failed: {streamOp.Status}");
                    return GetLegacyPlayerIcon();
                }

                var stream = streamOp.GetResults();
                if (stream == null || stream.Size == 0)
                {
                    Trace.WriteLine("MediaSessionService: GetCurrentThumbnail - Stream is null or empty");
                    return GetLegacyPlayerIcon();
                }

                Trace.WriteLine($"MediaSessionService: GetCurrentThumbnail - Stream size: {stream.Size}");

                // Read the stream
                var size = (uint)stream.Size;
                var buffer = new Windows.Storage.Streams.Buffer(size);

                var readOp = stream.ReadAsync(buffer, size, InputStreamOptions.None);
                var readWait = new System.Threading.ManualResetEventSlim(false);
                readOp.Completed = (_, __) => readWait.Set();
                if (readOp.Status == AsyncStatus.Started)
                    readWait.Wait(TimeSpan.FromMilliseconds(2000));

                if (readOp.Status != AsyncStatus.Completed)
                {
                    Trace.WriteLine($"MediaSessionService: GetCurrentThumbnail - Read failed: {readOp.Status}");
                    return GetLegacyPlayerIcon();
                }

                var readBuffer = readOp.GetResults();
                if (readBuffer == null || readBuffer.Length == 0)
                {
                    Trace.WriteLine("MediaSessionService: GetCurrentThumbnail - readBuffer is null or empty");
                    return GetLegacyPlayerIcon();
                }

                Trace.WriteLine($"MediaSessionService: GetCurrentThumbnail - Read {readBuffer.Length} bytes");

                var bytes = new byte[readBuffer.Length];
                var dataReader = DataReader.FromBuffer(readBuffer);
                dataReader.ReadBytes(bytes);

                using (var memStream = new MemoryStream(bytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = memStream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    Trace.WriteLine($"MediaSessionService: GetCurrentThumbnail - Success: {bitmap.PixelWidth}x{bitmap.PixelHeight}");
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

                Trace.WriteLine($"MediaSessionService: GetLegacyPlayerIcon - SourceAppId: {sourceAppId}");

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

                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
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

                        Trace.WriteLine($"MediaSessionService: ExtractIconFromPath - Got icon from {exePath}");
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

                position = timeline.Position;
                duration = timeline.EndTime - timeline.StartTime;
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
        public void SeekTo(TimeSpan position)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    _legacyService?.SeekTo(position);
                    return;
                }

                var _ = session.TryChangePlaybackPositionAsync(position.Ticks);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionService: SeekTo failed - {ex.Message}");
                _legacyService?.SeekTo(position);
            }
        }

        /// <summary>
        /// Check if legacy WMP is playing (for fallback)
        /// </summary>
        public bool IsLegacyMediaPlaying => _legacyService?.IsPlaying ?? false;
    }
}
