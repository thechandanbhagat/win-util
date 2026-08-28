using System.ComponentModel;
using WinUtil.Models;

namespace WinUtil.Services;

internal interface ISpotlightShortcutManager : IDisposable
{
    void BeginShortcutCapture(Action<ShortcutCaptureResult> captureCallback);

    void EndShortcutCapture();

    string? TryApply(SpotlightShortcuts shortcuts);
}

internal sealed class SpotlightShortcutManager : ISpotlightShortcutManager
{
    private readonly Action<string> executeAction;
    private readonly IntPtr sourceWindowHandle;
    private readonly Action toggleSpotlight;
    private PhysicalShortcutListener? actionShortcutListener;
    private SpotlightHotkeyListener? paletteHotkeyListener;

    internal SpotlightShortcutManager(IntPtr sourceWindowHandle, Action toggleSpotlight, Action<string> executeAction)
    {
        this.sourceWindowHandle = sourceWindowHandle;
        this.toggleSpotlight = toggleSpotlight;
        this.executeAction = executeAction;
    }

    public void Dispose()
    {
        actionShortcutListener?.Dispose();
        actionShortcutListener = null;
        paletteHotkeyListener?.Dispose();
        paletteHotkeyListener = null;
    }

    internal void BeginShortcutCapture(Action<ShortcutCaptureResult> captureCallback)
    {
        if (actionShortcutListener is null)
        {
            captureCallback(ShortcutCaptureResult.Error("Windows could not listen for Spotlight shortcuts."));
            return;
        }

        actionShortcutListener.BeginCapture(captureCallback);
    }

    internal void EndShortcutCapture() => actionShortcutListener?.EndCapture();

    internal string? TryApply(SpotlightShortcuts shortcuts)
    {
        var normalizedShortcuts = shortcuts.Normalize();
        var validationError = normalizedShortcuts.Validate();

        if (validationError is not null)
        {
            return validationError;
        }

        if (actionShortcutListener is null)
        {
            var startupError = TryStartListeners();

            if (startupError is not null)
            {
                return startupError;
            }
        }

        var listener = actionShortcutListener
            ?? throw new InvalidOperationException("The Spotlight shortcut listener did not start.");
        listener.UpdateBindings(normalizedShortcuts.GetBindings());
        return null;
    }

    private string? TryStartListeners()
    {
        try
        {
            paletteHotkeyListener = new SpotlightHotkeyListener(sourceWindowHandle, toggleSpotlight);
            actionShortcutListener = new PhysicalShortcutListener(executeAction);
            return null;
        }
        catch (Win32Exception exception)
        {
            actionShortcutListener?.Dispose();
            actionShortcutListener = null;
            paletteHotkeyListener?.Dispose();
            paletteHotkeyListener = null;

            return exception.NativeErrorCode == SpotlightHotkeyListener.AlreadyRegisteredErrorCode
                ? $"{SpotlightHotkeyListener.ShortcutDisplayName} is already in use by another app."
                : exception.Message;
        }
    }

    void ISpotlightShortcutManager.BeginShortcutCapture(Action<ShortcutCaptureResult> captureCallback) => BeginShortcutCapture(captureCallback);

    void ISpotlightShortcutManager.EndShortcutCapture() => EndShortcutCapture();

    string? ISpotlightShortcutManager.TryApply(SpotlightShortcuts shortcuts) => TryApply(shortcuts);
}
