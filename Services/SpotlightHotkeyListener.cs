using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WinUtil.Services;

internal sealed class SpotlightHotkeyListener : IDisposable
{
    internal const int AlreadyRegisteredErrorCode = 1409;
    internal const string ShortcutDisplayName = "Alt+2";

    private const uint AltModifier = 0x0001;
    private const uint DigitTwoVirtualKey = 0x32;
    private const int PaletteHotkeyId = 1;
    private const int HotkeyMessage = 0x0312;

    private readonly HwndSource source;
    private readonly HwndSourceHook sourceHook;
    private readonly Action toggleSpotlight;
    private bool hotkeyRegistered;

    internal SpotlightHotkeyListener(IntPtr sourceWindowHandle, Action toggleSpotlight)
    {
        source = HwndSource.FromHwnd(sourceWindowHandle)
            ?? throw new InvalidOperationException("The widget window must have a Win32 source before registering the Spotlight shortcut.");
        this.toggleSpotlight = toggleSpotlight;
        sourceHook = HandleWindowMessage;
        source.AddHook(sourceHook);

        if (!RegisterHotKey(sourceWindowHandle, PaletteHotkeyId, AltModifier, DigitTwoVirtualKey))
        {
            source.RemoveHook(sourceHook);
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to register the {ShortcutDisplayName} shortcut.");
        }

        hotkeyRegistered = true;
    }

    public void Dispose()
    {
        if (!hotkeyRegistered)
        {
            return;
        }

        if (!UnregisterHotKey(source.Handle, PaletteHotkeyId))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to unregister the {ShortcutDisplayName} shortcut.");
        }

        hotkeyRegistered = false;
        source.RemoveHook(sourceHook);
    }

    private IntPtr HandleWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != HotkeyMessage)
        {
            return IntPtr.Zero;
        }

        if (wParam.ToInt32() != PaletteHotkeyId)
        {
            return IntPtr.Zero;
        }

        toggleSpotlight();
        handled = true;
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int identifier, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int identifier);
}
