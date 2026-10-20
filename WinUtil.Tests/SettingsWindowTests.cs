using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinUtil.Models;
using WinUtil.Services;
using WinUtil.ViewModels;
using WinUtil.Views;

namespace WinUtil.Tests;

internal static class SettingsWindowTests
{
    private const int ExpectedTextToolGroupCount = 3;

    internal static void WindowRendersAShortcutEditorForEveryCatalogAction() => RunOnStaThread(() =>
    {
        EnsureApplicationResources();
        using var directory = new TemporaryDirectory();
        var window = new SettingsWindow(
            new SettingsStore(directory.PathFor("settings.json")),
            WidgetSettings.Default,
            _ => null,
            _ => { },
            () => { },
            _ => null);

        try
        {
            window.Show();
            foreach (var panel in new[] { window.UuidSettingsPanel, window.PasswordSettingsPanel, window.JwtSecretSettingsPanel, window.TextToolsSettingsPanel })
            {
                panel.Visibility = Visibility.Visible;
            }

            window.UpdateLayout();

            var shortcutTextBoxes = FindDescendants<TextBox>(window).Where(textBox => textBox.Tag is string).ToArray();
            var actionIds = shortcutTextBoxes.Select(textBox => (string)textBox.Tag).Order().ToArray();
            var expectedIds = SpotlightActionCatalog.All.Select(action => action.Id).Order().ToArray();

            TestAssert.SequenceEqual(expectedIds, actionIds);
            TestAssert.True(shortcutTextBoxes.All(textBox => textBox.Text == ShortcutFormatter.UnassignedText));
            TestAssert.Equal(ExpectedTextToolGroupCount, FindDescendants<ItemsControl>(window).Count(items => items.ItemsSource is IReadOnlyList<WinUtil.ViewModels.ShortcutSettingViewModel>));
        }
        finally
        {
            window.Close();
        }
    });

    internal static void StartupSettingAppliesAndPersistsWhenSaved() => RunOnStaThread(() =>
    {
        EnsureApplicationResources();
        using var directory = new TemporaryDirectory();
        var store = new SettingsStore(directory.PathFor("settings.json"));
        var registrations = new List<bool>();
        var window = CreateStartupSettingsWindow(store, enabled =>
        {
            registrations.Add(enabled);
            return null;
        });

        try
        {
            window.Show();
            TestAssert.True(window.RunAtStartupCheckBox.IsChecked == true);
            window.RunAtStartupCheckBox.IsChecked = false;
            SaveSettings(window);

            TestAssert.SequenceEqual(new[] { false }, registrations);
            TestAssert.False(store.Load().Settings.RunAtStartup);
            TestAssert.False(window.IsVisible);
        }
        finally
        {
            window.Close();
        }
    });

    internal static void StartupSettingShowsAnErrorWhenRegistrationFails() => RunOnStaThread(() =>
    {
        EnsureApplicationResources();
        using var directory = new TemporaryDirectory();
        var settingsPath = directory.PathFor("settings.json");
        var shortcutRegistrations = new List<SpotlightShortcuts>();
        var window = CreateStartupSettingsWindow(new SettingsStore(settingsPath), _ => "Test startup access denied.", shortcuts =>
        {
            shortcutRegistrations.Add(shortcuts);
            return null;
        });

        try
        {
            window.Show();
            window.RunAtStartupCheckBox.IsChecked = false;
            var editedShortcuts = EditUuidShortcut(window);
            SaveSettings(window);

            TestAssert.SequenceEqual(new[] { editedShortcuts, WidgetSettings.Default.SpotlightShortcuts }, shortcutRegistrations);
            TestAssert.Equal(Visibility.Visible, window.StartupErrorText.Visibility);
            TestAssert.Equal("Test startup access denied.", window.StartupErrorText.Text);
            TestAssert.True(window.IsVisible);
            TestAssert.False(File.Exists(settingsPath));
        }
        finally
        {
            window.Close();
        }
    });

