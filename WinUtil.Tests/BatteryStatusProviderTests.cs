using WinUtil.Models;
using WinUtil.Services;

namespace WinUtil.Tests;

internal static class BatteryStatusProviderTests
{
    internal static void ParseBluetoothDevicesExcludesDisconnectedDevices()
    {
        const string output = """
            [
              { "Name": "Keyboard", "Percentage": 64, "IsConnected": false },
              { "Name": "Mouse", "Percentage": 82, "IsConnected": true }
            ]
            """;

        var devices = BatteryStatusProvider.ParseBluetoothDevices(output);

        TestAssert.SequenceEqual([new BatteryDevice("Mouse", 82)], devices);
    }

    internal static void ParseBluetoothDevicesNormalizesConnectedDevices()
    {
        const string output = """
            [
              { "Name": " Mouse ", "Percentage": 45, "IsConnected": true },
              { "Name": "mouse", "Percentage": 72, "IsConnected": true },
              { "Name": "Headset", "Percentage": 100, "IsConnected": true },
              { "Name": "", "Percentage": 40, "IsConnected": true },
              { "Name": "Invalid", "Percentage": 101, "IsConnected": true }
            ]
            """;

        var devices = BatteryStatusProvider.ParseBluetoothDevices(output);

        TestAssert.SequenceEqual(
            [new BatteryDevice("Headset", 100), new BatteryDevice("Mouse", 72)],
            devices);
    }
}
