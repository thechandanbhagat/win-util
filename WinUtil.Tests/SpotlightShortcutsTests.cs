using WinUtil.Models;

namespace WinUtil.Tests;

internal static class SpotlightShortcutsTests
{
    private const uint LetterJVirtualKey = 0x4A;

    internal static void NormalizeRemovesInvalidShortcuts()
    {
        var shortcuts = new SpotlightShortcuts
        {
            Uuid = new GlobalShortcut { Modifiers = 0, VirtualKey = LetterJVirtualKey }
        };

        var normalized = shortcuts.Normalize();

        TestAssert.Equal<GlobalShortcut?>(null, normalized.Uuid);
    }

    internal static void NormalizePreservesValidShortcuts()
    {
        var shortcut = new GlobalShortcut
        {
            Modifiers = GlobalShortcut.ControlModifier | GlobalShortcut.ShiftModifier,
            VirtualKey = LetterJVirtualKey
        };
        var shortcuts = new SpotlightShortcuts { FormatJson = shortcut };

        var normalized = shortcuts.Normalize();

        TestAssert.Equal(shortcut, normalized.FormatJson);
    }

    internal static void ValidateRejectsDuplicateFunctionShortcuts()
    {
        var shortcut = new GlobalShortcut
        {
            Modifiers = GlobalShortcut.ControlModifier,
            VirtualKey = LetterJVirtualKey
        };
        var shortcuts = new SpotlightShortcuts { Password = shortcut, Uuid = shortcut };

        var error = shortcuts.Validate();

        TestAssert.Equal("Each Spotlight function needs a different shortcut.", error);
    }

    internal static void ValidateReservesThePaletteShortcut()
    {
        var shortcuts = new SpotlightShortcuts
        {
            JwtSecret = new GlobalShortcut
            {
                Modifiers = SpotlightShortcuts.PaletteModifiers,
                VirtualKey = SpotlightShortcuts.PaletteVirtualKey
            }
        };

        var error = shortcuts.Validate();

        TestAssert.Equal("Alt+2 is reserved for opening Spotlight.", error);
    }
}
