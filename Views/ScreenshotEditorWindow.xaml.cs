using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinUtil.Models;
using WinUtil.Services;
using Brushes = System.Windows.Media.Brushes;
using Canvas = System.Windows.Controls.Canvas;
using Clipboard = System.Windows.Clipboard;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using Border = System.Windows.Controls.Border;
using Cursors = System.Windows.Input.Cursors;
using Ellipse = System.Windows.Shapes.Ellipse;
using Path = System.IO.Path;
using Point = System.Windows.Point;
using RadioButton = System.Windows.Controls.RadioButton;
using Rectangle = System.Windows.Shapes.Rectangle;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Shape = System.Windows.Shapes.Shape;
using ScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace WinUtil.Views;

public partial class ScreenshotEditorWindow : Window
{
    private const int ClipboardAttempts = 5;
    private const int ClipboardRetryDelayMilliseconds = 80;
    private const double MinimumDragDistance = 1;
    private const double ArrowHeadStrokeMultiplier = 4;
    private const double MinimumArrowHeadLength = 12;
    private const double ArrowHeadLengthRatio = 0.5;
    private const double ArrowHeadWidthRatio = 0.5;
    private const double PreviewOpacity = 0.8;
    private const double LastPixelOffset = 1;
    private const double TextEditorMaximumWidth = 360;
    private const double TextEditorMaximumHeight = 240;
    private const double TextEditorPadding = 8;
    private const double TextEditorInset = TextEditorPadding / 2;
    private const double ColorSwatchSize = 12;
    private const double ColorSwatchSpacing = 8;
    private const double ReadableOnDarkThreshold = 0.55;
    private const double RedBrightnessWeight = 0.299;
    private const double GreenBrightnessWeight = 0.587;
    private const double BlueBrightnessWeight = 0.114;
    private const string DefaultInstruction = "Drag on the screenshot to add an arrow.";

    private readonly ScreenshotDocument document;
    private ScreenshotAnnotationKind selectedTool = ScreenshotAnnotationKind.Arrow;
    private Point? dragStart;
    private Point? textAnchor;
    private bool isInitializing = true;
    private bool isCopying;
    private bool isClosed;

    internal ScreenshotEditorWindow(BitmapSource image, ScreenshotEditorOptions? options = null)
    {
        document = new ScreenshotDocument(image);
        InitialOptions = (options ?? new ScreenshotEditorOptions()).Normalize();
        Options = InitialOptions;
        InitializeComponent();
        AnnotationTextBox.MaxLength = ScreenshotDocument.MaximumTextLength;
        PopulateOptions();
        ImageSurface.Width = document.Width;
        ImageSurface.Height = document.Height;
        AnnotationCanvas.Width = document.Width;
        AnnotationCanvas.Height = document.Height;
        ImageDetailsText.Text = $"{document.Width:N0} × {document.Height:N0} px";
        isInitializing = false;
        RefreshImage();
        SetStatus(DefaultInstruction);
        Closed += (_, _) => isClosed = true;
    }

    internal ScreenshotEditorOptions Options { get; private set; }

    internal ScreenshotEditorOptions InitialOptions { get; }

    internal BitmapSource RenderedImage => (BitmapSource)ScreenshotImage.Source;

