using WinUtil.Services;

namespace WinUtil.Tests;

internal static class WinUtilTrayIconTests
{
    private const int ExpectedIconSizePixels = 32;

    internal static void CreateProducesASystemTraySizedIcon()
    {
        using var icon = WinUtilTrayIcon.Create();

        TestAssert.Equal(ExpectedIconSizePixels, icon.Width);
        TestAssert.Equal(ExpectedIconSizePixels, icon.Height);
    }
}
