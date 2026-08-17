namespace EarTrumpet.Logic
{
    public enum FocusLostMode
    {
        Off = 0,
        Mute = 1,
        Attenuate = 2,
    }

    public readonly struct FocusLostSnapshot
    {
        public FocusLostSnapshot(int volume, bool isMuted)
        {
            Volume = volume;
            IsMuted = isMuted;
        }

        public int Volume { get; }
        public bool IsMuted { get; }
    }

    public readonly struct FocusLostSession
    {
        public FocusLostSession(string key, int processId, int volume, bool isMuted, bool canAdjust)
        {
            Key = key;
            ProcessId = processId;
            Volume = volume;
            IsMuted = isMuted;
            CanAdjust = canAdjust;
        }

        public string Key { get; }
        public int ProcessId { get; }
        public int Volume { get; }
        public bool IsMuted { get; }
        public bool CanAdjust { get; }
    }

    public readonly struct FocusLostAdjustment
    {
        public FocusLostAdjustment(string key, int volume, bool isMuted)
        {
            Key = key;
            Volume = volume;
            IsMuted = isMuted;
        }

        public string Key { get; }
        public int Volume { get; }
        public bool IsMuted { get; }
    }

    /// <summary>
    /// GitHub #33: mute or reduce an app when it is no longer the foreground process.
    /// 0% attenuation is treated as mute.
    /// </summary>
    public static class FocusLostVolumePolicy
    {
        public static FocusLostMode ResolveMode(bool enabled, int attenuatePercent)
        {
            if (!enabled)
            {
                return FocusLostMode.Off;
            }

            return attenuatePercent <= 0 ? FocusLostMode.Mute : FocusLostMode.Attenuate;
        }

        public static int ClampAttenuatePercent(int percent)
        {
            if (percent < 0)
            {
                return 0;
            }

            if (percent > 100)
            {
                return 100;
            }

            return percent;
        }

        public static FocusLostSnapshot ApplyBackground(int currentVolume, bool currentMuted, FocusLostMode mode, int attenuatePercent)
        {
            if (mode == FocusLostMode.Mute)
            {
                return new FocusLostSnapshot(currentVolume, true);
            }

            if (mode == FocusLostMode.Attenuate)
            {
                return new FocusLostSnapshot(ClampAttenuatePercent(attenuatePercent), currentMuted);
            }

            return new FocusLostSnapshot(currentVolume, currentMuted);
        }
    }
}
