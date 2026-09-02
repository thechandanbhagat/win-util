using WinUtil.Models;
using WinUtil.Services;

namespace WinUtil.Tests;

internal static class SettingsStoreTests
{
    internal static void LoadReturnsDefaultsWhenNoFileExists()
    {
        using var directory = new TemporaryDirectory();
        var store = new SettingsStore(directory.PathFor("settings.json"));

        var result = store.Load();

        TestAssert.Equal(WidgetSettings.Default, result.Settings);
        TestAssert.Equal(null, result.Warning);
    }

    internal static void SaveThenLoadRoundTripsSettings()
    {
        using var directory = new TemporaryDirectory();
        var store = new SettingsStore(directory.PathFor("nested", "settings.json"));
        var settings = WidgetSettings.Default with { ShowSeconds = !WidgetSettings.Default.ShowSeconds, PasswordLength = 32 };

        store.Save(settings);
        var result = store.Load();

        TestAssert.Equal(settings.ShowSeconds, result.Settings.ShowSeconds);
        TestAssert.Equal(32, result.Settings.PasswordLength);
        TestAssert.Equal(null, result.Warning);
    }

    internal static void LoadQuarantinesCorruptJsonAndReturnsDefaults()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = directory.PathFor("settings.json");
        File.WriteAllText(settingsPath, "{ not json");
        var store = new SettingsStore(settingsPath);

        var result = store.Load();

        TestAssert.Equal(WidgetSettings.Default, result.Settings);
        TestAssert.True(result.Warning is not null);
        TestAssert.False(File.Exists(settingsPath));
        TestAssert.True(File.Exists(settingsPath + ".corrupt"));
    }

    internal static void LoadTreatsJsonNullAsDefaults()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = directory.PathFor("settings.json");
        File.WriteAllText(settingsPath, "null");
        var store = new SettingsStore(settingsPath);

        var result = store.Load();

        TestAssert.Equal(WidgetSettings.Default, result.Settings);
        TestAssert.Equal(null, result.Warning);
    }
}