    internal static void StartupSettingRollsBackWhenSettingsCannotBeSaved() => RunOnStaThread(() =>
    {
        EnsureApplicationResources();
        using var directory = new TemporaryDirectory();
        var settingsPath = directory.PathFor("settings.json");
        Directory.CreateDirectory(settingsPath);
        var registrations = new List<bool>();
        var shortcutRegistrations = new List<SpotlightShortcuts>();
        var window = CreateStartupSettingsWindow(new SettingsStore(settingsPath), enabled =>
        {
            registrations.Add(enabled);
            return null;
        }, shortcuts =>
        {
            shortcutRegistrations.Add(shortcuts);
            return null;
        });

        try
        {
            window.Show();
            window.RunAtStartupCheckBox.IsChecked = false;
            var editedShortcuts = EditUuidShortcut(window);
            SaveSettings(window);

            TestAssert.SequenceEqual(new[] { false, true }, registrations);
            TestAssert.SequenceEqual(new[] { editedShortcuts, WidgetSettings.Default.SpotlightShortcuts }, shortcutRegistrations);
            TestAssert.Equal(Visibility.Visible, window.StartupErrorText.Visibility);
            TestAssert.True(window.StartupErrorText.Text.StartsWith("Settings could not be saved.", StringComparison.Ordinal));
            TestAssert.True(window.IsVisible);
        }
        finally
        {
            window.Close();
        }
    });

    internal static void FailedSaveReportsShortcutAndStartupRollbackErrors() => RunOnStaThread(() =>
    {
        EnsureApplicationResources();

        foreach (var settingsSaveFails in new[] { false, true })
        {
            using var directory = new TemporaryDirectory();
            var settingsPath = directory.PathFor("settings.json");

            if (settingsSaveFails)
            {
                Directory.CreateDirectory(settingsPath);
            }

            var shortcutRegistrations = new List<SpotlightShortcuts>();
            var window = CreateStartupSettingsWindow(new SettingsStore(settingsPath), enabled => settingsSaveFails
                ? enabled ? "Test startup rollback failed." : null
                : "Test startup access denied.", shortcuts =>
            {
                shortcutRegistrations.Add(shortcuts);
                return shortcutRegistrations.Count > 1 ? "Test shortcut rollback failed." : null;
            });

            try
            {
                window.Show();
                window.RunAtStartupCheckBox.IsChecked = false;
                var editedShortcuts = EditUuidShortcut(window);
                SaveSettings(window);

                TestAssert.SequenceEqual(new[] { editedShortcuts, WidgetSettings.Default.SpotlightShortcuts }, shortcutRegistrations);
                TestAssert.True(window.StartupErrorText.Text.Contains("Test shortcut rollback failed.", StringComparison.Ordinal));
                TestAssert.True(window.StartupErrorText.Text.Contains(settingsSaveFails ? "Settings could not be saved." : "Test startup access denied.", StringComparison.Ordinal));

                if (settingsSaveFails)
                {
                    TestAssert.True(window.StartupErrorText.Text.Contains("Test startup rollback failed.", StringComparison.Ordinal));
                }
            }
            finally
            {
                window.Close();
            }
        }
    });

    private static SpotlightShortcuts EditUuidShortcut(SettingsWindow window)
    {
        var editedShortcuts = WidgetSettings.Default.SpotlightShortcuts with
        {
            Uuid = new GlobalShortcut
            {
                Modifiers = GlobalShortcut.ControlModifier,
                VirtualKey = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.U)
            }
        };
        ((ShortcutSettingsViewModel)window.DataContext)[SpotlightActionIds.Uuid].Shortcut = editedShortcuts.Uuid;
        return editedShortcuts;
    }

    private static SettingsWindow CreateStartupSettingsWindow(SettingsStore store, Func<bool, string?> tryApplyStartup, Func<SpotlightShortcuts, string?>? tryApplySpotlightShortcuts = null) => new(
        store,
        WidgetSettings.Default,
        tryApplySpotlightShortcuts ?? (_ => null),
        _ => { },
        () => { },
        tryApplyStartup);

    private static void SaveSettings(SettingsWindow window)
    {
        window.UpdateLayout();
        FindDescendants<Button>(window).Single(button => Equals(button.Content, "Save changes")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static void EnsureApplicationResources()
    {
        if (System.Windows.Application.Current is not null)
        {
            return;
        }

        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException(failure.Message, failure);
        }
    }
}
