using WinUtil.Services;

namespace WinUtil.Tests;

internal static class SingleInstanceGateTests
{
    internal static void SecondGateDetectsAnExistingInstance()
    {
        var gateName = $"WinUtil.Tests.{Guid.NewGuid():N}";

        using var primaryGate = new SingleInstanceGate(gateName);
        using var secondaryGate = new SingleInstanceGate(gateName);

        TestAssert.True(primaryGate.IsPrimaryInstance);
        TestAssert.False(secondaryGate.IsPrimaryInstance);
    }
}
