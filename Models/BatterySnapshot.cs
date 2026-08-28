namespace WinUtil.Models;

internal sealed record BatterySnapshot(IReadOnlyList<BatteryDevice> Devices, string? BluetoothErrorMessage)
{
    internal bool HasDevices => Devices.Count > 0;
}
