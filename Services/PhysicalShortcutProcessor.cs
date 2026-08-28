using WinUtil.Models;

namespace WinUtil.Services;

internal readonly record struct PhysicalKey(uint VirtualKey, uint ScanCode, bool IsExtendedKey);

internal readonly record struct PhysicalKeyboardEvent(PhysicalKey Key, bool IsKeyDown);

internal sealed record ShortcutCaptureResult(GlobalShortcut? Shortcut, string? ErrorMessage)
{
    internal static ShortcutCaptureResult Capture(GlobalShortcut shortcut) => new(shortcut, null);

    internal static ShortcutCaptureResult Error(string message) => new(null, message);
}

internal readonly record struct ShortcutProcessingResult(
    bool Suppress,
    string? ActionId,
    ShortcutCaptureResult? CaptureResult);

internal sealed class PhysicalShortcutProcessor
{
    private const uint ControlVirtualKey = 0x11;
    private const uint LeftControlVirtualKey = 0xA2;
    private const uint RightControlVirtualKey = 0xA3;
    private const uint ShiftVirtualKey = 0x10;
    private const uint LeftShiftVirtualKey = 0xA0;
    private const uint RightShiftVirtualKey = 0xA1;
    private const uint AltVirtualKey = 0x12;
    private const uint LeftAltVirtualKey = 0xA4;
    private const uint RightAltVirtualKey = 0xA5;
    private const uint LeftWindowsVirtualKey = 0x5B;
    private const uint RightWindowsVirtualKey = 0x5C;

    private IReadOnlyList<SpotlightShortcutBinding> bindings = [];
    private bool isCapturing;
    private string? pendingActionId;
    private readonly HashSet<PhysicalKey> pressedKeys = [];
    private PhysicalKey? suppressedKey;

    internal void BeginCapture() => isCapturing = true;

    internal void EndCapture() => isCapturing = false;

    internal ShortcutProcessingResult Process(PhysicalKeyboardEvent keyboardEvent)
    {
        if (keyboardEvent.IsKeyDown)
        {
            return ProcessKeyDown(keyboardEvent.Key);
        }

        pressedKeys.Remove(keyboardEvent.Key);
        var suppress = suppressedKey == keyboardEvent.Key;

        if (suppress)
        {
            suppressedKey = null;
        }

        return new ShortcutProcessingResult(suppress, DequeueReleasedAction(), null);
    }

    internal void UpdateBindings(IReadOnlyList<SpotlightShortcutBinding> nextBindings)
    {
        bindings = nextBindings;
        pendingActionId = null;
        suppressedKey = null;
    }

    private ShortcutProcessingResult ProcessKeyDown(PhysicalKey key)
    {
        var isInitialKeyDown = pressedKeys.Add(key);

        if (!isInitialKeyDown)
        {
            return new ShortcutProcessingResult(suppressedKey == key, null, null);
        }

        if (isCapturing && !IsModifierKey(key.VirtualKey))
        {
            var captureResult = CreateCaptureResult(key);

            if (captureResult.Shortcut is not null)
            {
                isCapturing = false;
            }

            return new ShortcutProcessingResult(false, null, captureResult);
        }

        var actionId = bindings.FirstOrDefault(binding => Matches(binding.Shortcut, key, GetModifiers()))?.ActionId;

        if (actionId is null)
        {
            return new ShortcutProcessingResult(false, null, null);
        }

        pendingActionId = actionId;
        suppressedKey = key;
        return new ShortcutProcessingResult(true, null, null);
    }

    private ShortcutCaptureResult CreateCaptureResult(PhysicalKey key)
    {
        var modifiers = GetModifiers();

        if (pressedKeys.Any(pressedKey => IsWindowsKey(pressedKey.VirtualKey)))
        {
            return ShortcutCaptureResult.Error("Windows-key shortcuts are reserved by the operating system.");
        }

        if (modifiers == 0)
        {
            return ShortcutCaptureResult.Error("Each shortcut must include Ctrl, Alt, or Shift.");
        }

        return ShortcutCaptureResult.Capture(new GlobalShortcut
        {
            IsExtendedKey = key.IsExtendedKey,
            Modifiers = modifiers,
            ScanCode = key.ScanCode,
            VirtualKey = key.VirtualKey
        });
    }

    private string? DequeueReleasedAction()
    {
        if (pendingActionId is null || GetModifiers() != 0)
        {
            return null;
        }

        var actionId = pendingActionId;
        pendingActionId = null;
        return actionId;
    }

    private uint GetModifiers()
    {
        var modifiers = 0u;

        if (pressedKeys.Any(key => IsControlKey(key.VirtualKey)))
        {
            modifiers |= GlobalShortcut.ControlModifier;
        }

        if (pressedKeys.Any(key => IsAltKey(key.VirtualKey)))
        {
            modifiers |= GlobalShortcut.AltModifier;
        }

        if (pressedKeys.Any(key => IsShiftKey(key.VirtualKey)))
        {
            modifiers |= GlobalShortcut.ShiftModifier;
        }

        return modifiers;
    }

    private static bool IsAltKey(uint virtualKey) => virtualKey is AltVirtualKey
        or LeftAltVirtualKey
        or RightAltVirtualKey;

    private static bool IsControlKey(uint virtualKey) => virtualKey is ControlVirtualKey
        or LeftControlVirtualKey
        or RightControlVirtualKey;

    private static bool IsModifierKey(uint virtualKey) => IsAltKey(virtualKey)
        || IsControlKey(virtualKey)
        || IsShiftKey(virtualKey)
        || IsWindowsKey(virtualKey);

    private static bool IsShiftKey(uint virtualKey) => virtualKey is ShiftVirtualKey
        or LeftShiftVirtualKey
        or RightShiftVirtualKey;

    private static bool IsWindowsKey(uint virtualKey) => virtualKey is LeftWindowsVirtualKey
        or RightWindowsVirtualKey;

    private static bool Matches(GlobalShortcut shortcut, PhysicalKey key, uint modifiers) => shortcut.Modifiers == modifiers
        && (shortcut.ScanCode == 0
            ? shortcut.VirtualKey == key.VirtualKey
            : shortcut.ScanCode == key.ScanCode && shortcut.IsExtendedKey == key.IsExtendedKey);
}
