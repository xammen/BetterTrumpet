using EarTrumpet.Extensions;
using EarTrumpet.Interop.MMDeviceAPI;
using System;
using System.Diagnostics;

namespace EarTrumpet.Interop.Helpers
{
    public class AudioPolicyConfigFactory
    {
        public static IAudioPolicyConfigFactory Create()
        {
            if (Environment.OSVersion.IsAtLeast(OSVersions.Version21H2))
            {
                try
                {
                    return new AudioPolicyConfigFactoryImplFor21H2();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"AudioPolicyConfigFactory 21H2 activation failed, trying downlevel: {ex.Message}");
                }
            }

            return new AudioPolicyConfigFactoryImplForDownlevel();
        }
    }
}
