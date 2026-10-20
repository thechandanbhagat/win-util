using System.IO;
using System.ComponentModel;
using System.Security;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinUtil.Models;
using WinUtil.Services;
using WinUtil.Views;

namespace WinUtil;

public partial class App : System.Windows.Application
{
    internal const string SpotlightShortcutsUnavailableMessage = "Spotlight shortcuts are unavailable until the desktop widget has started.";
    internal const string StartupConfigurationArgument = "--configure-startup";

    private const int StartupConfigurationSuccessExitCode = 0;
    private const int StartupConfigurationFailureExitCode = 1;

    private readonly CrashLog crashLog = new();
    private readonly StartupRegistration startupRegistration = new();
    private readonly HashSet<ScreenshotEditorWindow> screenshotEditors = [];
    private ScreenshotCaptureListener? screenshotCaptureListener;
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

        if (e.Args.Contains(StartupConfigurationArgument, StringComparer.Ordinal))
        {
            var startupSettings = new SettingsStore().Load();
            var startupError = TryApplyStartup(startupSettings.Settings.RunAtStartup);

            if (startupError is not null)
            {
                Console.Error.WriteLine(startupError);
            }

            Shutdown(startupError is null ? StartupConfigurationSuccessExitCode : StartupConfigurationFailureExitCode);
            return;
        }

        singleInstanceGate = new SingleInstanceGate("Local\\WinUtil");
        if (!singleInstanceGate.IsPrimaryInstance)
        {
            Shutdown();
            return;
        }

        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        RegisterUnhandledExceptionHandlers();

        settingsStore = new SettingsStore();
        var loadResult = settingsStore.Load();
        var widget = new WidgetWindow(
            loadResult.Settings,
            settingsStore.Save,
            new BatteryStatusProvider(),
            new AudioDeviceProvider());
        widgetWindow = widget;
        var spotlightWindow = new SpotlightWindow(() => widget.CurrentSettings);
        spotlightController = new SpotlightController(spotlightWindow, new ForegroundTextInjector(), () => widget.CurrentSettings);
        trayIconController = new TrayIconController(ShowWidget, ShowSettings, ExitApplication);
        widgetWindow.ShowWidget();
        RegisterSpotlightShortcuts();
        RegisterScreenshotCapture();

        var registrationError = TryApplyStartup(loadResult.Settings.RunAtStartup);

        if (registrationError is not null)
        {
            trayIconController.ShowWarning("Windows startup could not be updated", registrationError);
        }

        if (loadResult.Warning is not null)
        {
            trayIconController.ShowWarning("Settings reset to defaults", loadResult.Warning);
        }
    }

    private void RegisterUnhandledExceptionHandlers()
    {
        DispatcherUnhandledException += HandleDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += HandleDomainUnhandledException;
    }

    private void HandleDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        crashLog.Write("Dispatcher", e.Exception);
        trayIconController?.ShowWarning(
            "WinUtil hit an unexpected error",
            $"{e.Exception.Message} Details were written to {crashLog.LogFilePath}.");
        e.Handled = true;
    }

    private void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        crashLog.Write("Task", e.Exception);
        e.SetObserved();
    }

    private void HandleDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            crashLog.Write("AppDomain", exception);
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        screenshotCaptureListener?.Dispose();
        spotlightShortcutManager?.Dispose();
        spotlightController?.Dispose();
        trayIconController?.Dispose();
        singleInstanceGate?.Dispose();
        base.OnExit(e);
    }

    private void ExitApplication()
    {
        screenshotCaptureListener?.Dispose();
        screenshotCaptureListener = null;
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
                EndShortcutCapture,
                TryApplyStartup,
                () => widgetWindow.CurrentSettings.ScreenshotEditor);
            settingsWindow.SettingsSaved += ApplyWidgetSettings;
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

    private void ApplyWidgetSettings(WidgetSettings settings)
    {
        widgetWindow?.ApplySettings(settings);
        screenshotCaptureListener?.CancelPendingCapture();
    }

    private void RegisterScreenshotCapture()
    {
        if (widgetWindow is null)
        {
            return;
        }

        try
        {
            screenshotCaptureListener = new ScreenshotCaptureListener(
                new WindowInteropHelper(widgetWindow).Handle,
                () => widgetWindow.CurrentSettings.OpenScreenshotEditorAfterCapture,
                ShowScreenshotEditor,
                ReportScreenshotError);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or ArgumentException or AggregateException)
        {
            ReportScreenshotError(exception);
        }
    }

    private void ShowScreenshotEditor(BitmapSource screenshot)
    {
        if (widgetWindow is null || !widgetWindow.CurrentSettings.OpenScreenshotEditorAfterCapture)
        {
            return;
        }

        var editor = new ScreenshotEditorWindow(screenshot, widgetWindow.CurrentSettings.ScreenshotEditor);
        screenshotEditors.Add(editor);
        editor.Closed += HandleScreenshotEditorClosed;
        editor.Show();
        editor.Activate();
    }

    private void HandleScreenshotEditorClosed(object? sender, EventArgs e)
    {
        if (sender is not ScreenshotEditorWindow editor)
        {
            return;
        }

        editor.Closed -= HandleScreenshotEditorClosed;
        screenshotEditors.Remove(editor);

        if (widgetWindow is null || settingsStore is null)
        {
            return;
        }

        var currentOptions = widgetWindow.CurrentSettings.ScreenshotEditor;
        var options = editor.Options.MergeChanges(editor.InitialOptions, currentOptions);

        if (options == currentOptions)
        {
            return;
        }

        var settings = widgetWindow.CurrentSettings with { ScreenshotEditor = options };

        try
        {
            settingsStore.Save(settings);
            widgetWindow.ApplySettings(settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReportScreenshotError(exception);
        }
    }

    private void ReportScreenshotError(Exception exception)
    {
        crashLog.Write("Screenshot editor", exception);
        trayIconController?.ShowWarning("Screenshot editor", exception.Message);
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

    private string? TryApplyStartup(bool enabled)
    {
        try
        {
            startupRegistration.Apply(enabled);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or InvalidOperationException)
        {
            crashLog.Write("Windows startup", exception);
            return $"Windows startup could not be updated. {exception.Message}";
        }
    }

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
