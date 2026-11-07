using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace EarTrumpet.UI.Controls
{
    /// <summary>
    /// Volume tick sound. The MediaPlayer lives on its own STA thread: creating one costs about
    /// 40 ms and opening the file another 20-50 ms, which used to freeze the slider on the first
    /// tick of every launch when it ran on the UI thread. The UI thread now only posts requests.
    ///
    /// Opening the file is what creates BetterTrumpet's own audio session (and therefore its
    /// row in the flyout), so <see cref="Prewarm"/> creates the player but never opens it.
    /// </summary>
    internal static class TickSoundPlayer
    {
        // UI thread only.
        private static Dispatcher _dispatcher;
        private static bool _failed;
        private static readonly Dictionary<TickSoundKind, byte[]> _wavs = new Dictionary<TickSoundKind, byte[]>();

        // Tick thread only.
        private static MediaPlayer _player;
        private static TickSoundKind? _openKind;

        /// <summary>Starts the sound thread and creates the player there. Call from the UI thread.</summary>
        public static void Prewarm()
        {
            EnsureDispatcher();
        }

        /// <summary>Plays the selected tick at the slider volume (0-100). Call from the UI thread.</summary>
        public static void Play(double volume)
        {
            Play(volume, App.Settings?.TickSound ?? TickSoundKind.Classic);
        }

        /// <summary>Plays a given tick, e.g. to preview it in settings. Call from the UI thread.</summary>
        public static void Play(double volume, TickSoundKind kind)
        {
            var dispatcher = EnsureDispatcher();
            var wav = GetWav(kind);
            if (dispatcher == null || wav == null)
            {
                return;
            }

            dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() => PlayOnSoundThread(volume, kind, wav)));
        }

        public static void Shutdown()
        {
            _dispatcher?.BeginInvokeShutdown(DispatcherPriority.Normal);
            _dispatcher = null;
        }

        private static string GetResourceUri(TickSoundKind kind)
        {
            switch (kind)
            {
                case TickSoundKind.Crystal: return "pack://application:,,,/Assets/tick-crystal.wav";
                case TickSoundKind.Low: return "pack://application:,,,/Assets/tick-low.wav";
                case TickSoundKind.Muffled: return "pack://application:,,,/Assets/tick-muffled.wav";
                case TickSoundKind.Sharp: return "pack://application:,,,/Assets/tick-sharp.wav";
                default: return "pack://application:,,,/Assets/tick.wav";
            }
        }

        // Pack resources are read on the UI thread; each sound is a few KB, read once.
        private static byte[] GetWav(TickSoundKind kind)
        {
            if (_wavs.TryGetValue(kind, out var cached))
            {
                return cached;
            }

            byte[] wav = null;
            try
            {
                var resource = Application.GetResourceStream(new Uri(GetResourceUri(kind)));
                if (resource != null)
                {
                    using (var stream = resource.Stream)
                    using (var buffer = new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        wav = buffer.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"TickSoundPlayer: failed to read {kind} — {ex.Message}");
            }

            _wavs[kind] = wav;
            return wav;
        }

        private static Dispatcher EnsureDispatcher()
        {
            if (_dispatcher != null || _failed)
            {
                return _dispatcher;
            }

            try
            {
                Dispatcher created = null;
                using (var ready = new ManualResetEventSlim())
                {
                    var thread = new Thread(() =>
                    {
                        created = Dispatcher.CurrentDispatcher;
                        ready.Set();
                        Dispatcher.Run();
                    })
                    {
                        IsBackground = true,
                        Name = "BetterTrumpet tick sound",
                    };
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();

                    // Only the thread start; the player is created asynchronously below.
                    if (!ready.Wait(TimeSpan.FromSeconds(2)) || created == null)
                    {
                        _failed = true;
                        Trace.WriteLine("TickSoundPlayer: sound thread did not start");
                        return null;
                    }
                }

                _dispatcher = created;
                _dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)EnsurePlayer);
                return _dispatcher;
            }
            catch (Exception ex)
            {
                _failed = true;
                Trace.WriteLine($"TickSoundPlayer: failed to start — {ex.Message}");
                return null;
            }
        }

        private static void EnsurePlayer()
        {
            if (_player == null)
            {
                _player = new MediaPlayer();
                _player.MediaFailed += (s, e) => Trace.WriteLine($"TickSoundPlayer: media failed — {e.ErrorException?.Message}");
            }
        }

        private static void PlayOnSoundThread(double volume, TickSoundKind kind, byte[] wav)
        {
            try
            {
                EnsurePlayer();
                if (_openKind != kind)
                {
                    // MediaPlayer can't read from pack:// URIs directly, so play a temp copy.
                    var tempPath = Path.Combine(Path.GetTempPath(), $"bettertrumpet_tick_{kind.ToString().ToLowerInvariant()}.wav");
                    File.WriteAllBytes(tempPath, wav);
                    _player.Open(new Uri(tempPath, UriKind.Absolute));
                    _openKind = kind;
                }

                // A minimum volume of 0.1 keeps it audible at low levels.
                _player.Volume = Math.Max(0.1, Math.Min(1.0, volume / 100.0));
                _player.Position = TimeSpan.Zero;
                _player.Play();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"TickSoundPlayer: failed to play — {ex.Message}");
            }
        }
    }
}
