using WinUtil.Models;

namespace WinUtil.Tests;

internal static class BatterySnapshotTests
{
    internal static void HasDevicesIsFalseForAnEmptySnapshot()
    {
        var snapshot = new BatterySnapshot([], null);

        TestAssert.False(snapshot.HasDevices);
    }

    internal static void HasDevicesIsTrueWhenABatteryIsAvailable()
    {
        var snapshot = new BatterySnapshot([new BatteryDevice("Mouse", 84)], null);

        TestAssert.True(snapshot.HasDevices);
    }
}
