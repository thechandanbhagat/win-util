namespace WinUtil.Models;

internal sealed record GlobalShortcut
{
    internal const uint AltModifier = 0x0001;
    internal const uint ControlModifier = 0x0002;
    internal const uint ShiftModifier = 0x0004;
    internal const uint WindowsModifier = 0x0008;
    internal const uint SupportedModifiers = AltModifier | ControlModifier | ShiftModifier;
    internal const uint MinimumVirtualKey = 0x01;
    internal const uint MaximumVirtualKey = 0xFE;

    public uint Modifiers { get; init; }

    public bool IsExtendedKey { get; init; }

    public uint ScanCode { get; init; }

    public uint VirtualKey { get; init; }

    internal bool IsValid => Modifiers != 0
        && (Modifiers & ~SupportedModifiers) == 0
        && VirtualKey is >= MinimumVirtualKey and <= MaximumVirtualKey;
}
