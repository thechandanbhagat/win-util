using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinUtil.Models;
using WinUtil.Services;
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
            () => { });

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
