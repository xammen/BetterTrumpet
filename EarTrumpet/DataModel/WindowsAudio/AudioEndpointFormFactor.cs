namespace EarTrumpet.DataModel.WindowsAudio
{
    // Mirrors the Windows EndpointFormFactor enumeration (mmdeviceapi.h), the value of
    // PKEY_AudioEndpoint_FormFactor.
    public enum AudioEndpointFormFactor
    {
        RemoteNetworkDevice = 0,
        Speakers = 1,
        LineLevel = 2,
        Headphones = 3,
        Microphone = 4,
        Headset = 5,
        Handset = 6,
        UnknownDigitalPassthrough = 7,
        SPDIF = 8,
        DigitalAudioDisplayDevice = 9,
        UnknownFormFactor = 10,
    }
}
