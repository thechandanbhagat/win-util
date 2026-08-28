using WinUtil.Models;
using WinUtil.Services;

namespace WinUtil.Tests;

internal static class AppTests
{
    internal static void ApplySpotlightShortcutsKeepsASuccessfulRegistrationSuccessful()
    {
        var manager = new SuccessfulShortcutManager();

        var error = App.ApplySpotlightShortcuts(manager, new SpotlightShortcuts());

        TestAssert.Equal<string?>(null, error);
        TestAssert.True(manager.WasCalled);
    }

    private sealed class SuccessfulShortcutManager : ISpotlightShortcutManager
    {
        internal bool WasCalled { get; private set; }

        public void BeginShortcutCapture(Action<ShortcutCaptureResult> captureCallback)
        {
        }

        public void Dispose()
        {
        }

        public void EndShortcutCapture()
        {
        }

        public string? TryApply(SpotlightShortcuts shortcuts)
        {
            WasCalled = true;
            return null;
        }
    }
}
