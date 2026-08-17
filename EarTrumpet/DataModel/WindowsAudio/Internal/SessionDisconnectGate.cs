using System.Threading;

namespace EarTrumpet.DataModel.WindowsAudio.Internal
{
    /// <summary>
    /// Process-watcher and WASAPI can both request teardown for the same session.
    /// Only the first caller should dispatch RemoveSession to the UI thread.
    /// Extracted so the Linux self-test can cover the race without WASAPI.
    /// </summary>
    static class SessionDisconnectGate
    {
        public static bool TryBeginDisconnect(ref int disconnectIssued)
        {
            return Interlocked.Exchange(ref disconnectIssued, 1) == 0;
        }
    }
}
