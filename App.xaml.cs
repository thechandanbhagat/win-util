using WinUtil.Services;
using WinUtil.Views;

namespace WinUtil;

public partial class App : System.Windows.Application
{
    private SingleInstanceGate? singleInstanceGate;
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
        var settingsStore = new SettingsStore();
        widgetWindow = new WidgetWindow(settingsStore.Load(), settingsStore.Save, new BatteryStatusProvider());
        trayIconController = new TrayIconController(ShowWidget, ExitApplication);
        widgetWindow.ShowWidget();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        trayIconController?.Dispose();
        singleInstanceGate?.Dispose();
        base.OnExit(e);
    }

    private void ExitApplication()
    {
        trayIconController?.Dispose();
        trayIconController = null;
        widgetWindow?.AllowClose();
        Shutdown();
    }

    private void ShowWidget() => widgetWindow?.ShowWidget();
}
