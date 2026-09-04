using System.Windows.Interop;
using WinUtil.Models;
using WinUtil.Services;
using WinUtil.Views;

namespace WinUtil;

public partial class App : System.Windows.Application
{
    internal const string SpotlightShortcutsUnavailableMessage = "Spotlight shortcuts are unavailable until the desktop widget has started.";

    private SettingsStore? settingsStore;
    private SettingsWindow? settingsWindow;
    private SingleInstanceGate? singleInstanceGate;
    private ISpotlightShortcutManager? spotlightShortcutManager;
    private SpotlightController? spotlightController;
    private TrayIconController? trayIconController;
    private WidgetWindow? widgetWindow;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        singleInstanceGate = new SingleInstanceGate("Local\\WinUtil");
        if (!singleInstanceGate.IsPrimaryInstance)
        {
            Shutdown();
            return;
        }

        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        settingsStore = new SettingsStore();
        var widget = new WidgetWindow(settingsStore.Load(), settingsStore.Save, new BatteryStatusProvider(), new AudioDeviceProvider());
        widgetWindow = widget;
        var spotlightWindow = new SpotlightWindow(() => widget.CurrentSettings);
        spotlightController = new SpotlightController(spotlightWindow, new ForegroundTextInjector(), () => widget.CurrentSettings);
        trayIconController = new TrayIconController(ShowWidget, ShowSettings, ExitApplication);
        widgetWindow.ShowWidget();
        RegisterSpotlightShortcuts();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        spotlightShortcutManager?.Dispose();
        spotlightController?.Dispose();
        trayIconController?.Dispose();
        singleInstanceGate?.Dispose();
        base.OnExit(e);
    }

    private void ExitApplication()
    {
        spotlightShortcutManager?.Dispose();
        spotlightShortcutManager = null;
        spotlightController?.Dispose();
        spotlightController = null;
        widgetWindow?.AllowClose();
        Shutdown();
    }

    private void ShowSettings()
    {
        if (settingsStore is null || widgetWindow is null)
        {
            return;
        }

        if (settingsWindow is null)
        {
            settingsWindow = new SettingsWindow(
                settingsStore,
                widgetWindow.CurrentSettings,
                TryApplySpotlightShortcuts,
                BeginShortcutCapture,
                EndShortcutCapture);
            settingsWindow.SettingsSaved += widgetWindow.ApplySettings;
            settingsWindow.Closed += HandleSettingsWindowClosed;
        }

        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private void ShowWidget()
    {
        if (widgetWindow is null)
        {
            return;
        }

        widgetWindow.ShowWidget();
    }

    private void HandleSettingsWindowClosed(object? sender, EventArgs e)
    {
        settingsWindow = null;
    }

    private void RegisterSpotlightShortcuts()
    {
        if (widgetWindow is null || spotlightController is null)
        {
            return;
        }

        spotlightShortcutManager = new SpotlightShortcutManager(
            new WindowInteropHelper(widgetWindow).Handle,
            spotlightController.Toggle,
            spotlightController.ExecuteAction);
        var error = spotlightShortcutManager.TryApply(widgetWindow.CurrentSettings.SpotlightShortcuts);

        if (error is null)
        {
            return;
        }

        trayIconController?.ShowWarning(
            "Command palette shortcut unavailable",
            error);
    }

    private string? TryApplySpotlightShortcuts(SpotlightShortcuts shortcuts) => ApplySpotlightShortcuts(spotlightShortcutManager, shortcuts);

    private void BeginShortcutCapture(Action<ShortcutCaptureResult> captureCallback)
    {
        if (spotlightShortcutManager is null)
        {
            captureCallback(ShortcutCaptureResult.Error(SpotlightShortcutsUnavailableMessage));
            return;
        }

        spotlightShortcutManager.BeginShortcutCapture(captureCallback);
    }

    private void EndShortcutCapture() => spotlightShortcutManager?.EndShortcutCapture();

    internal static string? ApplySpotlightShortcuts(ISpotlightShortcutManager? shortcutManager, SpotlightShortcuts shortcuts) => shortcutManager is null
        ? SpotlightShortcutsUnavailableMessage
        : shortcutManager.TryApply(shortcuts);
}
