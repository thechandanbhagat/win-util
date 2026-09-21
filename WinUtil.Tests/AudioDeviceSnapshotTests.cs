using WinUtil.Models;

namespace WinUtil.Tests;

internal static class AudioDeviceSnapshotTests
{
    internal static void HasDevicesTreatsBlankNamesAsUnavailable()
    {
        var snapshot = new AudioDeviceSnapshot(null, "   ", null);

        TestAssert.False(snapshot.HasOutputDevice);
        TestAssert.False(snapshot.HasInputDevice);
    }

    internal static void HasDevicesRecognizesReportedNames()
    {
        var snapshot = new AudioDeviceSnapshot("Speakers", "Microphone", null);

        TestAssert.True(snapshot.HasOutputDevice);
        TestAssert.True(snapshot.HasInputDevice);
    }
}
