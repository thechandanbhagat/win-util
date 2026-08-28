using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WinUtil.Models;
using WinUtil.Services;

namespace WinUtil.ViewModels;

internal sealed class SpotlightViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<SpotlightAction> actions;
    private readonly ObservableCollection<SpotlightAction> matchingActions = [];
    private string errorMessage = string.Empty;
    private string query = string.Empty;
    private SpotlightAction? selectedAction;

    internal SpotlightViewModel(IReadOnlyList<SpotlightAction> actions)
    {
        this.actions = actions;
        MatchingActions = new ReadOnlyObservableCollection<SpotlightAction>(matchingActions);
        RefreshMatches();
    }

    public string ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (errorMessage == value)
            {
                return;
            }

            errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasMatches => MatchingActions.Count > 0;

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);

    public ReadOnlyObservableCollection<SpotlightAction> MatchingActions { get; }

    public string Query
    {
        get => query;
        set
        {
            var nextQuery = value ?? string.Empty;

            if (query == nextQuery)
            {
                return;
            }

            query = nextQuery;
            ErrorMessage = string.Empty;
            RefreshMatches();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasQuery));
        }
    }

    public SpotlightAction? SelectedAction
    {
        get => selectedAction;
        set
        {
            if (selectedAction == value)
            {
                return;
            }

            selectedAction = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void MoveSelection(int offset)
    {
        if (MatchingActions.Count == 0)
        {
            return;
        }

        var selectedIndex = SelectedAction is null ? 0 : MatchingActions.IndexOf(SelectedAction);
        selectedIndex = Math.Max(selectedIndex, 0);
        var nextIndex = (selectedIndex + offset) % MatchingActions.Count;

        SelectedAction = MatchingActions[nextIndex < 0 ? nextIndex + MatchingActions.Count : nextIndex];
    }

    internal void Reset()
    {
        query = string.Empty;
        ErrorMessage = string.Empty;
        SelectedAction = null;
        RefreshMatches();
        OnPropertyChanged(nameof(Query));
        OnPropertyChanged(nameof(HasQuery));
    }

    internal void ShowInsertionError(string message) => ErrorMessage = message;

    private void RefreshMatches()
    {
        var previousSelection = SelectedAction;
        matchingActions.Clear();

        var filteredActions = HasQuery
            ? SpotlightActionCatalog.Filter(actions, Query)
            : [];

        foreach (var action in filteredActions)
        {
            matchingActions.Add(action);
        }

        SelectedAction = previousSelection is not null && MatchingActions.Contains(previousSelection)
            ? previousSelection
            : MatchingActions.FirstOrDefault();
        OnPropertyChanged(nameof(HasMatches));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
