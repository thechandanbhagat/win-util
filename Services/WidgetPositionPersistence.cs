using System.Windows.Threading;
using WinUtil.Models;

namespace WinUtil.Services;

internal sealed class WidgetPositionPersistence : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(250);

    private readonly Func<WidgetSettings> getSettings;
    private readonly Action<WidgetSettings> saveSettings;
    private readonly DispatcherTimer saveTimer;

    internal WidgetPositionPersistence(Func<WidgetSettings> getSettings, Action<WidgetSettings> saveSettings)
    {
        this.getSettings = getSettings;
        this.saveSettings = saveSettings;
        saveTimer = new DispatcherTimer { Interval = SaveDelay };
        saveTimer.Tick += HandleSaveTimerTick;
    }

    public void Dispose()
    {
        saveTimer.Stop();
        saveTimer.Tick -= HandleSaveTimerTick;
    }

    internal void SaveNow()
    {
        saveTimer.Stop();
        saveSettings(getSettings());
    }

    internal void ScheduleSave()
    {
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void HandleSaveTimerTick(object? sender, EventArgs e)
    {
        SaveNow();
    }
}
