using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace EarTrumpet.Interop.Helpers
{
    /// <summary>
    /// Named pipe client for sending CLI commands to a running BetterTrumpet instance.
    /// Uses raw byte I/O to avoid StreamReader buffering issues on pipes.
    /// </summary>
    public static class PipeClient
    {
        /// <summary>
        /// Send a command to the running instance and return the response.
        /// Returns null if the server is not running or times out.
        /// </summary>
        public static string SendCommand(string command, int timeoutMs = 5000)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeServer.PipeName, PipeDirection.InOut, PipeServer.Options))
                {
                    client.Connect(timeoutMs);

                    // Write command + newline
                    var commandBytes = Encoding.UTF8.GetBytes(command + "\n");
                    client.Write(commandBytes, 0, commandBytes.Length);
                    client.Flush();

                    // Read response (byte-by-byte to avoid StreamReader buffer blocking)
                    return PipeTextProtocol.ReadLineUtf8(client);
                }
            }
            catch (TimeoutException)
            {
                Trace.WriteLine("PipeClient: Connection timed out");
                return null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"PipeClient: Error - {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if a BetterTrumpet instance is running and accepting pipe commands.
        /// </summary>
        public static bool IsServerRunning()
        {
            var result = SendCommand("ping", 1000);
            return result != null;
        }

    }

    internal static class PipeTextProtocol
    {
        /// <summary>
        /// Reads one newline-delimited UTF-8 message without StreamReader buffering.
        /// </summary>
        internal static string ReadLineUtf8(Stream stream)
        {
            using (var bytes = new MemoryStream())
            {
                while (true)
                {
                    var value = stream.ReadByte();
                    if (value == -1 || value == '\n') break;
                    if (value == '\r') continue;
                    bytes.WriteByte((byte)value);
                }

                return Encoding.UTF8.GetString(bytes.GetBuffer(), 0, checked((int)bytes.Length));
            }
        }
    }
}
