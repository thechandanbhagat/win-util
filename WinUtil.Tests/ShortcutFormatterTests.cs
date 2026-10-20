using WinUtil.Models;
using WinUtil.Services;

namespace WinUtil.Tests;

internal static class ShortcutFormatterTests
{
    private const uint Digit2VirtualKey = 0x32;
    private const uint LetterJVirtualKey = 0x4A;

    internal static void FormatReturnsUnassignedForNull() =>
        TestAssert.Equal(ShortcutFormatter.UnassignedText, ShortcutFormatter.Format(null));

    internal static void FormatOrdersModifiersCtrlAltShift()
    {
        var shortcut = new GlobalShortcut
        {
            Modifiers = GlobalShortcut.ShiftModifier | GlobalShortcut.AltModifier | GlobalShortcut.ControlModifier,
            VirtualKey = LetterJVirtualKey
        };

        TestAssert.Equal("Ctrl+Alt+Shift+J", ShortcutFormatter.Format(shortcut));
    }

    internal static void FormatUsesPlainDigitsForNumberKeys()
    {
        var shortcut = new GlobalShortcut { Modifiers = GlobalShortcut.AltModifier, VirtualKey = Digit2VirtualKey };

        TestAssert.Equal("Alt+2", ShortcutFormatter.Format(shortcut));
    }
}
