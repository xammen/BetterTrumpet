using EarTrumpet.DataModel;
using EarTrumpet.DataModel.Audio;
using EarTrumpet.DataModel.WindowsAudio;
using EarTrumpet.UI.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EarTrumpet.UI.ViewModels
{
    public class DevicePickerViewModel : BindableBase
    {
        private const uint FF_Headphones = 3;
        private const uint FF_Headset = 5;

        // --- Flat device list (all playback devices) ---
        public ObservableCollection<DeviceViewModel> AllPlaybackDevices { get; }

        // --- Device section collapse/expand ---
        private bool _isDeviceListExpanded = true;
        public bool IsDeviceListExpanded
        {
            get => _isDeviceListExpanded;
            set
            {
                if (_isDeviceListExpanded != value)
                {
                    _isDeviceListExpanded = value;
                    RaisePropertyChanged(nameof(IsDeviceListExpanded));
                }
            }
        }

        // --- Device selection ---
        private DeviceViewModel _selectedDevice;
        public DeviceViewModel SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (_selectedDevice != value)
                {
                    _selectedDevice = value;
                    RaisePropertyChanged(nameof(SelectedDevice));
                    if (value != null)
                        value.MakeDefaultDevice();
                }
            }
        }

        // --- Master volume ---
        public int MasterVolume
        {
            get => _selectedDevice?.Volume ?? 0;
            set
            {
                if (_selectedDevice != null)
                {
                    _selectedDevice.Volume = value;
                    RaisePropertyChanged(nameof(MasterVolume));
                }
            }
        }

        public bool IsMasterMuted
        {
            get => _selectedDevice?.IsMuted ?? false;
            set
            {
                if (_selectedDevice != null)
                {
                    _selectedDevice.IsMuted = value;
                    RaisePropertyChanged(nameof(IsMasterMuted));
                }
            }
        }

        // --- Now Playing ---
        private string _mediaTitle;
        public string MediaTitle
        {
            get => _mediaTitle;
            private set { _mediaTitle = value; RaisePropertyChanged(nameof(MediaTitle)); RaisePropertyChanged(nameof(HasMedia)); }
        }

        private string _mediaArtist;
        public string MediaArtist
        {
            get => _mediaArtist;
            private set { _mediaArtist = value; RaisePropertyChanged(nameof(MediaArtist)); }
        }

        private string _mediaSource;
        public string MediaSource
        {
            get => _mediaSource;
            private set { _mediaSource = value; RaisePropertyChanged(nameof(MediaSource)); }
        }

        private BitmapImage _mediaThumbnail;
        public BitmapImage MediaThumbnail
        {
            get => _mediaThumbnail;
            private set { _mediaThumbnail = value; RaisePropertyChanged(nameof(MediaThumbnail)); }
        }

        private bool _isMediaPlaying;
        public bool IsMediaPlaying
        {
            get => _isMediaPlaying;
            private set
            {
                _isMediaPlaying = value;
                RaisePropertyChanged(nameof(IsMediaPlaying));
                RaisePropertyChanged(nameof(HasMedia));
                RaisePropertyChanged(nameof(PlayPauseGlyph));
            }
        }

        public bool HasMedia => !string.IsNullOrEmpty(_mediaTitle);
        public string PlayPauseGlyph => _isMediaPlaying ? "\xE103" : "\xE102";

        private Color _dominantColor = Color.FromRgb(60, 60, 80);
        public Color DominantColor
        {
            get => _dominantColor;
            private set { _dominantColor = value; RaisePropertyChanged(nameof(DominantColor)); }
        }

        // --- Timeline ---
        private double _mediaPosition;
        public double MediaPosition
        {
            get => _mediaPosition;
            set { _mediaPosition = value; RaisePropertyChanged(nameof(MediaPosition)); RaisePropertyChanged(nameof(MediaPositionText)); }
        }

        private double _mediaDuration;
        public double MediaDuration
        {
            get => _mediaDuration;
            set { _mediaDuration = value; RaisePropertyChanged(nameof(MediaDuration)); RaisePropertyChanged(nameof(MediaDurationText)); }
        }

        public string MediaPositionText => TimeSpan.FromSeconds(_mediaPosition).ToString(@"m\:ss");
        public string MediaDurationText => TimeSpan.FromSeconds(_mediaDuration).ToString(@"m\:ss");

        // --- Commands ---
        public ICommand PlayPauseCommand { get; }
        public ICommand NextTrackCommand { get; }
        public ICommand PreviousTrackCommand { get; }

        private readonly DeviceCollectionViewModel _deviceCollection;
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _timelineTimer;
        private int _syncCounter;

        public DevicePickerViewModel(DeviceCollectionViewModel deviceCollection)
        {
            _deviceCollection = deviceCollection;
            _dispatcher = Dispatcher.CurrentDispatcher;
            AllPlaybackDevices = new ObservableCollection<DeviceViewModel>();

            // Commands
            PlayPauseCommand = new RelayCommand(() => { try { MediaSessionService.Instance.PlayPause(); } catch { } });
            NextTrackCommand = new RelayCommand(() => { try { MediaSessionService.Instance.Next(); } catch { } });
            PreviousTrackCommand = new RelayCommand(() => { try { MediaSessionService.Instance.Previous(); } catch { } });

            _deviceCollection.AllDevices.CollectionChanged += OnDevicesChanged;
            _deviceCollection.DefaultChanged += OnDefaultChanged;

            // Media events
            try
            {
                MediaSessionService.Instance.MediaTrackChanged += OnMediaTrackChanged;
                MediaSessionService.Instance.MediaPlaybackChanged += OnMediaPlaybackChanged;
                MediaSessionService.Instance.TimelineChanged += OnTimelineChanged;
                RefreshMediaInfo();
                RefreshTimeline();
            }
            catch (Exception ex) { Trace.WriteLine($"DevicePickerViewModel: Media init failed: {ex.Message}"); }

            // Timeline tick — 1s, the code-behind handles smooth animation between ticks
            _timelineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timelineTimer.Tick += (s, e) =>
            {
                if (_isMediaPlaying && _mediaDuration > 0)
                {
                    _mediaPosition = Math.Min(_mediaPosition + 1.0, _mediaDuration);
                    RaisePropertyChanged(nameof(MediaPosition));
                    RaisePropertyChanged(nameof(MediaPositionText));
                    // Sync every 10s
                    if (++_syncCounter >= 10) { _syncCounter = 0; RefreshTimeline(); }
                }
            };
            _timelineTimer.Start();

            RebuildDeviceList();
        }

        // --- Media handlers ---
        private void OnMediaTrackChanged() => _dispatcher.BeginInvoke((Action)(() => { RefreshMediaInfo(); RefreshTimeline(); }));

        private void OnMediaPlaybackChanged(bool isPlaying) => _dispatcher.BeginInvoke((Action)(() =>
        {
            IsMediaPlaying = isPlaying;
            if (isPlaying) { RefreshMediaInfo(); RefreshTimeline(); }
        }));

        private void OnTimelineChanged() => _dispatcher.BeginInvoke((Action)RefreshTimeline);

        private void RefreshMediaInfo()
        {
            try
            {
                var info = MediaSessionService.Instance.GetCurrentMediaInfo();
                if (!string.IsNullOrEmpty(info))
                {
                    var parts = info.Split(new[] { " - " }, 2, StringSplitOptions.None);
                    if (parts.Length == 2) { MediaArtist = parts[0]; MediaTitle = parts[1]; }
                    else { MediaTitle = info; MediaArtist = ""; }
                    MediaSource = "Spotify";
                    try
                    {
                        MediaThumbnail = MediaSessionService.Instance.GetCurrentThumbnail();
                        if (MediaThumbnail != null) DominantColor = ExtractDominantColor(MediaThumbnail);
                    }
                    catch { MediaThumbnail = null; }
                }
                IsMediaPlaying = MediaSessionService.Instance.IsMediaPlaying;
            }
            catch (Exception ex) { Trace.WriteLine($"DevicePickerViewModel: RefreshMediaInfo failed: {ex.Message}"); }
        }

        private void RefreshTimeline()
        {
            try
            {
                MediaSessionService.Instance.GetTimelineInfo(out var position, out var duration);
                MediaPosition = position.TotalSeconds;
                MediaDuration = Math.Max(duration.TotalSeconds, 1);
            }
            catch { }
        }

        // --- Device handlers ---
        private void OnDevicesChanged(object sender, NotifyCollectionChangedEventArgs e) => RebuildDeviceList();

        private void OnDefaultChanged(object sender, DeviceViewModel newDefault)
        {
            _selectedDevice = newDefault;
            RaisePropertyChanged(nameof(SelectedDevice));
            RaisePropertyChanged(nameof(MasterVolume));
            RaisePropertyChanged(nameof(IsMasterMuted));
            // Notify ALL devices so their IsDefault radio dot updates
            foreach (var device in AllPlaybackDevices)
                device.NotifyIsDefaultChanged();
        }

        public void RebuildDeviceList()
        {
            try
            {
                AllPlaybackDevices.Clear();
                var kind = AudioDeviceKind.Playback.ToString();
                foreach (var device in _deviceCollection.AllDevices.Where(d => d.DeviceKind == kind))
                    AllPlaybackDevices.Add(device);

                _selectedDevice = _deviceCollection.Default;
                RaisePropertyChanged(nameof(SelectedDevice));
                RaisePropertyChanged(nameof(MasterVolume));
                RaisePropertyChanged(nameof(IsMasterMuted));
            }
            catch (Exception ex) { Trace.WriteLine($"DevicePickerViewModel RebuildDeviceList error: {ex}"); }
        }

        private static Color ExtractDominantColor(BitmapImage bitmap)
        {
            try
            {
                var resized = new TransformedBitmap(bitmap, new ScaleTransform(32.0 / bitmap.PixelWidth, 32.0 / bitmap.PixelHeight));
                var pixels = new byte[resized.PixelWidth * resized.PixelHeight * 4];
                resized.CopyPixels(pixels, resized.PixelWidth * 4, 0);

                long totalR = 0, totalG = 0, totalB = 0;
                int count = 0;
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    int b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
                    if ((r + g + b) > 60 && (r + g + b) < 680)
                    { totalR += r; totalG += g; totalB += b; count++; }
                }

                if (count > 0)
                {
                    var avgR = (byte)(totalR / count);
                    var avgG = (byte)(totalG / count);
                    var avgB = (byte)(totalB / count);
                    // Boost saturation slightly
                    var max = Math.Max(avgR, Math.Max(avgG, avgB));
                    if (max > 0)
                    {
                        var factor = 200.0 / max;
                        avgR = (byte)Math.Min(255, avgR * factor);
                        avgG = (byte)Math.Min(255, avgG * factor);
                        avgB = (byte)Math.Min(255, avgB * factor);
                    }
                    return Color.FromRgb(avgR, avgG, avgB);
                }
            }
            catch (Exception ex) { Trace.WriteLine($"ExtractDominantColor failed: {ex.Message}"); }
            return Color.FromRgb(80, 60, 120);
        }
    }
}
