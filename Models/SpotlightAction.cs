using WinUtil.Services;

namespace WinUtil.Models;

internal sealed class SpotlightAction
{
    private readonly Func<string?, string> createOutput;

    private SpotlightAction(
        string id,
        string title,
        string description,
        IReadOnlyList<string> searchTerms,
        bool requiresSelectedText,
        Func<string?, string> createOutput)
    {
        Id = id;
        Title = title;
        Description = description;
        SearchTerms = searchTerms;
        RequiresSelectedText = requiresSelectedText;
        this.createOutput = createOutput;
    }

    public string Description { get; }

    public string Id { get; }

    public bool RequiresSelectedText { get; }

    public IReadOnlyList<string> SearchTerms { get; }

    public string Title { get; }

    internal static SpotlightAction CreateGenerator(
        string id,
        string title,
        string description,
        IReadOnlyList<string> searchTerms,
        Func<string> generator) => new(id, title, description, searchTerms, false, _ => generator());

    internal static SpotlightAction CreateSelectedTextTransformer(
        string id,
        string title,
        string description,
        IReadOnlyList<string> searchTerms,
        Func<string, string> transformer) => new(
        id,
        title,
        description,
        searchTerms,
        true,
        selectedText => transformer(selectedText ?? throw new SelectedTextUnavailableException()));

    internal string GenerateOutput()
    {
        if (RequiresSelectedText)
        {
            throw new InvalidOperationException("This Spotlight action requires selected text.");
        }

        return createOutput(null);
    }

    internal bool Matches(string query) => SearchTerms.Append(Title).Any(searchTerm =>
        searchTerm.Contains(query, StringComparison.OrdinalIgnoreCase));

    internal string TransformSelectedText(string selectedText)
    {
        if (!RequiresSelectedText)
        {
            throw new InvalidOperationException("This Spotlight action does not transform selected text.");
        }

        return createOutput(selectedText);
    }
}
