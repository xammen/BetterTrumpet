using System;
using System.Diagnostics;
using System.Windows.Threading;
using Windows.Foundation;
using Windows.Media.Control;

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

        /// <summary>
        /// Event fired when media playback state changes (play/pause/stop)
        /// </summary>
        public event Action<bool> MediaPlaybackChanged;

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
    }
}
