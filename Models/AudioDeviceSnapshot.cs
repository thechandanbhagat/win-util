namespace WinUtil.Models;

internal sealed record AudioDeviceSnapshot(string? OutputDeviceName, string? InputDeviceName, string? ErrorMessage)
{
    internal bool HasOutputDevice => !string.IsNullOrWhiteSpace(OutputDeviceName);

    internal bool HasInputDevice => !string.IsNullOrWhiteSpace(InputDeviceName);
}
