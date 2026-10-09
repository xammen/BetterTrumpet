using System;
using System.Diagnostics;

namespace EarTrumpet.Interop.Helpers
{
    public class ProcessHelper
    {
        public  static void StartNoThrow(string fileName)
        {
            try
            {
                Trace.WriteLine($"ProcessHelper StartNoThrow {fileName}");
                using (Process.Start(fileName))
                {
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"ProcessHelper StartNoThrow Failed: {ex}");
            }
        }

        // Web pages (settings bundle, remote what's-new feed) may only open https links:
        // anything else (file://, UNC paths, custom protocol handlers) would let a tampered
        // feed launch arbitrary programs through ShellExecute.
        public static bool OpenWebLinkNoThrow(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.IsUnc)
            {
                Trace.WriteLine($"ProcessHelper OpenWebLinkNoThrow: blocked {url}");
                return false;
            }

            try
            {
                using (Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true }))
                {
                }
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"ProcessHelper OpenWebLinkNoThrow Failed: {ex}");
                return false;
            }
        }
    }
}
