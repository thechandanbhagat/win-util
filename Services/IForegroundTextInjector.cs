namespace WinUtil.Services;

internal interface IForegroundTextInjector
{
    IntPtr CaptureForegroundWindow();

    void InsertText(IntPtr targetWindow, string text);

    Task ReplaceSelectedTextAsync(IntPtr targetWindow, Func<string, string> transformer);
}
