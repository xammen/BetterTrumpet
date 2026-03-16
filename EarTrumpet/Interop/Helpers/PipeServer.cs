using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace EarTrumpet.Interop.Helpers
{
    /// <summary>
    /// Named pipe server for IPC. Runs on a background thread, accepts commands
    /// from CLI clients, dispatches them, and returns JSON responses.
    /// 
    /// Protocol: newline-delimited text.
    /// Client sends: "command args...\n"
    /// Server responds: "json response\n"
    /// </summary>
    public sealed class PipeServer : IDisposable
    {
        public const string PipeName = "BetterTrumpet_IPC_v1";

        public event Func<string, string> CommandReceived;

        private Thread _serverThread;
        private volatile bool _running;

        public void Start()
        {
            if (_running) return;
            _running = true;
            _serverThread = new Thread(ServerLoop)
            {
                IsBackground = true,
                Name = "BT-PipeServer"
            };
            _serverThread.Start();
            Trace.WriteLine("PipeServer: Started");
        }

        public void Stop()
        {
            _running = false;
            // Connect to self to unblock WaitForConnection
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                {
                    client.Connect(500);
                    // Send a newline so ReadLineRaw unblocks
                    var bytes = Encoding.UTF8.GetBytes("\n");
                    client.Write(bytes, 0, bytes.Length);
                }
            }
            catch { /* Expected — just unblocking */ }
            Trace.WriteLine("PipeServer: Stopped");
        }

        private void ServerLoop()
        {
            while (_running)
            {
                NamedPipeServerStream server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        1, // max 1 concurrent connection
                        PipeTransmissionMode.Byte,
                        PipeOptions.None);

                    server.WaitForConnection();

                    if (!_running) break;

                    // Read command (byte-by-byte to avoid StreamReader buffer blocking)
                    var command = ReadLineRaw(server);
                    if (string.IsNullOrEmpty(command))
                    {
                        server.Dispose();
                        continue;
                    }

                    Trace.WriteLine($"PipeServer: Received '{command}'");

                    string response;
                    try
                    {
                        response = CommandReceived?.Invoke(command) ?? "{\"error\":\"no handler\"}";
                    }
                    catch (Exception ex)
                    {
                        response = $"{{\"error\":\"{EscapeJson(ex.Message)}\"}}";
                    }

                    // Write response on a single line (strip internal newlines for pipe protocol)
                    var singleLine = response.Replace("\r", "").Replace("\n", "");
                    var responseBytes = Encoding.UTF8.GetBytes(singleLine + "\n");
                    server.Write(responseBytes, 0, responseBytes.Length);
                    server.Flush();
                }
                catch (ObjectDisposedException)
                {
                    // Server shutting down
                    break;
                }
                catch (IOException ex)
                {
                    Trace.WriteLine($"PipeServer: IO error - {ex.Message}");
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"PipeServer: Error - {ex.Message}");
                }
                finally
                {
                    try { server?.Dispose(); }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Read a line from a stream one byte at a time. 
        /// Avoids StreamReader's internal buffering which blocks on pipes.
        /// </summary>
        private static string ReadLineRaw(Stream stream)
        {
            var sb = new StringBuilder();
            while (true)
            {
                int b = stream.ReadByte();
                if (b == -1) break; // End of stream
                if (b == '\n') break;
                if (b == '\r') continue; // Skip CR
                sb.Append((char)b);
            }
            return sb.ToString();
        }

        private static string EscapeJson(string s)
        {
            return s?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
