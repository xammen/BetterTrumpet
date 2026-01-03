using EarTrumpet.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Extracts frames from an animated GIF and converts them to Icons for tray animation.
    /// </summary>
    public class GifFrameExtractor : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private readonly List<Icon> _frames = new List<Icon>();
        private readonly int _targetSize;
        private bool _disposed;

        public IReadOnlyList<Icon> Frames => _frames;
        public int FrameCount => _frames.Count;
        public int FrameDelayMs { get; private set; } = 100;

        public GifFrameExtractor(string gifPath, int targetSize = 32)
        {
            _targetSize = targetSize;
            LoadGif(gifPath);
        }

        public GifFrameExtractor(Stream gifStream, int targetSize = 32)
        {
            _targetSize = targetSize;
            LoadGif(gifStream);
        }

        private void LoadGif(string gifPath)
        {
            if (!File.Exists(gifPath))
            {
                System.Diagnostics.Trace.WriteLine($"GifFrameExtractor: File not found: {gifPath}");
                return;
            }

            using (var stream = File.OpenRead(gifPath))
            {
                LoadGif(stream);
            }
        }

        private void LoadGif(Stream stream)
        {
            try
            {
                using (var gif = Image.FromStream(stream))
                {
                    var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
                    int frameCount = gif.GetFrameCount(dimension);

                    // Get frame delay from GIF metadata
                    try
                    {
                        var delayProperty = gif.GetPropertyItem(0x5100); // PropertyTagFrameDelay
                        if (delayProperty != null && delayProperty.Value.Length >= 4)
                        {
                            // Delay is in 1/100ths of a second
                            int delay = BitConverter.ToInt32(delayProperty.Value, 0) * 10;
                            if (delay > 0 && delay < 1000)
                            {
                                FrameDelayMs = delay;
                            }
                        }
                    }
                    catch
                    {
                        // Use default delay if we can't read the property
                    }

                    System.Diagnostics.Trace.WriteLine($"GifFrameExtractor: Loading {frameCount} frames, delay={FrameDelayMs}ms");

                    for (int i = 0; i < frameCount; i++)
                    {
                        gif.SelectActiveFrame(dimension, i);
                        
                        // Create a resized bitmap (32-bit ARGB for quality)
                        using (var frameBitmap = new Bitmap(gif))
                        {
                            var resized = new Bitmap(_targetSize, _targetSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            using (var g = Graphics.FromImage(resized))
                            {
                                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                
                                // Start with transparent background
                                g.Clear(Color.Transparent);
                                
                                // Draw the GIF frame
                                g.DrawImage(frameBitmap, 0, 0, _targetSize, _targetSize);
                            }

                            // Fix alpha channel - ensure non-transparent pixels are fully opaque
                            FixAlphaChannel(resized);

                            // Create icon
                            IntPtr hIcon = resized.GetHicon();
                            Icon icon = Icon.FromHandle(hIcon);
                            Icon ownedIcon = (Icon)icon.Clone();
                            _frames.Add(ownedIcon);
                            
                            DestroyIcon(hIcon);
                            resized.Dispose();
                        }
                    }

                    System.Diagnostics.Trace.WriteLine($"GifFrameExtractor: Loaded {_frames.Count} frames successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"GifFrameExtractor: Error loading GIF: {ex.Message}");
            }
        }

        /// <summary>
        /// Fixes the alpha channel of the bitmap.
        /// Pixels that are not fully transparent get their alpha set to 255 (fully opaque).
        /// This fixes the issue where white pixels appear semi-transparent.
        /// </summary>
        private void FixAlphaChannel(Bitmap bitmap)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            
            try
            {
                int bytes = Math.Abs(bitmapData.Stride) * bitmap.Height;
                byte[] pixels = new byte[bytes];
                Marshal.Copy(bitmapData.Scan0, pixels, 0, bytes);
                
                // Process each pixel (BGRA format)
                for (int i = 0; i < bytes; i += 4)
                {
                    byte alpha = pixels[i + 3];
                    
                    // If pixel has any opacity (not fully transparent), make it fully opaque
                    if (alpha > 0)
                    {
                        pixels[i + 3] = 255;
                    }
                }
                
                Marshal.Copy(pixels, 0, bitmapData.Scan0, bytes);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }

        public Icon GetFrame(int index)
        {
            if (_frames.Count == 0) return null;
            return _frames[index % _frames.Count];
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                foreach (var frame in _frames)
                {
                    frame?.Dispose();
                }
                _frames.Clear();
            }
        }
    }
}
