using EarTrumpet.Interop.Helpers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace EarTrumpet.CLI
{
    /// <summary>
    /// Handles CLI arguments before the WPF application starts.
    /// If CLI args are detected, sends the command to the running instance via pipe
    /// and outputs the result to the console, then exits.
    /// </summary>
    public static class CliEntryPoint
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        /// <summary>
        /// Check if the app was launched with CLI arguments.
        /// Returns true if the command was handled (caller should exit).
        /// Returns false if normal WPF startup should continue.
        /// </summary>
        public static bool TryHandleCliArgs(string[] args)
        {
            if (args == null || args.Length == 0) return false;

            // Filter out WPF internal args
            var cliArgs = args.Where(a => !a.StartsWith("-ServerName:")).ToArray();
            if (cliArgs.Length == 0) return false;

            var firstArg = cliArgs[0].TrimStart('-', '/').ToLowerInvariant();

            // Only handle known CLI commands
            switch (firstArg)
            {
                case "help":
                case "h":
                case "?":
                    AttachConsole(ATTACH_PARENT_PROCESS);
                    PrintHelp();
                    FreeConsole();
                    return true;

                case "version":
                case "v":
                    AttachConsole(ATTACH_PARENT_PROCESS);
                    Console.WriteLine($"BetterTrumpet v{GetVersion()}");
                    FreeConsole();
                    return true;

                case "list-devices":
                case "list-apps":
                case "get-volume":
                case "set-volume":
                case "mute":
                case "unmute":
                case "toggle-mute":
                case "get-default":
                case "set-default":
                case "set-device":
                case "list-profiles":
                case "apply-profile":
                case "watch":
                case "check-update":
                case "ping":
                    AttachConsole(ATTACH_PARENT_PROCESS);
                    HandleRemoteCommand(cliArgs);
                    FreeConsole();
                    return true;

                default:
                    // Unknown arg — might be a file association or WPF startup, let it through
                    return false;
            }
        }

        private static void HandleRemoteCommand(string[] args)
        {
            // Build command string from args
            var command = string.Join(" ", args.Select(a => a.Contains(" ") ? $"\"{a}\"" : a));

            // Remove leading -- or / from the first arg
            if (command.StartsWith("--"))
                command = command.Substring(2);
            else if (command.StartsWith("-") || command.StartsWith("/"))
                command = command.Substring(1);

            Console.WriteLine(); // New line after prompt

            // Try to send to running instance
            var response = PipeClient.SendCommand(command);

            if (response == null)
            {
                Console.WriteLine("Error: BetterTrumpet is not running or not responding.");
                Console.WriteLine("Start BetterTrumpet first, then use CLI commands.");
                return;
            }

            // Pretty-print JSON responses
            try
            {
                var parsed = Newtonsoft.Json.Linq.JToken.Parse(response);
                Console.WriteLine(parsed.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch
            {
                Console.WriteLine(response);
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine();
            Console.WriteLine($"BetterTrumpet v{GetVersion()} - CLI Interface");
            Console.WriteLine("═══════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("Usage: BetterTrumpet.exe <command> [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  --list-devices                          List all audio devices and apps");
            Console.WriteLine("  --list-apps                             List running apps with audio sessions");
            Console.WriteLine("  --get-volume [--device ID]              Get volume of default/specified device");
            Console.WriteLine("  --set-volume VALUE [--device ID]        Set volume (0-100 or +N/-N relative)");
            Console.WriteLine("  --set-volume VALUE --app NAME           Set app volume (absolute or relative)");
            Console.WriteLine("  --mute [--device ID|--app NAME]         Mute device or app");
            Console.WriteLine("  --unmute [--device ID|--app NAME]       Unmute device or app");
            Console.WriteLine("  --toggle-mute [--device ID]             Toggle mute on device");
            Console.WriteLine("  --get-default                           Show current default playback device");
            Console.WriteLine("  --set-default DEVICE_NAME               Change default playback device");
            Console.WriteLine("  --set-device APP_EXE DEVICE_NAME        Route app audio to specific device");
            Console.WriteLine("  --list-profiles                         List saved volume profiles");
            Console.WriteLine("  --apply-profile NAME                    Apply a saved volume profile");
            Console.WriteLine("  --watch                                 Snapshot all devices/volumes (JSON)");
            Console.WriteLine("  --check-update                          Check for new version on GitHub");
            Console.WriteLine("  --ping                                  Check if BetterTrumpet is running");
            Console.WriteLine("  --version                               Show version");
            Console.WriteLine("  --help                                  Show this help");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  BetterTrumpet.exe --list-devices");
            Console.WriteLine("  BetterTrumpet.exe --set-volume 75");
            Console.WriteLine("  BetterTrumpet.exe --set-volume +10");
            Console.WriteLine("  BetterTrumpet.exe --set-volume -5 --app spotify");
            Console.WriteLine("  BetterTrumpet.exe --mute --device \"Speakers\"");
            Console.WriteLine("  BetterTrumpet.exe --set-default \"Headphones (BEACN Mic)\"");
            Console.WriteLine("  BetterTrumpet.exe --set-device spotify.exe \"Headphones\"");
            Console.WriteLine("  BetterTrumpet.exe --apply-profile \"Night Mode\"");
            Console.WriteLine();
            Console.WriteLine("Note: BetterTrumpet must be running for remote commands to work.");
            Console.WriteLine("All output is JSON for easy scripting and automation.");
            Console.WriteLine();
        }

        private static string GetVersion()
        {
            try
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
