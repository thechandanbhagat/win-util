using System.ComponentModel;
using WinUtil.Models;
using WinUtil.Services;

namespace WinUtil.ViewModels;

internal sealed class ShortcutSettingViewModel : INotifyPropertyChanged
{
    private GlobalShortcut? shortcut;

    internal ShortcutSettingViewModel(SpotlightAction action, GlobalShortcut? shortcut)
    {
        ActionId = action.Id;
        Title = action.Title;
        Description = action.Description;
        this.shortcut = shortcut;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ActionId { get; }

    public string Description { get; }

    public string Title { get; }

    public string DisplayText => ShortcutFormatter.Format(shortcut);

    public bool IsAssigned => shortcut is not null;

    public string AutomationHelpText => $"Click and press the physical key combination to assign the {Title} shortcut.";

    public GlobalShortcut? Shortcut
    {
        get => shortcut;
        set
        {
            if (Equals(shortcut, value))
            {
                return;
            }

            shortcut = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Shortcut)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAssigned)));
        }
    }
}

internal sealed record ShortcutSettingGroup(string Title, IReadOnlyList<ShortcutSettingViewModel> Shortcuts);

internal sealed class ShortcutSettingsViewModel
{
    private readonly IReadOnlyDictionary<string, ShortcutSettingViewModel> shortcutsByActionId;

    internal ShortcutSettingsViewModel(IReadOnlyList<SpotlightAction> actions, SpotlightShortcuts shortcuts)
    {
        var settings = actions
            .Select(action => new ShortcutSettingViewModel(action, shortcuts.GetShortcut(action.Id)))
            .ToArray();
        shortcutsByActionId = settings.ToDictionary(setting => setting.ActionId, StringComparer.Ordinal);
        Shortcuts = shortcutsByActionId;
        TextToolGroups = actions
            .Where(action => action.RequiresSelectedText)
            .GroupBy(action => action.Category)
            .Select(group => new ShortcutSettingGroup(group.Key, group.Select(action => shortcutsByActionId[action.Id]).ToArray()))
            .ToArray();
    }

    public IReadOnlyDictionary<string, ShortcutSettingViewModel> Shortcuts { get; }

    public IReadOnlyList<ShortcutSettingGroup> TextToolGroups { get; }

    internal ShortcutSettingViewModel this[string actionId] => shortcutsByActionId.TryGetValue(actionId, out var setting)
        ? setting
        : throw new ArgumentOutOfRangeException(nameof(actionId), actionId, "Unsupported Spotlight action.");

    internal SpotlightShortcuts ToSpotlightShortcuts() => shortcutsByActionId.Values.Aggregate(
        new SpotlightShortcuts(),
        (shortcuts, setting) => shortcuts.WithShortcut(setting.ActionId, setting.Shortcut));
}
