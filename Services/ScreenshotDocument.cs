using System.Collections.Immutable;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinUtil.Models;
using FlowDirection = System.Windows.FlowDirection;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace WinUtil.Services;

internal sealed class ScreenshotDocument
{
    internal const double MinimumStrokeWidth = 1;
    internal const double MaximumStrokeWidth = 100;
    internal const double MinimumFontSize = 8;
    internal const double MaximumFontSize = 200;
    internal const double MinimumBlurRadius = 1;
    internal const double MaximumBlurRadius = 64;
    internal const int MaximumTextLength = 4096;
    internal const int MaximumSourceDimension = 32768;
    internal const long MaximumPixelCount = 64_000_000;

    private const double RenderDpi = 96;
    private const int BytesPerPixel = 4;
    private const int BlurPassCount = 3;
    private const double ArrowHeadStrokeMultiplier = 4;
    private const double MinimumArrowHeadLength = 12;
    private const double ArrowHeadLengthRatio = 0.5;
    private const double ArrowHeadWidthRatio = 0.5;

    private readonly BitmapSource source;
    private ImmutableArray<ScreenshotAnnotation> annotations = [];
    private ImmutableStack<ImmutableArray<ScreenshotAnnotation>> undoHistory = ImmutableStack<ImmutableArray<ScreenshotAnnotation>>.Empty;
    private ImmutableStack<ImmutableArray<ScreenshotAnnotation>> redoHistory = ImmutableStack<ImmutableArray<ScreenshotAnnotation>>.Empty;

