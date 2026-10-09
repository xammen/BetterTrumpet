using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>Device shapes the tray icon can show when it reflects the default output (#75).</summary>
    public enum TrayDeviceGlyph
    {
        Speaker,
        Headphones,
        Monitor,
    }

    /// <summary>
    /// Generates animated volume icons dynamically with high-quality anti-aliased rendering.
    /// Uses supersampling (4x) for crisp edges at small sizes.
    /// </summary>
    public class VolumeIconGenerator : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private readonly int _iconSize;
        private readonly int _renderSize; // 4x supersampling
        private readonly int _totalFrames;
        private readonly Icon[] _frames;
        private readonly Icon[] _framesLight;
        private readonly int[] _canonicalFrames;
        private bool _disposed;

        // Playback animations of the device glyphs (#75): rendered on first use, cached per glyph
        // and theme. Poses are held for two 50 ms ticks, so the shell gets ~10 updates per second.
        private const int HeadphonesAnimationFrames = 48; // 2.4 s loop
        private const int MonitorAnimationFrames = 16;    // 0.8 s loop, like the speaker
        private const int DeviceAnimationStep = 2;
        private readonly Dictionary<(TrayDeviceGlyph, bool), Icon[]> _deviceFrames = new Dictionary<(TrayDeviceGlyph, bool), Icon[]>();

        // Animation timing
        private const int WAVE1_START = 0;
        private const int WAVE1_END = 6;
        private const int WAVE2_START = 3;
        private const int WAVE2_END = 10;

        public int FrameCount => _totalFrames;

        public VolumeIconGenerator(int iconSize, int totalFrames = 14)
        {
            _iconSize = iconSize;
            _renderSize = iconSize * 4; // 4x supersampling for quality
            _totalFrames = totalFrames;
            _frames = new Icon[totalFrames];
            _framesLight = new Icon[totalFrames];
            _canonicalFrames = new int[totalFrames];

            GenerateAllFrames();
        }

        private void GenerateAllFrames()
        {
            for (int i = 0; i < _totalFrames; i++)
            {
                float wave1Opacity = CalculateWaveOpacity(i, WAVE1_START, WAVE1_END);
                float wave2Opacity = CalculateWaveOpacity(i, WAVE2_START, WAVE2_END);

                _frames[i] = GenerateFrame(wave1Opacity, wave2Opacity, false);
                _framesLight[i] = GenerateFrame(wave1Opacity, wave2Opacity, true);

                // Frames past the end of both wave fades are pixel-identical; remember the
                // first frame with the same opacities so callers can skip redundant updates.
                _canonicalFrames[i] = i;
                for (int j = 0; j < i; j++)
                {
                    if (CalculateWaveOpacity(j, WAVE1_START, WAVE1_END) == wave1Opacity &&
                        CalculateWaveOpacity(j, WAVE2_START, WAVE2_END) == wave2Opacity)
                    {
                        _canonicalFrames[i] = j;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the lowest frame index that renders exactly like <paramref name="frameIndex"/>.
        /// </summary>
        public int GetCanonicalFrame(int frameIndex)
        {
            if (frameIndex < 0 || frameIndex >= _totalFrames)
                frameIndex = 0;

            return _canonicalFrames[frameIndex];
        }

        private float CalculateWaveOpacity(int frame, int startFrame, int endFrame)
        {
            if (frame < startFrame) return 0f;
            if (frame >= endFrame) return 1f;

            float progress = (float)(frame - startFrame) / (endFrame - startFrame);
            return EaseOutCubic(progress);
        }

        private float EaseOutCubic(float t)
        {
            return 1f - (float)Math.Pow(1 - t, 3);
        }

        private Icon GenerateFrame(float wave1Opacity, float wave2Opacity, bool lightTheme)
        {
            return RenderIcon(lightTheme, (g, scale, mainColor) =>
            {
                DrawSpeaker(g, scale, mainColor);

                if (wave1Opacity > 0)
                {
                    DrawWave1(g, scale, Color.FromArgb((int)(255 * wave1Opacity), mainColor));
                }

                if (wave2Opacity > 0)
                {
                    DrawWave2(g, scale, Color.FromArgb((int)(255 * wave2Opacity), mainColor));
                }
            });
        }

        /// <summary>
        /// Draws in a 24-unit (SVG-like) space at 4x, then downsamples to the tray size.
        /// </summary>
        private Icon RenderIcon(bool lightTheme, Action<Graphics, float, Color> draw)
        {
            // Render at high resolution
            using (var highRes = new Bitmap(_renderSize, _renderSize, PixelFormat.Format32bppArgb))
            {
                highRes.SetResolution(96, 96);

                using (var g = Graphics.FromImage(highRes))
                {
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.Clear(Color.Transparent);

                    float scale = _renderSize / 24f;
                    Color mainColor = lightTheme ? Color.FromArgb(20, 20, 20) : Color.White;
                    draw(g, scale, mainColor);
                }

                // Downsample to target size with high quality
                using (var final = new Bitmap(_iconSize, _iconSize, PixelFormat.Format32bppArgb))
                {
                    final.SetResolution(96, 96);

                    using (var g = Graphics.FromImage(final))
                    {
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.Clear(Color.Transparent);

                        g.DrawImage(highRes, 0, 0, _iconSize, _iconSize);
                    }

                    IntPtr hIcon = final.GetHicon();
                    Icon icon = Icon.FromHandle(hIcon);
                    Icon ownedIcon = (Icon)icon.Clone();
                    DestroyIcon(hIcon);
                    return ownedIcon;
                }
            }
        }

        private void DrawSpeaker(Graphics g, float scale, Color color)
        {
            float strokeWidth = 2f * scale;

            using (var brush = new SolidBrush(color))
            using (var path = new GraphicsPath())
            {
                // Recreate the exact SVG path:
                // M11 4.702a.705.705 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.705.705 0 0 0 11 19.298z

                // Simplified speaker shape with precise coordinates
                PointF[] speakerShape = new PointF[]
                {
                    new PointF(11f * scale, 4.7f * scale),    // Top tip of cone
                    new PointF(11f * scale, 19.3f * scale),   // Bottom tip of cone
                    new PointF(6.4f * scale, 16.5f * scale),  // Bottom inner corner
                    new PointF(5.4f * scale, 16f * scale),    // Box bottom right
                    new PointF(3f * scale, 16f * scale),      // Box bottom left
                    new PointF(2f * scale, 15f * scale),      // Box bottom left corner (rounded)
                    new PointF(2f * scale, 9f * scale),       // Box top left corner (rounded)
                    new PointF(3f * scale, 8f * scale),       // Box top left
                    new PointF(5.4f * scale, 8f * scale),     // Box top right
                    new PointF(6.4f * scale, 7.5f * scale),   // Top inner corner
                };

                path.AddPolygon(speakerShape);
                g.FillPath(brush, path);
            }
        }

        private void DrawWave1(Graphics g, float scale, Color color)
        {
            // Small wave: M16 9a5 5 0 0 1 0 6
            // Arc centered around x=16, from y=9 to y=15, radius ~3
            float strokeWidth = 2f * scale;

            using (var pen = new Pen(color, strokeWidth))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                // The arc is on the right side of center point
                float centerX = 13f * scale;
                float centerY = 12f * scale;
                float radius = 3f * scale;

                RectangleF arcRect = new RectangleF(
                    centerX - radius,
                    centerY - radius,
                    radius * 2,
                    radius * 2
                );

                // Draw arc from -50 to 50 degrees (right side arc)
                g.DrawArc(pen, arcRect, -50, 100);
            }
        }

        private void DrawWave2(Graphics g, float scale, Color color)
        {
            // Large wave: M19.364 18.364a9 9 0 0 0 0-12.728
            float strokeWidth = 2f * scale;

            using (var pen = new Pen(color, strokeWidth))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                float centerX = 13f * scale;
                float centerY = 12f * scale;
                float radius = 6.5f * scale;

                RectangleF arcRect = new RectangleF(
                    centerX - radius,
                    centerY - radius,
                    radius * 2,
                    radius * 2
                );

                g.DrawArc(pen, arcRect, -55, 110);
            }
        }

        public Icon GetFrame(int frameIndex, bool lightTheme)
        {
            if (frameIndex < 0 || frameIndex >= _totalFrames)
                frameIndex = 0;

            var sourceIcon = lightTheme ? _framesLight[frameIndex] : _frames[frameIndex];
            return (Icon)sourceIcon.Clone();
        }

        public Icon GetStaticIcon(bool lightTheme, bool showWaves = true)
        {
            if (showWaves)
            {
                return GetFrame(_totalFrames - 1, lightTheme);
            }
            else
            {
                return GenerateFrame(0, 0, lightTheme);
            }
        }

        public Icon GetMutedIcon(bool lightTheme)
        {
            return RenderIcon(lightTheme, (g, scale, mainColor) =>
            {
                DrawSpeaker(g, scale, mainColor);
                DrawMuteX(g, scale, mainColor);
            });
        }

        /// <summary>
        /// Static icon for the default output's kind. Non-speaker shapes are muted with a
        /// cut-out slash, since there is no room for the speaker's side X next to them.
        /// </summary>
        public Icon GetDeviceIcon(TrayDeviceGlyph glyph, bool muted, bool lightTheme)
        {
            if (glyph == TrayDeviceGlyph.Speaker)
            {
                return muted ? GetMutedIcon(lightTheme) : GetStaticIcon(lightTheme);
            }

            return RenderIcon(lightTheme, (g, scale, mainColor) =>
            {
                ScaleDeviceGlyph(g, scale);

                switch (glyph)
                {
                    case TrayDeviceGlyph.Headphones:
                        DrawHeadphones(g, scale, mainColor);
                        break;
                    case TrayDeviceGlyph.Monitor:
                        DrawMonitor(g, scale, mainColor);
                        break;
                }

                if (muted)
                {
                    DrawSlash(g, scale, mainColor);
                }
            });
        }

        /// <summary>Frames in one playback loop of the given glyph.</summary>
        public int GetFrameCount(TrayDeviceGlyph glyph)
        {
            switch (glyph)
            {
                case TrayDeviceGlyph.Headphones: return HeadphonesAnimationFrames;
                case TrayDeviceGlyph.Monitor: return MonitorAnimationFrames;
                default: return _totalFrames;
            }
        }

        /// <summary>Lowest frame index that renders exactly like <paramref name="frameIndex"/>.</summary>
        public int GetCanonicalFrame(TrayDeviceGlyph glyph, int frameIndex)
        {
            if (glyph == TrayDeviceGlyph.Speaker)
            {
                return GetCanonicalFrame(frameIndex);
            }

            if (frameIndex < 0 || frameIndex >= GetFrameCount(glyph))
                frameIndex = 0;

            return frameIndex / DeviceAnimationStep * DeviceAnimationStep;
        }

        /// <summary>
        /// Playback animation frame: speaker waves, a waveform scrolling inside the headphones, or an
        /// equalizer on the monitor.
        /// </summary>
        public Icon GetFrame(TrayDeviceGlyph glyph, int frameIndex, bool lightTheme)
        {
            if (glyph == TrayDeviceGlyph.Speaker)
            {
                return GetFrame(frameIndex, lightTheme);
            }

            int pose = GetCanonicalFrame(glyph, frameIndex);
            if (!_deviceFrames.TryGetValue((glyph, lightTheme), out var frames))
            {
                frames = new Icon[GetFrameCount(glyph)];
                _deviceFrames[(glyph, lightTheme)] = frames;
            }

            if (frames[pose] == null)
            {
                frames[pose] = RenderIcon(lightTheme, (g, scale, mainColor) =>
                {
                    ScaleDeviceGlyph(g, scale);
                    if (glyph == TrayDeviceGlyph.Headphones)
                    {
                        DrawHeadphones(g, scale, mainColor);
                        DrawScope(g, scale, mainColor, pose);
                    }
                    else
                    {
                        DrawMonitor(g, scale, mainColor);
                        DrawEqualizer(g, scale, mainColor, pose);
                    }
                });
            }

            return (Icon)frames[pose].Clone();
        }

        // These shapes span the full 24-unit box; at 90% they weigh about the same as the
        // speaker next to other tray icons.
        private static void ScaleDeviceGlyph(Graphics g, float scale)
        {
            g.TranslateTransform(12f * scale, 12f * scale);
            g.ScaleTransform(0.9f, 0.9f);
            g.TranslateTransform(-12f * scale, -12f * scale);
        }

        private static void DrawHeadphones(Graphics g, float scale, Color color)
        {
            using (var pen = new Pen(color, 2f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var brush = new SolidBrush(color))
            {
                // Headband: top half of a 9-unit circle, ending inside the filled cups.
                g.DrawArc(pen, 3f * scale, 2f * scale, 18f * scale, 18f * scale, 180, 180);

                FillRoundedRect(g, brush, 2f * scale, 10.5f * scale, 6f * scale, 9.5f * scale, 2f * scale);
                FillRoundedRect(g, brush, 16f * scale, 10.5f * scale, 6f * scale, 9.5f * scale, 2f * scale);
            }
        }

        // A short waveform scrolling inside the arch, between the cups: the headphones keep their
        // full size and silhouette (notes or waves around them forced a shrink that made the
        // headphones unreadable at 16 px). Three wave cycles scroll per loop; the amplitude
        // breathes with a music-like level, and the ends taper so it never touches the cups.
        private static void DrawScope(Graphics g, float scale, Color color, int frame)
        {
            double loop = frame / (double)HeadphonesAnimationFrames * Math.PI * 2;
            double level = 0.55 + 0.25 * Math.Sin(loop * 3 + 1.1) + 0.15 * Math.Sin(loop * 7 + 1.87) + 0.08 * Math.Sin(loop * 13 + 0.66);
            double amplitude = 2.4 * (0.6 + 0.4 * Math.Max(0, Math.Min(1, level)));
            double phase = loop * 3;

            var points = new List<PointF>();
            for (float x = 9.2f; x <= 14.8f + 0.001f; x += 0.35f)
            {
                double u = (x - 9.2) / 5.6;
                double y = 14.6 - amplitude * Math.Sin(u * Math.PI) * Math.Sin(u * Math.PI * 3 - phase);
                points.Add(new PointF(x * scale, (float)y * scale));
            }

            using (var pen = new Pen(color, 1.6f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawLines(pen, points.ToArray());
            }
        }

        // Four phase-shifted bars bouncing inside the screen; one sine cycle per loop.
        private static void DrawEqualizer(Graphics g, float scale, Color color, int frame)
        {
            double[] phases = { 0, 2.1, 4.2, 1.0 };
            float[] xs = { 7.5f, 10.5f, 13.5f, 16.5f };
            double t = frame / (double)MonitorAnimationFrames * Math.PI * 2;

            using (var pen = new Pen(color, 1.8f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                for (int i = 0; i < xs.Length; i++)
                {
                    float height = 1f + 5f * (float)(0.5 + 0.5 * Math.Sin(t + phases[i]));
                    g.DrawLine(pen, xs[i] * scale, 13.4f * scale, xs[i] * scale, (13.4f - height) * scale);
                }
            }
        }

        private static void DrawMonitor(Graphics g, float scale, Color color)
        {
            using (var pen = new Pen(color, 2f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var screen = RoundedRect(2.5f * scale, 4f * scale, 19f * scale, 12.5f * scale, 2f * scale))
            {
                g.DrawPath(pen, screen);
                g.DrawLine(pen, 12f * scale, 16.5f * scale, 12f * scale, 20.5f * scale);
                g.DrawLine(pen, 8f * scale, 20.5f * scale, 16f * scale, 20.5f * scale);
            }
        }

        private static void DrawSlash(Graphics g, float scale, Color color)
        {
            // Erase a gap around the slash first so it reads over filled shapes too.
            var previous = g.CompositingMode;
            g.CompositingMode = CompositingMode.SourceCopy;
            using (var eraser = new Pen(Color.Transparent, 4f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(eraser, 3f * scale, 3f * scale, 21f * scale, 21f * scale);
            }
            g.CompositingMode = previous;

            using (var pen = new Pen(color, 2f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, 3f * scale, 3f * scale, 21f * scale, 21f * scale);
            }
        }

        private static void FillRoundedRect(Graphics g, Brush brush, float x, float y, float width, float height, float radius)
        {
            using (var path = RoundedRect(x, y, width, height, radius))
            {
                g.FillPath(brush, path);
            }
        }

        private static GraphicsPath RoundedRect(float x, float y, float width, float height, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(width, height));
            var path = new GraphicsPath();
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + width - d, y, d, d, 270, 90);
            path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
            path.AddArc(x, y + height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawMuteX(Graphics g, float scale, Color color)
        {
            float strokeWidth = 2f * scale;

            using (var pen = new Pen(color, strokeWidth))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                // X mark: (16,9) to (22,15) and (16,15) to (22,9)
                g.DrawLine(pen, 16f * scale, 9f * scale, 22f * scale, 15f * scale);
                g.DrawLine(pen, 16f * scale, 15f * scale, 22f * scale, 9f * scale);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var icon in _frames)
            {
                icon?.Dispose();
            }

            foreach (var icon in _framesLight)
            {
                icon?.Dispose();
            }

            foreach (var frames in _deviceFrames.Values)
            {
                foreach (var icon in frames)
                {
                    icon?.Dispose();
                }
            }
            _deviceFrames.Clear();
        }
    }
}