    internal void SavePng(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FinishTextEntry(commit: true);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(RenderedImage));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        File.WriteAllBytes(filePath, stream.ToArray());
    }

    internal void CommitAnnotation(Point start, Point end)
    {
        var first = selectedTool == ScreenshotAnnotationKind.Text
            ? new Point(Math.Clamp(start.X, 0, document.Width - LastPixelOffset), Math.Clamp(start.Y, 0, document.Height - LastPixelOffset))
            : ClampPoint(start);
        var last = ClampPoint(end);

        if (selectedTool == ScreenshotAnnotationKind.Text && string.IsNullOrWhiteSpace(AnnotationTextBox.Text))
        {
            SetStatus("Enter text on the screenshot, then press Ctrl+Enter to place it.", isError: true);
            AnnotationTextBox.Focus();
            return;
        }

        if (selectedTool != ScreenshotAnnotationKind.Text && (last - first).Length < MinimumDragDistance)
        {
            return;
        }

        if (selectedTool is ScreenshotAnnotationKind.Rectangle or ScreenshotAnnotationKind.Ellipse or ScreenshotAnnotationKind.Blur
            && (Math.Abs(last.X - first.X) < MinimumDragDistance || Math.Abs(last.Y - first.Y) < MinimumDragDistance))
        {
            return;
        }

        document.Add(new ScreenshotAnnotation(
            selectedTool,
            first,
            last,
            (Color)ColorConverter.ConvertFromString(Options.Color),
            Options.StrokeWidth,
            AnnotationTextBox.Text,
            Options.FontSize,
            Options.BlurRadius));
        RefreshImage();
        SetStatus(selectedTool == ScreenshotAnnotationKind.Blur
            ? "Area blurred. Copy or save to export the edited image."
            : "Annotation added. Ctrl+Z to undo.");
    }

    internal void BeginTextEntry(Point point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(point), "Text coordinates must be finite.");
        }

        TextTool.IsChecked = true;
        FinishTextEntry(commit: true);
        var anchor = new Point(
            Math.Clamp(point.X, 0, document.Width - LastPixelOffset),
            Math.Clamp(point.Y, 0, document.Height - LastPixelOffset));
        textAnchor = anchor;
        AnnotationTextBox.Text = string.Empty;
        AnnotationTextBox.Width = Math.Max(LastPixelOffset, Math.Min(TextEditorMaximumWidth, document.Width - anchor.X) - TextEditorPadding);
        AnnotationTextBox.MaxHeight = Math.Max(LastPixelOffset, Math.Min(TextEditorMaximumHeight, document.Height - anchor.Y) - TextEditorPadding);
        UpdateTextEntryStyle();
        Canvas.SetLeft(TextInputPanel, anchor.X - TextEditorInset);
        Canvas.SetTop(TextInputPanel, anchor.Y - TextEditorInset);
        TextInputPanel.Visibility = Visibility.Visible;
        AnnotationTextBox.Focus();
        SetStatus("Type here. Ctrl+Enter to place text; Esc to cancel.");
    }

    internal void FinishTextEntry(bool commit)
    {
        if (textAnchor is not Point anchor)
        {
            return;
        }

        textAnchor = null;
        TextInputPanel.Visibility = Visibility.Collapsed;

        if (commit && !string.IsNullOrWhiteSpace(AnnotationTextBox.Text))
        {
            CommitAnnotation(anchor, anchor);
        }

        AnnotationTextBox.Text = string.Empty;
    }

    private void UpdateTextEntryStyle()
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Options.Color));
        AnnotationTextBox.Foreground = brush;
        AnnotationTextBox.CaretBrush = brush;
        AnnotationTextBox.FontSize = Options.FontSize;
        var brightness = (brush.Color.R * RedBrightnessWeight + brush.Color.G * GreenBrightnessWeight + brush.Color.B * BlueBrightnessWeight) / byte.MaxValue;
        TextInputPanel.Background = brightness >= ReadableOnDarkThreshold
            ? (System.Windows.Media.Brush)FindResource("SurfaceBrush")
            : Brushes.White;
    }

    private void PopulateOptions()
    {
        foreach (var (name, color) in new[]
        {
            ("Red", ScreenshotEditorOptions.DefaultColor),
            ("Blue", "#FF52A8FF"),
            ("Yellow", "#FFFFD952"),
            ("Green", "#FF71E5A0"),
            ("White", "#FFFFFFFF"),
            ("Black", "#FF111827")
        })
        {
            ColorComboBox.Items.Add(CreateColorOption(name, color));
        }

        if (!ColorComboBox.Items.Cast<ComboBoxItem>().Any(item => Equals(item.Tag, Options.Color)))
        {
            ColorComboBox.Items.Add(CreateColorOption("Custom", Options.Color));
        }

        ColorComboBox.SelectedValue = Options.Color;
        PopulateNumericOptions(StrokeComboBox, new double[] { 2, 4, 6, 8, 12, 24, 48, 100 }, Options.StrokeWidth);
        PopulateNumericOptions(FontSizeComboBox, new double[] { 8, 16, 24, 32, 48, 64, 100, 200 }, Options.FontSize);
        PopulateNumericOptions(BlurComboBox, new double[] { 4, 8, 12, 20, 32, 48, 64 }, Options.BlurRadius);
    }

    private static ComboBoxItem CreateColorOption(string name, string color)
    {
        var content = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        content.Children.Add(new Border
        {
            Width = ColorSwatchSize,
            Height = ColorSwatchSize,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 0, ColorSwatchSpacing, 0)
        });
        content.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
        var item = new ComboBoxItem { Content = content, Tag = color };
        System.Windows.Automation.AutomationProperties.SetName(item, name);
        return item;
    }

    private static void PopulateNumericOptions(ComboBox comboBox, IEnumerable<double> values, double selected)
    {
        foreach (var value in values.Append(selected).Distinct().Order())
        {
            comboBox.Items.Add(new ComboBoxItem { Content = $"{value:0.##} px", Tag = value });
        }

        comboBox.SelectedValue = selected;
    }

    private void HandleOptionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (isInitializing)
        {
            return;
        }

        Options = new ScreenshotEditorOptions
        {
            Color = (string)ColorComboBox.SelectedValue,
            StrokeWidth = (double)StrokeComboBox.SelectedValue,
            FontSize = (double)FontSizeComboBox.SelectedValue,
            BlurRadius = (double)BlurComboBox.SelectedValue
        }.Normalize();
        UpdateTextEntryStyle();
    }

    private void HandleToolChecked(object sender, RoutedEventArgs e)
    {
        if (isInitializing || sender is not RadioButton { Tag: string toolName })
        {
            return;
        }

        FinishTextEntry(commit: true);
        CancelDrag();
        selectedTool = Enum.Parse<ScreenshotAnnotationKind>(toolName);
        ColorOptionPanel.Visibility = selectedTool == ScreenshotAnnotationKind.Blur ? Visibility.Collapsed : Visibility.Visible;
        StrokeOptionPanel.Visibility = selectedTool is ScreenshotAnnotationKind.Text or ScreenshotAnnotationKind.Blur ? Visibility.Collapsed : Visibility.Visible;
        FontSizeOptionPanel.Visibility = selectedTool == ScreenshotAnnotationKind.Text ? Visibility.Visible : Visibility.Collapsed;
        BlurOptionPanel.Visibility = selectedTool == ScreenshotAnnotationKind.Blur ? Visibility.Visible : Visibility.Collapsed;
        ActiveToolText.Text = selectedTool == ScreenshotAnnotationKind.Blur ? "Blur area" : toolName;
        AnnotationCanvas.Cursor = selectedTool == ScreenshotAnnotationKind.Text ? Cursors.IBeam : Cursors.Cross;
        SetStatus(selectedTool switch
        {
            ScreenshotAnnotationKind.Text => "Click the screenshot to type text. Ctrl+Enter to place it.",
            ScreenshotAnnotationKind.Blur => "Drag over the area to blur. The change is applied to copied and saved images.",
            ScreenshotAnnotationKind.Rectangle => "Drag on the screenshot to add a rectangle.",
            ScreenshotAnnotationKind.Ellipse => "Drag on the screenshot to add an ellipse.",
            _ => DefaultInstruction
        });

        AnnotationCanvas.Focus();
    }

    private void HandleCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        AnnotationCanvas.Focus();

        if (selectedTool == ScreenshotAnnotationKind.Text)
        {
            BeginTextEntry(e.GetPosition(AnnotationCanvas));
        }
        else
        {
            dragStart = ClampPoint(e.GetPosition(AnnotationCanvas));
            AnnotationCanvas.CaptureMouse();
        }

        e.Handled = true;
    }

    private void HandleTextEntryKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            FinishTextEntry(commit: false);
            SetStatus("Text cancelled. Click the screenshot to type again.");
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            FinishTextEntry(commit: true);
        }
        else
        {
            return;
        }

        AnnotationCanvas.Focus();
        e.Handled = true;
    }

    private void HandleWorkspaceMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (textAnchor is null || TextInputPanel.IsMouseOver)
        {
            return;
        }

        FinishTextEntry(commit: true);
        AnnotationCanvas.Focus();
        e.Handled = true;
    }

    private void HandleZoomChanged(object sender, RoutedEventArgs e)
    {
        if (isInitializing || sender is not RadioButton { Tag: string zoom })
        {
            return;
        }

        CancelDrag();
        var actualSize = zoom == "Actual";
        ImageViewbox.Stretch = actualSize ? Stretch.None : Stretch.Uniform;
        ImageScrollViewer.HorizontalScrollBarVisibility = actualSize ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        ImageScrollViewer.VerticalScrollBarVisibility = actualSize ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        ImageScrollViewer.ScrollToHome();
    }

    private void HandleCanvasMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (dragStart is not Point start)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            CancelDrag();
            return;
        }

        DrawPreview(start, ClampPoint(e.GetPosition(AnnotationCanvas)));
    }

    private void HandleCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (dragStart is not Point start)
        {
            return;
        }

        var end = ClampPoint(e.GetPosition(AnnotationCanvas));
        CancelDrag();
        CommitAnnotation(start, end);
        e.Handled = true;
    }

    private void HandleLostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e) => CancelDrag();

    private void CancelDrag()
    {
        dragStart = null;
        AnnotationCanvas.Children.Clear();

        if (AnnotationCanvas.IsMouseCaptured)
        {
            AnnotationCanvas.ReleaseMouseCapture();
        }
    }

    private void DrawPreview(Point start, Point end)
    {
        AnnotationCanvas.Children.Clear();
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Options.Color));
        Shape preview;

        if (selectedTool == ScreenshotAnnotationKind.Arrow)
        {
            var direction = end - start;

            if (direction.Length < MinimumDragDistance)
            {
                return;
            }

            var headLength = Math.Min(Math.Max(Options.StrokeWidth * ArrowHeadStrokeMultiplier, MinimumArrowHeadLength), direction.Length * ArrowHeadLengthRatio);
            direction.Normalize();
            var headBase = end - direction * headLength;
            var perpendicular = new Vector(-direction.Y, direction.X) * headLength * ArrowHeadWidthRatio;
            var geometry = new StreamGeometry();

            using (var context = geometry.Open())
            {
                context.BeginFigure(start, false, false);
                context.LineTo(end, true, false);
                context.BeginFigure(headBase + perpendicular, false, false);
                context.LineTo(end, true, false);
                context.LineTo(headBase - perpendicular, true, false);
            }

            preview = new System.Windows.Shapes.Path { Data = geometry };
        }
        else
        {
            preview = selectedTool == ScreenshotAnnotationKind.Ellipse ? new Ellipse() : new Rectangle();
            preview.Width = Math.Abs(end.X - start.X);
            preview.Height = Math.Abs(end.Y - start.Y);
            Canvas.SetLeft(preview, Math.Min(start.X, end.X));
            Canvas.SetTop(preview, Math.Min(start.Y, end.Y));

            if (selectedTool == ScreenshotAnnotationKind.Blur)
            {
                preview.Fill = (System.Windows.Media.Brush)FindResource("AccentMutedBrush");
                preview.StrokeDashArray = new DoubleCollection { 4, 4 };
                brush = (SolidColorBrush)FindResource("AccentBrush");
            }
        }

        preview.Stroke = brush;
        preview.StrokeThickness = Options.StrokeWidth;
        preview.Opacity = PreviewOpacity;
        preview.IsHitTestVisible = false;
        AnnotationCanvas.Children.Add(preview);
    }

    private Point ClampPoint(Point point) => new(
        Math.Clamp(point.X, 0, document.Width),
        Math.Clamp(point.Y, 0, document.Height));

    private void RefreshImage()
    {
        ScreenshotImage.Source = document.Render();
        UndoButton.IsEnabled = document.CanUndo;
        RedoButton.IsEnabled = document.CanRedo;
    }

    private void HandleUndo(object sender, RoutedEventArgs e)
    {
        FinishTextEntry(commit: true);
        CancelDrag();
        document.Undo();
        RefreshImage();
        SetStatus("Annotation undone.");
    }

    private void HandleRedo(object sender, RoutedEventArgs e)
    {
        FinishTextEntry(commit: true);
        CancelDrag();
        document.Redo();
        RefreshImage();
        SetStatus("Annotation restored.");
    }

    private async void HandleCopy(object sender, RoutedEventArgs e) => await CopyImageAsync();

    private async Task CopyImageAsync()
    {
        if (isCopying)
        {
            return;
        }

        FinishTextEntry(commit: true);
        CancelDrag();
        isCopying = true;
        CopyButton.IsEnabled = false;

        try
        {
            var image = RenderedImage;

            for (var attempt = 0; attempt < ClipboardAttempts; attempt++)
            {
                if (isClosed)
                {
                    return;
                }

                try
                {
                    Clipboard.SetImage(image);
                    SetStatus("Edited image copied to the clipboard.");
                    return;
                }
                catch (ExternalException) when (attempt < ClipboardAttempts - 1)
                {
                    await Task.Delay(ClipboardRetryDelayMilliseconds);
                }
            }
        }
        catch (ExternalException exception)
        {
            SetStatus($"Could not copy the image. Try again after closing other clipboard tools. {exception.Message}", isError: true);
        }
        finally
        {
            isCopying = false;
            CopyButton.IsEnabled = true;
        }
    }

    private void HandleSave(object sender, RoutedEventArgs e)
    {
        FinishTextEntry(commit: true);
        CancelDrag();
        var dialog = new SaveFileDialog
        {
            Title = "Save edited screenshot",
            Filter = "PNG image (*.png)|*.png",
            DefaultExt = ".png",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = $"Screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            SavePng(dialog.FileName);
            SetStatus($"Saved {Path.GetFileName(dialog.FileName)}.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ExternalException or NotSupportedException)
        {
            SetStatus($"Could not save the image. Choose another location and try again. {exception.Message}", isError: true);
        }
    }

    private async void HandlePreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && textAnchor is not null)
        {
            FinishTextEntry(commit: false);
            AnnotationCanvas.Focus();
            SetStatus("Text cancelled. Click the screenshot to type again.");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && dragStart is not null)
        {
            CancelDrag();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && Keyboard.FocusedElement is not TextBoxBase and not ComboBox)
        {
            var tool = e.Key switch
            {
                Key.A => ArrowTool,
                Key.R => RectangleTool,
                Key.E => EllipseTool,
                Key.T => TextTool,
                Key.B => BlurTool,
                _ => null
            };

            if (tool is not null)
            {
                tool.IsChecked = true;
                e.Handled = true;
            }

            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control || Keyboard.FocusedElement is TextBoxBase && e.Key != Key.S)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Z when document.CanUndo:
                HandleUndo(this, e);
                break;
            case Key.Y when document.CanRedo:
                HandleRedo(this, e);
                break;
            case Key.C:
                e.Handled = true;
                await CopyImageAsync();
                break;
            case Key.S:
                HandleSave(this, e);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void SetStatus(string text, bool isError = false)
    {
        StatusText.Text = text;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(isError ? "ErrorBrush" : "TextSecondaryBrush");
    }
}
