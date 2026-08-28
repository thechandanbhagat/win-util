namespace WinUtil.Services;

internal sealed class SelectedTextUnavailableException : InvalidOperationException
{
    internal SelectedTextUnavailableException()
        : base("No selected text was available.")
    {
    }
}
