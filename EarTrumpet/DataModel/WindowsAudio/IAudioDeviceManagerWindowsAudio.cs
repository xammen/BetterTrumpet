using EarTrumpet.DataModel.Audio;
using EarTrumpet.Interop.MMDeviceAPI;

namespace EarTrumpet.DataModel.WindowsAudio
{
    public interface IAudioDeviceManagerWindowsAudio
    {
        bool SetDefaultDevice(IAudioDevice device, ERole role);
        IAudioDevice GetDefaultDevice(ERole role);
        bool SetDefaultEndPoint(string id, int pid);
        string GetDefaultEndPoint(int processId);
        void MoveHiddenAppsToDevice(string appId, string deviceId);
        void UnhideSessionsForProcessId(string deviceId, int processId);
    }
}
