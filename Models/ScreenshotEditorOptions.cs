namespace WinUtil.Models;

internal sealed record ScreenshotEditorOptions
{
    internal const double MinimumStrokeWidth = 1;
    internal const double MaximumStrokeWidth = 100;
    internal const double MinimumFontSize = 8;
    internal const double MaximumFontSize = 200;
    internal const double MinimumBlurRadius = 1;
    internal const double MaximumBlurRadius = 64;
    internal const string DefaultColor = "#FFFF5252";
    internal const double DefaultStrokeWidth = 4;
    internal const double DefaultFontSize = 24;
    internal const double DefaultBlurRadius = 12;

    public string Color { get; init; } = DefaultColor;

    public double StrokeWidth { get; init; } = DefaultStrokeWidth;

    public double FontSize { get; init; } = DefaultFontSize;

    public double BlurRadius { get; init; } = DefaultBlurRadius;

    internal ScreenshotEditorOptions Normalize() => this with
    {
        Color = IsHexColor(Color) ? Color.ToUpperInvariant() : DefaultColor,
        StrokeWidth = NormalizeNumber(StrokeWidth, MinimumStrokeWidth, MaximumStrokeWidth, DefaultStrokeWidth),
        FontSize = NormalizeNumber(FontSize, MinimumFontSize, MaximumFontSize, DefaultFontSize),
        BlurRadius = NormalizeNumber(BlurRadius, MinimumBlurRadius, MaximumBlurRadius, DefaultBlurRadius)
    };

    internal ScreenshotEditorOptions MergeChanges(ScreenshotEditorOptions initial, ScreenshotEditorOptions current)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(current);

        return (current with
        {
            Color = Color != initial.Color ? Color : current.Color,
            StrokeWidth = StrokeWidth != initial.StrokeWidth ? StrokeWidth : current.StrokeWidth,
            FontSize = FontSize != initial.FontSize ? FontSize : current.FontSize,
            BlurRadius = BlurRadius != initial.BlurRadius ? BlurRadius : current.BlurRadius
        }).Normalize();
    }

    private static bool IsHexColor(string? color) => color is { Length: 7 or 9 }
        && color[0] == '#'
        && color.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;

    private static double NormalizeNumber(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
