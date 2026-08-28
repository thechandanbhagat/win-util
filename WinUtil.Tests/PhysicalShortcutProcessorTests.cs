using WinUtil.Models;
using WinUtil.Services;

namespace WinUtil.Tests;

internal static class PhysicalShortcutProcessorTests
{
    private const uint ControlVirtualKey = 0x11;
    private const uint OemMinusVirtualKey = 0xBD;
    private const uint SlashScanCode = 0x35;
    private const uint SlashVirtualKey = 0xBF;
    private const uint UuidShortcutScanCode = 0x16;

    internal static void ProcessMatchesThePhysicalKeyAndWaitsForModifierRelease()
    {
        var processor = new PhysicalShortcutProcessor();
        processor.UpdateBindings(
        [
            new SpotlightShortcutBinding(
                SpotlightActionIds.Uuid,
                new GlobalShortcut
                {
                    Modifiers = GlobalShortcut.ControlModifier,
                    ScanCode = SlashScanCode,
                    VirtualKey = SlashVirtualKey
                })
        ]);

        var controlKey = new PhysicalKey(ControlVirtualKey, 0, false);
        var shortcutKey = new PhysicalKey(OemMinusVirtualKey, SlashScanCode, false);

        var controlDown = processor.Process(new PhysicalKeyboardEvent(controlKey, true));
        var shortcutDown = processor.Process(new PhysicalKeyboardEvent(shortcutKey, true));
        var shortcutUp = processor.Process(new PhysicalKeyboardEvent(shortcutKey, false));
        var controlUp = processor.Process(new PhysicalKeyboardEvent(controlKey, false));

        TestAssert.False(controlDown.Suppress);
        TestAssert.True(shortcutDown.Suppress);
        TestAssert.Equal<string?>(null, shortcutUp.ActionId);
        TestAssert.Equal(SpotlightActionIds.Uuid, controlUp.ActionId);
    }

    internal static void ProcessCapturesTheRawPhysicalKey()
    {
        var processor = new PhysicalShortcutProcessor();
        processor.BeginCapture();
        var controlKey = new PhysicalKey(ControlVirtualKey, 0, false);
        var shortcutKey = new PhysicalKey(OemMinusVirtualKey, SlashScanCode, false);

        processor.Process(new PhysicalKeyboardEvent(controlKey, true));
        var capture = processor.Process(new PhysicalKeyboardEvent(shortcutKey, true));

        var shortcut = capture.CaptureResult?.Shortcut;
        TestAssert.Equal(GlobalShortcut.ControlModifier, shortcut?.Modifiers);
        TestAssert.Equal(SlashScanCode, shortcut?.ScanCode);
        TestAssert.Equal(OemMinusVirtualKey, shortcut?.VirtualKey);
    }

    internal static void ProcessRejectsShortcutsWithoutAModifier()
    {
        var processor = new PhysicalShortcutProcessor();
        processor.BeginCapture();

        var capture = processor.Process(new PhysicalKeyboardEvent(new PhysicalKey(SlashVirtualKey, UuidShortcutScanCode, false), true));

        TestAssert.Equal("Each shortcut must include Ctrl, Alt, or Shift.", capture.CaptureResult?.ErrorMessage);
    }
}
