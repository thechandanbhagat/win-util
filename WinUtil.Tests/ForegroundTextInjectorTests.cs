using WinUtil.Services;

namespace WinUtil.Tests;

internal static class ForegroundTextInjectorTests
{
    private const int InputStructureSizeOn32BitProcess = 28;
    private const int InputStructureSizeOn64BitProcess = 40;

    internal static void InputStructureMatchesTheWindowsAbi()
    {
        var expectedSize = IntPtr.Size == sizeof(long)
            ? InputStructureSizeOn64BitProcess
            : InputStructureSizeOn32BitProcess;

        TestAssert.Equal(expectedSize, ForegroundTextInjector.InputStructureSize);
    }
}
