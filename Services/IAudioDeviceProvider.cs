using WinUtil.Models;

namespace WinUtil.Services;

internal interface IAudioDeviceProvider
{
    AudioDeviceSnapshot GetSnapshot();
}
