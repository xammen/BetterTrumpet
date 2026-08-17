using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using EarTrumpet.DataModel.WindowsAudio.Internal;
using EarTrumpet.Diagnosis;
using EarTrumpet.Logic;

namespace BetterTrumpet.LinuxSelfTest;

/// <summary>
/// Runs on the Linux cloud-agent VM. Covers portable engines for GitHub
/// #7 / #30 / #33 / #36 / #37 / #39 / #40 / #41 / #43.
/// Real WASAPI / flyout / tray behavior still needs a Windows box.
/// </summary>
internal static class Program
{
    private static int _failed;

    public static int Main(string[] args)
    {
        var repoRoot = args.Length > 0 ? args[0] : FindRepoRoot();
        if (repoRoot == null)
        {
            Console.WriteLine("FAIL  could not locate BetterTrumpet repo root");
            return 2;
        }

        Console.WriteLine("BetterTrumpet Linux self-test");
        Console.WriteLine("Repo:    " + repoRoot);
        Console.WriteLine("Runtime: " + RuntimeInformation.OSDescription);
        Console.WriteLine();

        RunPathSanitizerTests();
        RunDisconnectGateTests();
        RunAppIdentityTests();
        RunFolderVolumeTests();
        RunWindowSizeTests();
        RunDeviceChangeNotifyTests();
        RunRemoteDesktopTests();
        RunFocusLostTests();
        RunSourceContractTests(repoRoot);

        Console.WriteLine();
        if (_failed == 0)
        {
            Console.WriteLine("ALL TESTS PASSED");
            Console.WriteLine("Note: this does not exercise WASAPI, the flyout, or combase.dll.");
            return 0;
        }

        Console.WriteLine(_failed + " TEST(S) FAILED");
        return 1;
    }

    private static void Assert(bool condition, string name, string? detail = null)
    {
        if (condition)
        {
            Console.WriteLine("PASS  " + name);
            return;
        }

        _failed++;
        Console.WriteLine("FAIL  " + name + (detail == null ? "" : " — " + detail));
    }

    private static void RunPathSanitizerTests()
    {
        Console.WriteLine("== #41 PathSanitizer ==");
        Assert(PathSanitizer.Sanitize(string.Empty) == string.Empty, "empty stays empty");

        var windowsLog = @"C:\Users\Nekromast\AppData\Roaming\BetterTrumpet\logs\bettertrumpet-20260809.log";
        var sanitizedLog = PathSanitizer.Sanitize(windowsLog);
        Assert(
            sanitizedLog.Contains(@"C:\Users\%USERNAME%\AppData\Roaming\BetterTrumpet\logs")
            && !sanitizedLog.Contains("Nekromast"),
            "Windows profile path becomes %USERNAME%",
            sanitizedLog);

        var mixed = @"Log directory not found: c:\users\Alice\AppData\Roaming\BetterTrumpet\logs";
        var mixedOut = PathSanitizer.Sanitize(mixed);
        Assert(
            mixedOut.Contains(@"c:\users\%USERNAME%\", StringComparison.OrdinalIgnoreCase)
            && !mixedOut.Contains("Alice"),
            "case-insensitive Windows user path",
            mixedOut);

        var quoted = @"IconPath: ""C:\Users\Bob\AppData\Local\Programs\Spotify\Spotify.exe""";
        var quotedOut = PathSanitizer.Sanitize(quoted);
        Assert(
            quotedOut.Contains(@"C:\Users\%USERNAME%\") && !quotedOut.Contains("Bob"),
            "quoted exe path",
            quotedOut);

        Assert(
            PathSanitizer.Sanitize(PathSanitizer.Sanitize(windowsLog)) == PathSanitizer.Sanitize(windowsLog),
            "sanitize is idempotent");

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile) && userProfile.Length >= 4)
        {
            var localOut = PathSanitizer.Sanitize(userProfile + "/BetterTrumpet/logs/app.log");
            Assert(localOut.StartsWith("%USERPROFILE%", StringComparison.Ordinal), "Linux user profile replaced", localOut);
            Assert(!localOut.Contains(userProfile), "raw profile path gone");
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData) && appData.Length >= 4)
        {
            var outApp = PathSanitizer.Sanitize(appData + "/BetterTrumpet/diagnostics/bundle.zip");
            Assert(outApp.StartsWith("%APPDATA%", StringComparison.Ordinal), "appdata replaced", outApp);
        }

