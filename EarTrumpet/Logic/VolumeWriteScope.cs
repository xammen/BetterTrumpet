using System;
using System.Threading;

namespace EarTrumpet.Logic
{
    /// <summary>
    /// Marks volume writes that should not persist as the user's last RDP volume.
    /// Uses Interlocked rather than ThreadStatic: WASAPI property callbacks can
    /// arrive on a different thread than the dispatcher write.
    /// </summary>
    public static class VolumeWriteScope
    {
        private static int _depth;

        public static bool IsActive
        {
            get { return Volatile.Read(ref _depth) > 0; }
        }

        public static IDisposable Begin()
        {
            Interlocked.Increment(ref _depth);
            return new Releaser();
        }

        private sealed class Releaser : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                Interlocked.Decrement(ref _depth);
            }
        }
    }
}