    internal ScreenshotDocument(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.PixelWidth < 1 || source.PixelHeight < 1 ||
            source.PixelWidth > MaximumSourceDimension || source.PixelHeight > MaximumSourceDimension ||
            (long)source.PixelWidth * source.PixelHeight > MaximumPixelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(source), "The screenshot dimensions exceed the editor's image limits.");
        }

        Width = source.PixelWidth;
        Height = source.PixelHeight;
        var convertedSource = source.Format == PixelFormats.Pbgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        var stride = checked(Width * BytesPerPixel);
        var pixels = new byte[checked(stride * Height)];
        convertedSource.CopyPixels(pixels, stride, 0);
        this.source = CreateBitmap(pixels);
    }

    internal int Width { get; }
    internal int Height { get; }
    internal bool CanUndo => !undoHistory.IsEmpty;
    internal bool CanRedo => !redoHistory.IsEmpty;
    internal bool HasAnnotations => !annotations.IsEmpty;

    internal void Add(ScreenshotAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var normalizedAnnotation = Normalize(annotation);
        undoHistory = undoHistory.Push(annotations);
        annotations = annotations.Add(normalizedAnnotation);
        redoHistory = ImmutableStack<ImmutableArray<ScreenshotAnnotation>>.Empty;
    }

    internal void Undo()
    {
        if (undoHistory.IsEmpty)
        {
            return;
        }

        redoHistory = redoHistory.Push(annotations);
        annotations = undoHistory.Peek();
        undoHistory = undoHistory.Pop();
    }

    internal void Redo()
    {
        if (redoHistory.IsEmpty)
        {
            return;
        }

        undoHistory = undoHistory.Push(annotations);
        annotations = redoHistory.Peek();
        redoHistory = redoHistory.Pop();
    }

    internal BitmapSource Render()
    {
        var composed = source;
        var pendingAnnotations = new List<ScreenshotAnnotation>();

        foreach (var annotation in annotations)
        {
            if (annotation.Kind == ScreenshotAnnotationKind.Blur)
            {
                composed = DrawAnnotations(composed, pendingAnnotations);
                composed = ApplyBlur(composed, annotation);
                pendingAnnotations.Clear();
            }
            else
            {
                pendingAnnotations.Add(annotation);
            }
        }

        return DrawAnnotations(composed, pendingAnnotations);
    }

    private ScreenshotAnnotation Normalize(ScreenshotAnnotation annotation)
    {
        if (!Enum.IsDefined(annotation.Kind) || !IsFinite(annotation.Start) || !IsFinite(annotation.End))
        {
            throw new ArgumentException("The annotation must use a supported tool and finite coordinates.", nameof(annotation));
        }

        if (!IsInRange(annotation.StrokeWidth, MinimumStrokeWidth, MaximumStrokeWidth) ||
            !IsInRange(annotation.FontSize, MinimumFontSize, MaximumFontSize) ||
            !IsInRange(annotation.BlurRadius, MinimumBlurRadius, MaximumBlurRadius))
        {
            throw new ArgumentOutOfRangeException(nameof(annotation), "The annotation style exceeds the supported limits.");
        }

        if (annotation.Text is null || annotation.Text.Length > MaximumTextLength)
        {
            throw new ArgumentException("The annotation text exceeds the supported length.", nameof(annotation));
        }

        var start = Clip(annotation.Start);
        var end = Clip(annotation.End);

        if (annotation.Kind == ScreenshotAnnotationKind.Text)
        {
            if (string.IsNullOrWhiteSpace(annotation.Text) || start.X >= Width || start.Y >= Height)
            {
                throw new ArgumentException("Text must contain visible characters and start inside the screenshot.", nameof(annotation));
            }
        }
        else if (annotation.Kind == ScreenshotAnnotationKind.Arrow)
        {
            if (start == end)
            {
                throw new ArgumentException("An arrow must have distinct endpoints inside the screenshot.", nameof(annotation));
            }
        }
        else if (start.X == end.X || start.Y == end.Y)
        {
            throw new ArgumentException("The selected region must have a nonzero width and height.", nameof(annotation));
        }

        return annotation with { Start = start, End = end };
    }

    private Point Clip(Point point) => new(Math.Clamp(point.X, 0, Width), Math.Clamp(point.Y, 0, Height));

    private static bool IsFinite(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    private static bool IsInRange(double value, double minimum, double maximum) => double.IsFinite(value) && value >= minimum && value <= maximum;

    private BitmapSource DrawAnnotations(BitmapSource background, IReadOnlyList<ScreenshotAnnotation> pendingAnnotations)
    {
        if (pendingAnnotations.Count == 0)
        {
            return background;
        }

        var visual = new DrawingVisual();
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);

        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(background, new Rect(0, 0, Width, Height));
            drawing.PushClip(new RectangleGeometry(new Rect(0, 0, Width, Height)));

            foreach (var annotation in pendingAnnotations)
            {
                DrawAnnotation(drawing, annotation);
            }

            drawing.Pop();
        }

        var bitmap = new RenderTargetBitmap(Width, Height, RenderDpi, RenderDpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawAnnotation(DrawingContext drawing, ScreenshotAnnotation annotation)
    {
        var brush = new SolidColorBrush(annotation.Color);
        brush.Freeze();
        var pen = new Pen(brush, annotation.StrokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        var bounds = new Rect(annotation.Start, annotation.End);

        switch (annotation.Kind)
        {
            case ScreenshotAnnotationKind.Arrow:
                var direction = annotation.End - annotation.Start;
                var headLength = Math.Min(Math.Max(annotation.StrokeWidth * ArrowHeadStrokeMultiplier, MinimumArrowHeadLength), direction.Length * ArrowHeadLengthRatio);
                direction.Normalize();
                var perpendicular = new Vector(-direction.Y, direction.X);
                var headBase = annotation.End - direction * headLength;
                var head = new StreamGeometry();
                using (var geometry = head.Open())
                {
                    geometry.BeginFigure(annotation.End, true, true);
                    geometry.LineTo(headBase + perpendicular * headLength * ArrowHeadWidthRatio, true, false);
                    geometry.LineTo(headBase - perpendicular * headLength * ArrowHeadWidthRatio, true, false);
                }

                head.Freeze();
                drawing.DrawLine(pen, annotation.Start, headBase);
                drawing.DrawGeometry(brush, pen, head);
                break;
            case ScreenshotAnnotationKind.Rectangle:
                drawing.DrawRectangle(null, pen, bounds);
                break;
            case ScreenshotAnnotationKind.Ellipse:
                drawing.DrawEllipse(null, pen, new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), bounds.Width / 2, bounds.Height / 2);
                break;
            case ScreenshotAnnotationKind.Text:
                var text = new FormattedText(annotation.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), annotation.FontSize, brush, 1);
                drawing.DrawText(text, annotation.Start);
                break;
            default:
                throw new InvalidOperationException("Only drawing annotations can be rendered as vectors.");
        }
    }

    private BitmapSource ApplyBlur(BitmapSource background, ScreenshotAnnotation annotation)
    {
        var bounds = new Rect(annotation.Start, annotation.End);
        var left = (int)Math.Floor(bounds.Left);
        var top = (int)Math.Floor(bounds.Top);
        var right = (int)Math.Ceiling(bounds.Right);
        var bottom = (int)Math.Ceiling(bounds.Bottom);
        var width = right - left;
        var height = bottom - top;
        var regionStride = checked(width * BytesPerPixel);
        var regionPixels = new byte[checked(regionStride * height)];
        background.CopyPixels(new Int32Rect(left, top, width, height), regionPixels, regionStride, 0);
        var buffer = new byte[regionPixels.Length];
        var radius = (int)Math.Ceiling(annotation.BlurRadius);

        // Restrict every pass to the selection so no untouched edge pixels survive as an overlay.
        for (var pass = 0; pass < BlurPassCount; pass++)
        {
            BlurAxis(regionPixels, buffer, width, height, radius, horizontal: true);
            BlurAxis(buffer, regionPixels, width, height, radius, horizontal: false);
        }

        var stride = checked(Width * BytesPerPixel);
        var pixels = new byte[checked(stride * Height)];
        background.CopyPixels(pixels, stride, 0);

        for (var row = 0; row < height; row++)
        {
            Buffer.BlockCopy(regionPixels, row * regionStride, pixels, (top + row) * stride + left * BytesPerPixel, regionStride);
        }

        return CreateBitmap(pixels);
    }

    private static void BlurAxis(byte[] input, byte[] output, int width, int height, int radius, bool horizontal)
    {
        var lineLength = horizontal ? width : height;
        var lineCount = horizontal ? height : width;
        var pixelStep = horizontal ? BytesPerPixel : width * BytesPerPixel;
        var lineStep = horizontal ? width * BytesPerPixel : BytesPerPixel;
        var windowLength = radius * 2 + 1;
        var sums = new int[BytesPerPixel];

        for (var line = 0; line < lineCount; line++)
        {
            Array.Clear(sums);
            var lineStart = line * lineStep;

            for (var offset = -radius; offset <= radius; offset++)
            {
                var index = lineStart + Math.Clamp(offset, 0, lineLength - 1) * pixelStep;

                for (var channel = 0; channel < BytesPerPixel; channel++)
                {
                    sums[channel] += input[index + channel];
                }
            }

            for (var position = 0; position < lineLength; position++)
            {
                var index = lineStart + position * pixelStep;
                var leaving = lineStart + Math.Clamp(position - radius, 0, lineLength - 1) * pixelStep;
                var entering = lineStart + Math.Clamp(position + radius + 1, 0, lineLength - 1) * pixelStep;

                for (var channel = 0; channel < BytesPerPixel; channel++)
                {
                    output[index + channel] = (byte)((sums[channel] + windowLength / 2) / windowLength);
                    sums[channel] += input[entering + channel] - input[leaving + channel];
                }
            }
        }
    }

    private BitmapSource CreateBitmap(byte[] pixels)
    {
        var bitmap = BitmapSource.Create(Width, Height, RenderDpi, RenderDpi, PixelFormats.Pbgra32, null, pixels, checked(Width * BytesPerPixel));
        bitmap.Freeze();
        return bitmap;
    }
}