        var temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var tempOut = PathSanitizer.Sanitize(temp + "/BetterTrumpet-diagnostics.zip");
        Assert(tempOut.StartsWith("%TEMP%", StringComparison.Ordinal), "temp replaced", tempOut);

        Assert(
            PathSanitizer.Sanitize("Brave froze and I killed it") == "Brave froze and I killed it",
            "plain prose unchanged");
    }

    private static void RunDisconnectGateTests()
    {
        Console.WriteLine();
        Console.WriteLine("== #43 SessionDisconnectGate ==");

        var issued = 0;
        Assert(SessionDisconnectGate.TryBeginDisconnect(ref issued), "first disconnect wins");
        Assert(issued == 1, "flag is set after first disconnect");
        Assert(!SessionDisconnectGate.TryBeginDisconnect(ref issued), "second disconnect is ignored");
        Assert(!SessionDisconnectGate.TryBeginDisconnect(ref issued), "third disconnect is ignored");

        var concurrent = 0;
        var entered = 0;
        Parallel.For(0, 2000, _ =>
        {
            if (SessionDisconnectGate.TryBeginDisconnect(ref concurrent))
            {
                Interlocked.Increment(ref entered);
            }
        });
        Assert(entered == 1, "exactly one concurrent caller enters teardown", "entered=" + entered);
    }

    private static void RunSourceContractTests(string repoRoot)
    {
        Console.WriteLine();
        Console.WriteLine("== #37 / #41 source contracts ==");

        var combase = Read(repoRoot, "EarTrumpet/Interop/Combase.cs");
        Assert(
            !Regex.IsMatch(combase, @"MarshalAs\s*\(\s*UnmanagedType\.HString"),
            "Combase.cs does not marshal HSTRING via UnmanagedType.HString");
        Assert(
            !Regex.IsMatch(combase, @"MarshalAs\s*\(\s*UnmanagedType\.IInspectable"),
            "Combase.cs does not marshal IInspectable via UnmanagedType.IInspectable");
        Assert(combase.Contains("GetActivationFactory"), "Combase.cs exposes GetActivationFactory");
        Assert(combase.Contains("WindowsDeleteString"), "Combase.cs deletes HSTRING handles");
        Assert(combase.Contains("WindowsGetStringRawBuffer"), "Combase.cs unpacks HSTRING buffers");
        Assert(Regex.IsMatch(combase, @"RoGetActivationFactory\(\s*IntPtr"), "RoGetActivationFactory takes IntPtr, not string");

        foreach (var variant in new[]
                 {
                     "EarTrumpet/Interop/MMDeviceAPI/IAudioPolicyConfigFactoryVariantFor21H2.cs",
                     "EarTrumpet/Interop/MMDeviceAPI/IAudioPolicyConfigFactoryVariantForDownlevel.cs"
                 })
        {
            var text = Read(repoRoot, variant);
            Assert(
                text.Contains("GetPersistedDefaultAudioEndpoint") && text.Contains("out IntPtr deviceId"),
                Path.GetFileName(variant) + " returns HSTRING as IntPtr",
                variant);
            Assert(!Regex.IsMatch(text, @"MarshalAs\s*\(\s*UnmanagedType\.HString"), Path.GetFileName(variant) + " has no HString marshaller");
        }

        var manager = Read(repoRoot, "EarTrumpet/DataModel/WindowsAudio/IAudioDeviceManagerWindowsAudio.cs");
        Assert(manager.Contains("bool SetDefaultEndPoint"), "SetDefaultEndPoint reports success to callers");

        var moveVm = Read(repoRoot, "EarTrumpet/UI/ViewModels/DeviceCollectionViewModel.cs");
        Assert(
            moveVm.Contains("if (!app.MoveToDevice") && moveVm.Contains("Windows API failed"),
            "flyout does not clone an app row after a failed endpoint move");

        var session = Read(repoRoot, "EarTrumpet/DataModel/WindowsAudio/Internal/AudioDeviceSession.cs");
        Assert(
            session.Contains("SessionDisconnectGate.TryBeginDisconnect"),
            "AudioDeviceSession uses the disconnect gate");
        Assert(
            session.Contains("_dispatcher.BeginInvoke") && session.Contains("OnIconPathChanged"),
            "OnIconPathChanged marshals to the dispatcher");

        var exporter = Read(repoRoot, "EarTrumpet/Diagnosis/LocalDataExporter.cs");
        Assert(exporter.Contains("PathSanitizer.Sanitize"), "diagnostic export sanitizes paths");
        Assert(exporter.Contains("CreateStagingFolder"), "diagnostic export can stage files for review");

        var reporter = Read(repoRoot, "EarTrumpet/Diagnosis/ErrorReporter.cs");
        Assert(reporter.Contains("HasStoredTelemetryConsent"), "Sentry waits for stored telemetry consent");
        Assert(reporter.Contains("DiagnosticsExportConfirmMessage"), "manual export warns before creating files");

        var onboarding = Read(repoRoot, "EarTrumpet/UI/ViewModels/OnboardingViewModel.cs");
        Assert(
            onboarding.Contains("ApplyPrivacyAndUpdates()") && Regex.IsMatch(onboarding, @"void Skip\(\)[\s\S]*ApplyPrivacyAndUpdates"),
            "onboarding Skip persists privacy choices");

        var app = Read(repoRoot, "EarTrumpet/App.xaml.cs");
        Assert(app.Contains("TryStartUpdateService"), "GitHub update checks wait for first-run");
        Assert(
            app.Contains("Settings.HasShownFirstRun = true") && app.Contains("vm.Completed +="),
            "hasShownFirstRun is written when onboarding completes");
        Assert(app.Contains("OnDefaultPlaybackDeviceChanged"), "device-change toast is wired");
        Assert(app.Contains("_lastNotifiedDeviceId = CollectionViewModel.Default"), "first default device is seeded so the next switch can notify");
        Assert(app.Contains("FocusLostService"), "focus-lost service starts with the tray");

        var colors = Read(repoRoot, "EarTrumpet/UI/ViewModels/EarTrumpetColorsSettingsPageViewModel.cs");
        Assert(colors.Contains("public bool UseLegacyIcon"), "legacy tray icon lives on Appearance");
        var general = Read(repoRoot, "EarTrumpet/UI/ViewModels/EarTrumpetLegacySettingsPageViewModel.cs");
        Assert(!Regex.IsMatch(general, @"public bool UseLegacyIcon"), "General page no longer owns UseLegacyIcon");

        var settingsXaml = Read(repoRoot, "EarTrumpet/UI/Views/SettingsWindow.xaml");
        var appearanceStart = settingsXaml.IndexOf("EarTrumpetColorsSettingsPageViewModel", StringComparison.Ordinal);
        var generalStart = settingsXaml.IndexOf("EarTrumpetLegacySettingsPageViewModel", StringComparison.Ordinal);
        var iconBinding = settingsXaml.IndexOf("IsChecked=\"{Binding UseLegacyIcon", StringComparison.Ordinal);
        Assert(appearanceStart >= 0 && generalStart > appearanceStart && iconBinding > appearanceStart && iconBinding < generalStart,
            "legacy icon checkbox is in the Appearance template");
        Assert(settingsXaml.Contains("ShowAppRulesEmptyHint"), "folder-only rules hide the empty app-rules hint");

        var mixer = Read(repoRoot, "EarTrumpet/UI/Views/FullWindow.xaml.cs");
        Assert(mixer.Contains("WindowSizePolicy.ShouldRestoreUserSize"), "mixer restores size only in many-devices mode");
        Assert(mixer.Contains("MixerWindowWidth"), "mixer persists width/height");
        Assert(mixer.Contains("_restoredUserSize"), "mixer does not re-apply saved size on every device change");
        Assert(Regex.IsMatch(mixer, @"ShouldRestoreUserSize\([\s\S]*MixerWindowWidth"), "auto-sized small mixer does not overwrite the saved size");

        var sessionVm = Read(repoRoot, "EarTrumpet/UI/ViewModels/AudioSessionViewModel.cs");
        Assert(sessionVm.Contains("SetMuteWithoutUndo"), "automatic mute does not record undo steps");

        var focusService = Read(repoRoot, "EarTrumpet/UI/Helpers/FocusLostService.cs");
        Assert(focusService.Contains("Environment.ProcessId"), "focus-lost ignores BetterTrumpet's own process");
        Assert(focusService.Contains("SetMuteWithoutUndo"), "focus-lost mute skips the undo stack");

        var deviceVm = Read(repoRoot, "EarTrumpet/UI/ViewModels/DeviceViewModel.cs");
        Assert(deviceVm.Contains("RemoteDesktopIdentity.IsRemoteDesktopExe"), "RDP reconnect restores last volume");
        Assert(deviceVm.Contains("FolderVolumeRuleMatcher") || Read(repoRoot, "EarTrumpet/AppSettings.cs").Contains("FolderVolumeRuleMatcher.TryMatch"),
            "folder defaults use the portable matcher");
    }

    private static string Read(string repoRoot, string relative)
    {
        return File.ReadAllText(Path.Combine(repoRoot, relative));
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "EarTrumpet", "Interop", "Combase.cs")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "EarTrumpet", "Interop", "Combase.cs")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static void RunAppIdentityTests()
    {
        Console.WriteLine();
        Console.WriteLine("== CLI / app identity ==");
        Assert(AppIdentity.NormalizeExeName(@"C:\Games\steam.exe") == "steam", "full path becomes exe name");
        Assert(AppIdentity.NormalizeExeName("steam.exe") == "steam", "steam.exe becomes steam");
        Assert(AppIdentity.NormalizeExeName("  \"Spotify.exe\"  ") == "Spotify", "quoted name is trimmed");
        Assert(AppIdentity.MatchesExact("spotify.exe", "Spotify", "spotify"), "exact match ignores extension");
        Assert(AppIdentity.MatchesPartial("chrome.exe", "Google Chrome", "chrom"), "partial exe match");
        Assert(AppIdentity.MatchesDevice("Speakers (Realtek)", "realtek"), "device match is partial");
        Assert(!AppIdentity.MatchesDevice("Headphones", "speaker"), "unrelated device does not match");
        Assert(AppIdentity.Score("spotify.exe", "Spotify", "C:\\Spotify\\Spotify.exe", "spotify") == 100, "exact score is 100");
        Assert(AppIdentity.Score("spotify.exe", "Spotify", "", "spot") == 80, "prefix score is 80");
        Assert(AppIdentity.Score("spotify.exe", "Spotify", "", "ify") == 60, "contains score is 60");
        Assert(AppIdentity.Score("spotify.exe", "Spotify", "", "zzz") == 0, "miss scores 0");
    }

    private static void RunFolderVolumeTests()
    {
        Console.WriteLine();
        Console.WriteLine("== #30 folder volume matcher ==");
        var steam = new FolderVolumeRule(@"C:\Program Files\Steam", 10, DateTime.UtcNow.AddMinutes(-2));
        var game = new FolderVolumeRule(@"C:\Program Files\Steam\steamapps\common\Game", 5, DateTime.UtcNow);
        var rules = new[] { steam, game };

        Assert(FolderVolumeRuleMatcher.IsUsableExecutablePath(@"C:\Program Files\Steam\steam.exe"), "Windows exe path is usable on Linux");
        Assert(FolderVolumeRuleMatcher.IsExecutableUnderFolder(
                @"C:\Program Files\Steam\steamapps\common\Game\game.exe",
                @"C:\Program Files\Steam"),
            "nested exe matches parent folder");
        Assert(!FolderVolumeRuleMatcher.IsExecutableUnderFolder(
                @"C:\Games\other\game.exe",
                @"C:\Program Files\Steam"),
            "unrelated folder does not match");
        Assert(FolderVolumeRuleMatcher.IsExecutableUnderFolder(
                @"C:\Program Files\WindowsApps\Foo.Bar_1.0.0.0_x64",
                @"C:\Program Files\WindowsApps\Foo.Bar_1.0.0.0_x64"),
            "store package folder equals the rule path");
        Assert(!FolderVolumeRuleMatcher.IsExecutableUnderFolder(
                @"C:\Game2\game.exe",
                @"C:\Game"),
            "prefix without a separator is not a match");

        int volume;
        Assert(FolderVolumeRuleMatcher.TryMatch(@"C:\Program Files\Steam\steamapps\common\Game\game.exe", rules, out volume) && volume == 5,
            "deepest folder wins", "volume=" + volume);
        Assert(FolderVolumeRuleMatcher.TryMatch(@"C:\Program Files\Steam\steam.exe", rules, out volume) && volume == 10,
            "parent folder matches steam.exe", "volume=" + volume);
        Assert(!FolderVolumeRuleMatcher.TryMatch("steam", rules, out volume), "bare exe name is not a folder path");

        var unixRules = new[] { new FolderVolumeRule("/opt/games/steam", 25, DateTime.UtcNow) };
        Assert(FolderVolumeRuleMatcher.TryMatch("/opt/games/steam/steamapps/game.exe", unixRules, out volume) && volume == 25,
            "unix folder prefix matches", "volume=" + volume);
    }

    private static void RunWindowSizeTests()
    {
        Console.WriteLine();
        Console.WriteLine("== #40 mixer window size ==");
        Assert(!WindowSizePolicy.ShouldRestoreUserSize(1), "1 device stays auto-sized");
        Assert(!WindowSizePolicy.ShouldRestoreUserSize(3), "3 devices stay auto-sized");
        Assert(WindowSizePolicy.ShouldRestoreUserSize(4), "4+ devices restore user size");
        Assert(!WindowSizePolicy.TryNormalize(0, 800, out _, out _), "zero width is rejected");
        Assert(!WindowSizePolicy.TryNormalize(800, double.NaN, out _, out _), "NaN is rejected");
        double width, height;
        Assert(WindowSizePolicy.TryNormalize(100, 50, out width, out height) && width == WindowSizePolicy.MinWidth && height == WindowSizePolicy.MinHeight,
            "tiny size clamps to minimum", $"w={width} h={height}");
        Assert(WindowSizePolicy.TryNormalize(99999, 99999, out width, out height) && width == WindowSizePolicy.MaxWidth && height == WindowSizePolicy.MaxHeight,
            "huge size clamps to maximum", $"w={width} h={height}");
        Assert(WindowSizePolicy.TryNormalize(1280, 720, out width, out height) && width == 1280 && height == 720,
            "normal size is kept");
    }

    private static void RunDeviceChangeNotifyTests()
    {
        Console.WriteLine();
        Console.WriteLine("== #36 device-change notify ==");
        Assert(!DefaultDeviceChangePolicy.ShouldNotify(null, "dev-1", true), "first observation is silent");
        Assert(!DefaultDeviceChangePolicy.ShouldNotify("", "dev-1", true), "empty previous id is silent");
        Assert(!DefaultDeviceChangePolicy.ShouldNotify("dev-1", "dev-2", false), "disabled setting never notifies");
        Assert(!DefaultDeviceChangePolicy.ShouldNotify("dev-1", "dev-1", true), "same device does not notify");
        Assert(DefaultDeviceChangePolicy.ShouldNotify("dev-1", "dev-2", true), "real switch notifies");
        Assert(!DefaultDeviceChangePolicy.ShouldNotify("dev-1", null, true), "null new device is ignored");
    }

    private static void RunRemoteDesktopTests()
    {
        Console.WriteLine();
        Console.WriteLine("== #7 RDP volume identity ==");
        Assert(RemoteDesktopIdentity.IsRemoteDesktopExe("mstsc"), "mstsc is RDP");
        Assert(RemoteDesktopIdentity.IsRemoteDesktopExe("mstsc.exe"), "mstsc.exe is RDP");
        Assert(RemoteDesktopIdentity.IsRemoteDesktopExe(@"C:\Windows\System32\mstsc.exe"), "full mstsc path is RDP");
        Assert(RemoteDesktopIdentity.IsRemoteDesktopExe("msrdc"), "Store RDP client is RDP");
        Assert(!RemoteDesktopIdentity.IsRemoteDesktopExe("spotify"), "spotify is not RDP");
        int volume;
        Assert(!RemoteDesktopIdentity.TryGetRememberedVolume(-1, out volume), "unset sentinel is not a volume");
        Assert(RemoteDesktopIdentity.TryGetRememberedVolume(37, out volume) && volume == 37, "stored RDP volume is returned");
        Assert(RemoteDesktopIdentity.ClampStoredVolume(-8) == -1, "negative stays unset");
        Assert(RemoteDesktopIdentity.ClampStoredVolume(140) == 100, "over-max clamps to 100");
    }

    private static void RunFocusLostTests()
    {
        Console.WriteLine();
        Console.WriteLine("== #33 focus-lost policy ==");
        Assert(FocusLostVolumePolicy.ResolveMode(false, 0) == FocusLostMode.Off, "disabled is Off");
        Assert(FocusLostVolumePolicy.ResolveMode(true, 0) == FocusLostMode.Mute, "0% is mute");
        Assert(FocusLostVolumePolicy.ResolveMode(true, 20) == FocusLostMode.Attenuate, "20% is attenuate");

        var muted = FocusLostVolumePolicy.ApplyBackground(80, false, FocusLostMode.Mute, 0);
        Assert(muted.IsMuted && muted.Volume == 80, "mute keeps slider volume");
        var quiet = FocusLostVolumePolicy.ApplyBackground(80, false, FocusLostMode.Attenuate, 20);
        Assert(!quiet.IsMuted && quiet.Volume == 20, "attenuate sets background volume");

        var supervisor = new FocusLostSupervisor();
        var game = new FocusLostSession("game", 1001, 80, false, true);
        var music = new FocusLostSession("music", 2002, 50, false, true);
        var locked = new FocusLostSession("locked", 3003, 40, false, false);

        var first = supervisor.OnForegroundChanged(1001, new[] { game, music, locked }, FocusLostMode.Mute, 0);
        Assert(first.Count == 0, "first foreground observation does not touch volumes");

        var ignored = supervisor.OnForegroundChanged(9999, new[] { game, music, locked }, FocusLostMode.Mute, 0, 9999);
        Assert(ignored.Count == 0, "BetterTrumpet's own HWND does not mute everything");

        var backgrounded = supervisor.OnForegroundChanged(2002, new[] { game, music, locked }, FocusLostMode.Mute, 0, 9999);
        Assert(backgrounded.Count == 1 && backgrounded[0].Key == "game" && backgrounded[0].IsMuted,
            "leaving a game mutes it");
        Assert(backgrounded.All(a => a.Key != "locked"), "locked sessions are skipped");

        var gameMuted = new FocusLostSession("game", 1001, 80, true, true);
        var again = supervisor.OnForegroundChanged(4004, new[] { gameMuted, music, locked }, FocusLostMode.Mute, 0, 9999);
        Assert(again.All(a => a.Key != "game"), "already-muted background app is not written again");
        Assert(again.Any(a => a.Key == "music" && a.IsMuted), "the new background app is muted");

        var musicMuted = new FocusLostSession("music", 2002, 50, true, true);
        var restored = supervisor.OnForegroundChanged(1001, new[] { gameMuted, musicMuted, locked }, FocusLostMode.Mute, 0);
        Assert(restored.Any(a => a.Key == "game" && !a.IsMuted && a.Volume == 80), "returning focus restores the game");

        var off = supervisor.OnForegroundChanged(1001, new[] { game, musicMuted, locked }, FocusLostMode.Off, 0);
        Assert(off.Any(a => a.Key == "music" && !a.IsMuted && a.Volume == 50), "disabling the feature restores leftovers");

        var modeSwitch = new FocusLostSupervisor();
        modeSwitch.OnForegroundChanged(1001, new[] { game, music }, FocusLostMode.Mute, 0);
        modeSwitch.OnForegroundChanged(2002, new[] { game, music }, FocusLostMode.Mute, 0);
        var attenuated = modeSwitch.OnForegroundChanged(2002, new[] { gameMuted, music }, FocusLostMode.Attenuate, 20);
        Assert(attenuated.Any(a => a.Key == "game" && a.Volume == 20 && !a.IsMuted),
            "changing 0% mute to 20% reapplies from the original snapshot");

        using (VolumeWriteScope.Begin())
        {
            Assert(VolumeWriteScope.IsActive, "volume write scope is active inside using");
        }
        Assert(!VolumeWriteScope.IsActive, "volume write scope clears after dispose");
    }
}
