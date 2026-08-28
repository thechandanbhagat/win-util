using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WinUtil.Models;
using WinUtil.Services;
using WinUtil.ViewModels;

namespace WinUtil.Views;

public partial class SpotlightWindow : Window
{
    private readonly SpotlightViewModel viewModel;

    internal SpotlightWindow(Func<WidgetSettings> settingsProvider)
    {
        InitializeComponent();
        viewModel = new SpotlightViewModel(SpotlightActionCatalog.Create(settingsProvider));
        DataContext = viewModel;
    }

    internal event EventHandler<SpotlightAction>? ActionRequested;

    internal void ShowPalette()
    {
        viewModel.Reset();

        if (!IsVisible)
        {
            Show();
        }

        FocusSearchBox();
    }

    internal void RestoreAfterInsertionFailure(string errorMessage)
    {
        if (!IsVisible)
        {
            Show();
        }

        viewModel.ShowInsertionError(errorMessage);
        FocusSearchBox();
        CommandTextBox.SelectAll();
    }

    private void HandlePreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            RequestSelectedAction();
            return;
        }

        if (e.Key is Key.Down or Key.Up)
        {
            viewModel.MoveSelection(e.Key == Key.Down ? 1 : -1);
            e.Handled = true;
        }
    }

    private void HandleActionDoubleClick(object sender, MouseButtonEventArgs e) => RequestSelectedAction();

    private void HandleDeactivated(object? sender, EventArgs e) => Hide();

    private void FocusSearchBox()
    {
        Activate();
        Dispatcher.BeginInvoke(() =>
        {
            Activate();
            CommandTextBox.Focus();
            Keyboard.Focus(CommandTextBox);
        }, DispatcherPriority.Input);
    }

    private void RequestSelectedAction()
    {
        if (viewModel.SelectedAction is { } selectedAction)
        {
            ActionRequested?.Invoke(this, selectedAction);
        }
    }
}
