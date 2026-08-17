using EarTrumpet.Interop.MMDeviceAPI;
using System;

namespace EarTrumpet.Interop.Helpers
{
    class AudioPolicyConfigFactoryImplFor21H2 : IAudioPolicyConfigFactory
    {
        private readonly IAudioPolicyConfigFactoryVariantFor21H2 _factory;

        internal AudioPolicyConfigFactoryImplFor21H2()
        {
            var iid = typeof(IAudioPolicyConfigFactoryVariantFor21H2).GUID;
            _factory = (IAudioPolicyConfigFactoryVariantFor21H2)Combase.GetActivationFactory(
                "Windows.Media.Internal.AudioPolicyConfig", iid);
        }

        public HRESULT ClearAllPersistedApplicationDefaultEndpoints()
        {
            return _factory.ClearAllPersistedApplicationDefaultEndpoints();
        }

        public HRESULT GetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, out string deviceId)
        {
            var hr = _factory.GetPersistedDefaultAudioEndpoint(processId, flow, role, out var hstring);
            try
            {
                deviceId = Combase.HStringToString(hstring);
                return hr;
            }
            finally
            {
                if (hstring != IntPtr.Zero)
                {
                    Combase.WindowsDeleteString(hstring);
                }
            }
        }

        public HRESULT SetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, IntPtr deviceId)
        {
            return _factory.SetPersistedDefaultAudioEndpoint(processId, flow, role, deviceId);
        }
    }
}
