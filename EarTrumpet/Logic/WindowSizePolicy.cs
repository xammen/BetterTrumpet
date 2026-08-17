namespace EarTrumpet.Logic
{
    /// <summary>
    /// Mixer size restore for GitHub #40. The Open Mixer window auto-sizes with
    /// 1-3 devices; user-resized bounds only apply in many-devices mode.
    /// </summary>
    public static class WindowSizePolicy
    {
        public const double MinWidth = 360;
        public const double MinHeight = 200;
        public const double MaxWidth = 4096;
        public const double MaxHeight = 4096;
        public const int SmallDeviceCountLimit = 3;

        public static bool ShouldRestoreUserSize(int deviceCount)
        {
            return deviceCount > SmallDeviceCountLimit;
        }

        public static bool TryNormalize(double width, double height, out double clampedWidth, out double clampedHeight)
        {
            clampedWidth = 0;
            clampedHeight = 0;
            if (double.IsNaN(width) || double.IsNaN(height) || double.IsInfinity(width) || double.IsInfinity(height))
            {
                return false;
            }

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            clampedWidth = Clamp(width, MinWidth, MaxWidth);
            clampedHeight = Clamp(height, MinHeight, MaxHeight);
            return true;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }
}
