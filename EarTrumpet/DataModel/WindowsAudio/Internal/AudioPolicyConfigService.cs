using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.Interop.MMDeviceAPI;
using System;
using System.Diagnostics;

namespace EarTrumpet.DataModel.WindowsAudio.Internal
{
    class AudioPolicyConfig
    {
        private const string DEVINTERFACE_AUDIO_RENDER = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
        private const string DEVINTERFACE_AUDIO_CAPTURE = "#{2eef81be-33fa-4800-9670-1cd474972c3f}";
        private const string MMDEVAPI_TOKEN = @"\\?\SWD#MMDEVAPI#";

        private IAudioPolicyConfigFactory _sharedPolicyConfig;
        private bool _factoryFailed;
        private EDataFlow _flow;

        public AudioPolicyConfig(EDataFlow flow)
        {
            _flow = flow;
        }

        private bool EnsurePolicyConfig()
        {
            if (_sharedPolicyConfig != null)
            {
                return true;
            }

            if (_factoryFailed)
            {
                return false;
            }

            try
            {
                _sharedPolicyConfig = AudioPolicyConfigFactory.Create();
                return _sharedPolicyConfig != null;
            }
            catch (Exception ex)
            {
                _factoryFailed = true;
                Trace.WriteLine($"AudioPolicyConfig EnsurePolicyConfig failed: {ex}");
                return false;
            }
        }

        private string GenerateDeviceId(string deviceId)
        {
            return $"{MMDEVAPI_TOKEN}{deviceId}{(_flow == EDataFlow.eRender ? DEVINTERFACE_AUDIO_RENDER : DEVINTERFACE_AUDIO_CAPTURE)}";
        }

        private string UnpackDeviceId(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return deviceId;
            if (deviceId.StartsWith(MMDEVAPI_TOKEN)) deviceId = deviceId.Remove(0, MMDEVAPI_TOKEN.Length);
            if (deviceId.EndsWith(DEVINTERFACE_AUDIO_RENDER)) deviceId = deviceId.Remove(deviceId.Length - DEVINTERFACE_AUDIO_RENDER.Length);
            if (deviceId.EndsWith(DEVINTERFACE_AUDIO_CAPTURE)) deviceId = deviceId.Remove(deviceId.Length - DEVINTERFACE_AUDIO_CAPTURE.Length);
            return deviceId;
        }

        private static bool Succeeded(HRESULT hr)
        {
            return unchecked((int)hr) >= 0;
        }

        public bool SetDefaultEndPoint(string deviceId, int processId)
        {
            Trace.WriteLine($"AudioPolicyConfigService SetDefaultEndPoint {deviceId} {processId}");
            try
            {
                if (!EnsurePolicyConfig())
                {
                    return false;
                }

                IntPtr hstring = IntPtr.Zero;

                try
                {
                    if (!string.IsNullOrWhiteSpace(deviceId))
                    {
                        var str = GenerateDeviceId(deviceId);
                        Combase.WindowsCreateString(str, (uint)str.Length, out hstring);
                    }

                    var multimedia = _sharedPolicyConfig.SetPersistedDefaultAudioEndpoint((uint)processId, _flow, ERole.eMultimedia, hstring);
                    var console = _sharedPolicyConfig.SetPersistedDefaultAudioEndpoint((uint)processId, _flow, ERole.eConsole, hstring);
                    return Succeeded(multimedia) && Succeeded(console);
                }
                finally
                {
                    if (hstring != IntPtr.Zero)
                    {
                        Combase.WindowsDeleteString(hstring);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{ex}");
                return false;
            }
        }

        public string GetDefaultEndPoint(int processId)
        {
            try
            {
                if (!EnsurePolicyConfig())
                {
                    return null;
                }

                var hr = _sharedPolicyConfig.GetPersistedDefaultAudioEndpoint((uint)processId, _flow, ERole.eMultimedia | ERole.eConsole, out string deviceId);
                if (!Succeeded(hr) || string.IsNullOrWhiteSpace(deviceId))
                {
                    return null;
                }

                return UnpackDeviceId(deviceId);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{ex}");
            }

            return null;
        }
    }
}
