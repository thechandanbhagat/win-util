using System.Windows.Input;
using WinUtil.Models;

namespace WinUtil.Services;

internal static class ShortcutFormatter
{
    internal const string UnassignedText = "Not assigned";

    internal static string Format(GlobalShortcut? shortcut)
    {
        if (shortcut is null)
        {
            return UnassignedText;
        }

        var parts = new List<string>();

        if ((shortcut.Modifiers & GlobalShortcut.ControlModifier) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((shortcut.Modifiers & GlobalShortcut.AltModifier) != 0)
        {
            parts.Add("Alt");
        }

        if ((shortcut.Modifiers & GlobalShortcut.ShiftModifier) != 0)
        {
            parts.Add("Shift");
        }

        parts.Add(FormatKey(KeyInterop.KeyFromVirtualKey((int)shortcut.VirtualKey)));
        return string.Join('+', parts);
    }

    private static string FormatKey(Key key) => key switch
    {
        Key.D0 => "0",
        Key.D1 => "1",
        Key.D2 => "2",
        Key.D3 => "3",
        Key.D4 => "4",
        Key.D5 => "5",
        Key.D6 => "6",
        Key.D7 => "7",
        Key.D8 => "8",
        Key.D9 => "9",
        Key.OemMinus => "-",
        Key.OemPlus => "+",
        Key.OemQuestion => "/",
        _ => key.ToString()
    };
}
