using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinUtil.Models;
using WinUtil.Services;

namespace WinUtil.Views;

public partial class WidgetWindow : Window
{
    private static readonly TimeSpan AutoHidePollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan BatteryUpdateInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ClockUpdateInterval = TimeSpan.FromSeconds(1);
    private static readonly Duration VisibilityFadeDuration = new(TimeSpan.FromMilliseconds(300));
    private const double MinimumVisibleWidgetArea = 48;

    private readonly DispatcherTimer autoHideTimer;
    private readonly IBatteryStatusProvider batteryStatusProvider;
    private readonly DispatcherTimer batteryTimer;
    private readonly CancellationTokenSource batteryUpdateCancellation = new();
    private readonly DispatcherTimer clockTimer;
    private readonly WidgetPositionPersistence positionPersistence;
    private bool canClose;
    private WidgetSettings currentSettings;
    private bool isFaded;
    private bool isBatteryUpdateInProgress;
    private DateTime lastMouseLeaveAt;

    internal WidgetWindow(
        WidgetSettings settings,
        Action<WidgetSettings> saveSettings,
        IBatteryStatusProvider batteryStatusProvider)
    {
        InitializeComponent();
        this.batteryStatusProvider = batteryStatusProvider;
        currentSettings = settings.Normalize();
        positionPersistence = new WidgetPositionPersistence(() => currentSettings, saveSettings);
        autoHideTimer = new DispatcherTimer { Interval = AutoHidePollInterval };
        autoHideTimer.Tick += HandleAutoHideTick;
        clockTimer = new DispatcherTimer { Interval = ClockUpdateInterval };
        clockTimer.Tick += HandleClockTick;
        batteryTimer = new DispatcherTimer { Interval = BatteryUpdateInterval };
        batteryTimer.Tick += HandleBatteryTimerTick;
        LocationChanged += HandleLocationChanged;
        RestorePosition();
        clockTimer.Start();
        batteryTimer.Start();
        ApplySettings(currentSettings);
        _ = UpdateBatteryStatusAsync();
    }

    internal WidgetSettings CurrentSettings => currentSettings;

    internal void AllowClose()
    {
        positionPersistence.SaveNow();
        canClose = true;
        Close();
    }

    internal void ShowWidget()
    {
        if (!IsVisible)
        {
            Show();
        }

        Activate();
        FadeIn();
        StartAutoHideCountdown();
    }

    internal void ApplySettings(WidgetSettings settings)
    {
        currentSettings = settings.Normalize();
        Opacity = currentSettings.Opacity;
        Topmost = currentSettings.KeepOnTop;
        WidgetBackgroundBrush.Opacity = currentSettings.BackgroundOpacity;

        if (isFaded)
        {
            Opacity = currentSettings.FadedOpacity;
        }

        UpdateClock();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!canClose)
        {
            positionPersistence.SaveNow();
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        LocationChanged -= HandleLocationChanged;
        positionPersistence.Dispose();
        autoHideTimer.Stop();
        autoHideTimer.Tick -= HandleAutoHideTick;
        batteryTimer.Stop();
        batteryTimer.Tick -= HandleBatteryTimerTick;
        batteryUpdateCancellation.Cancel();
        batteryUpdateCancellation.Dispose();
        clockTimer.Stop();
        clockTimer.Tick -= HandleClockTick;
        base.OnClosed(e);
    }

    private void HandleClockTick(object? sender, EventArgs e)
    {
        UpdateClock();
    }

    private async void HandleBatteryTimerTick(object? sender, EventArgs e)
    {
        await UpdateBatteryStatusAsync();
    }

    private void HandleLocationChanged(object? sender, EventArgs e)
    {
        if (double.IsNaN(Left) || double.IsNaN(Top))
        {
            return;
        }

        currentSettings = currentSettings with { Left = Left, Top = Top };
        positionPersistence.ScheduleSave();
    }

    private void HandleAutoHideTick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.UtcNow - lastMouseLeaveAt;

        if (elapsed >= TimeSpan.FromSeconds(currentSettings.AutoHideDelaySeconds))
        {
            autoHideTimer.Stop();
            FadeOut();
        }
    }

    private void HandleDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void HandleMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        autoHideTimer.Stop();

        if (isFaded)
        {
            FadeIn();
        }
    }

    private void HandleMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        StartAutoHideCountdown();
    }

    private void FadeIn()
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
        isFaded = false;
        BeginAnimation(OpacityProperty, CreateOpacityAnimation(currentSettings.Opacity));
    }

    private void FadeOut()
    {
        if (IsMouseOver)
        {
            return;
        }

        isFaded = true;
        BeginAnimation(OpacityProperty, CreateOpacityAnimation(currentSettings.FadedOpacity));
    }

    private static DoubleAnimation CreateOpacityAnimation(double targetOpacity) => new()
    {
        Duration = VisibilityFadeDuration,
        To = targetOpacity
    };

    private void StartAutoHideCountdown()
    {
        lastMouseLeaveAt = DateTime.UtcNow;
        autoHideTimer.Start();
    }

    private void RestorePosition()
    {
        if (currentSettings.Left is not double savedLeft || currentSettings.Top is not double savedTop)
        {
            return;
        }

        WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
        Left = Math.Clamp(savedLeft, MinimumLeft, MaximumLeft);
        Top = Math.Clamp(savedTop, MinimumTop, MaximumTop);
    }

    private double MinimumLeft => SystemParameters.VirtualScreenLeft - Width + MinimumVisibleWidgetArea;

    private double MaximumLeft => SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - MinimumVisibleWidgetArea;

    private double MinimumTop => SystemParameters.VirtualScreenTop - Height + MinimumVisibleWidgetArea;

    private double MaximumTop => SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - MinimumVisibleWidgetArea;

    private void UpdateClock()
    {
        var now = DateTime.Now;
        var timeFormat = currentSettings.ShowSeconds ? "HH:mm:ss" : "HH:mm";

        TimeText.Text = now.ToString(timeFormat, CultureInfo.CurrentCulture);
        DateText.Text = now.ToString("dddd, d MMMM", CultureInfo.CurrentCulture);
    }

    private async Task UpdateBatteryStatusAsync()
    {
        if (isBatteryUpdateInProgress || batteryUpdateCancellation.IsCancellationRequested)
        {
            return;
        }

        isBatteryUpdateInProgress = true;

        try
        {
            var snapshot = await batteryStatusProvider.GetSnapshotAsync(batteryUpdateCancellation.Token);
            BatteryDevicesItemsControl.ItemsSource = snapshot.Devices;
            BatteryDevicesItemsControl.Visibility = snapshot.HasDevices ? Visibility.Visible : Visibility.Collapsed;
            BatteryEmptyText.Visibility = snapshot.HasDevices ? Visibility.Collapsed : Visibility.Visible;
            BatteryStatusText.Text = snapshot.BluetoothErrorMessage is null ? "LIVE" : "UNAVAILABLE";
        }
        catch (OperationCanceledException) when (batteryUpdateCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            isBatteryUpdateInProgress = false;
        }
    }
}
