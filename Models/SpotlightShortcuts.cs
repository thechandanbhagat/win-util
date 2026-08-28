namespace WinUtil.Models;

internal sealed record SpotlightShortcuts
{
    internal const uint PaletteModifiers = GlobalShortcut.AltModifier;
    internal const uint PaletteVirtualKey = 0x32;

    public GlobalShortcut? FormatJson { get; init; }

    public GlobalShortcut? Base64Decode { get; init; }

    public GlobalShortcut? Base64Encode { get; init; }

    public GlobalShortcut? JwtSecret { get; init; }

    public GlobalShortcut? Lowercase { get; init; }

    public GlobalShortcut? MinifyJson { get; init; }

    public GlobalShortcut? Password { get; init; }

    public GlobalShortcut? Uppercase { get; init; }

    public GlobalShortcut? UrlDecode { get; init; }

    public GlobalShortcut? UrlEncode { get; init; }

    public GlobalShortcut? Uuid { get; init; }

    internal IReadOnlyList<SpotlightShortcutBinding> GetBindings() =>
    [
        .. CreateBinding(SpotlightActionIds.Uuid, Uuid),
        .. CreateBinding(SpotlightActionIds.Password, Password),
        .. CreateBinding(SpotlightActionIds.JwtSecret, JwtSecret),
        .. CreateBinding(SpotlightActionIds.FormatJson, FormatJson),
        .. CreateBinding(SpotlightActionIds.MinifyJson, MinifyJson),
        .. CreateBinding(SpotlightActionIds.Base64Encode, Base64Encode),
        .. CreateBinding(SpotlightActionIds.Base64Decode, Base64Decode),
        .. CreateBinding(SpotlightActionIds.UrlEncode, UrlEncode),
        .. CreateBinding(SpotlightActionIds.UrlDecode, UrlDecode),
        .. CreateBinding(SpotlightActionIds.Uppercase, Uppercase),
        .. CreateBinding(SpotlightActionIds.Lowercase, Lowercase)
    ];

    internal string? Validate()
    {
        var bindings = GetBindings();

        if (bindings.Any(binding => !binding.Shortcut.IsValid))
        {
            return "Each shortcut must include Ctrl, Alt, or Shift and a regular key.";
        }

        if (bindings.Any(IsPaletteShortcut))
        {
            return "Alt+2 is reserved for opening Spotlight.";
        }

        if (bindings.GroupBy(binding => binding.Shortcut).Any(group => group.Count() > 1))
        {
            return "Each Spotlight function needs a different shortcut.";
        }

        return null;
    }

    internal SpotlightShortcuts Normalize() => this with
    {
        FormatJson = NormalizeShortcut(FormatJson),
        Base64Decode = NormalizeShortcut(Base64Decode),
        Base64Encode = NormalizeShortcut(Base64Encode),
        JwtSecret = NormalizeShortcut(JwtSecret),
        Lowercase = NormalizeShortcut(Lowercase),
        MinifyJson = NormalizeShortcut(MinifyJson),
        Password = NormalizeShortcut(Password),
        Uppercase = NormalizeShortcut(Uppercase),
        UrlDecode = NormalizeShortcut(UrlDecode),
        UrlEncode = NormalizeShortcut(UrlEncode),
        Uuid = NormalizeShortcut(Uuid)
    };

    private static IReadOnlyList<SpotlightShortcutBinding> CreateBinding(string actionId, GlobalShortcut? shortcut) => shortcut is null
        ? []
        : [new SpotlightShortcutBinding(actionId, shortcut)];

    private static bool IsPaletteShortcut(SpotlightShortcutBinding binding) => binding.Shortcut.Modifiers == PaletteModifiers
        && binding.Shortcut.VirtualKey == PaletteVirtualKey;

    private static GlobalShortcut? NormalizeShortcut(GlobalShortcut? shortcut) => shortcut is { IsValid: true }
        ? shortcut
        : null;
}

internal sealed record SpotlightShortcutBinding(string ActionId, GlobalShortcut Shortcut);
