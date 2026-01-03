using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Generates animated volume icons dynamically with high-quality anti-aliased rendering.
    /// Uses supersampling (4x) for crisp edges at small sizes.
    /// </summary>
    public class VolumeIconGenerator : IDisposable
    {
        private readonly int _iconSize;
        private readonly int _renderSize; // 4x supersampling
        private readonly int _totalFrames;
        private readonly Icon[] _frames;
        private readonly Icon[] _framesLight;
        private bool _disposed;

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
            }
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

                    // Draw all elements
                    DrawSpeaker(g, scale, mainColor);

                    if (wave1Opacity > 0)
                    {
                        Color wave1Color = Color.FromArgb((int)(255 * wave1Opacity), mainColor);
                        DrawWave1(g, scale, wave1Color);
                    }

                    if (wave2Opacity > 0)
                    {
                        Color wave2Color = Color.FromArgb((int)(255 * wave2Opacity), mainColor);
                        DrawWave2(g, scale, wave2Color);
                    }
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

                    return Icon.FromHandle(final.GetHicon());
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

                    DrawSpeaker(g, scale, mainColor);
                    DrawMuteX(g, scale, mainColor);
                }

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

                    return Icon.FromHandle(final.GetHicon());
                }
            }
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
        }
    }
}
