using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clipboard = System.Windows.Clipboard;

namespace WinUtil.Services;

internal sealed class ScreenshotCaptureListener : IDisposable
{
    private const int ClipboardUpdateMessage = 0x031D;
    private const int LowLevelKeyboardHook = 13;
    private const int KeyDownMessage = 0x0100;
    private const int KeyUpMessage = 0x0101;
    private const int SystemKeyDownMessage = 0x0104;
    private const int SystemKeyUpMessage = 0x0105;
    private const int InjectedKeyFlag = 0x0010;
    private const int KeyDownMask = 0x8000;
    private const uint EscapeVirtualKey = 0x1B;
    private const uint CaptureVirtualKey = 0x53;
    private const uint ShiftVirtualKey = 0x10;
    private const uint ControlVirtualKey = 0x11;
    private const uint AltVirtualKey = 0x12;
    private const uint LeftShiftVirtualKey = 0xA0;
    private const uint RightShiftVirtualKey = 0xA1;
    private const uint LeftControlVirtualKey = 0xA2;
    private const uint RightControlVirtualKey = 0xA3;
    private const uint LeftAltVirtualKey = 0xA4;
    private const uint RightAltVirtualKey = 0xA5;
    private const uint LeftWindowsVirtualKey = 0x5B;
    private const uint RightWindowsVirtualKey = 0x5C;
    private const int ClipboardAccessAttemptCount = 5;
    private const int MaximumImageDimension = 16_384;
    private const long MaximumImagePixels = 64_000_000;
    private const int MaximumPngBytes = 128 * 1024 * 1024;
    private const int BitsPerByte = 8;
    private const string PngClipboardFormat = "PNG";
    private static readonly TimeSpan ClipboardRetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly uint[] SpecificModifierKeys =
    [
        LeftShiftVirtualKey, RightShiftVirtualKey,
        LeftControlVirtualKey, RightControlVirtualKey,
        LeftAltVirtualKey, RightAltVirtualKey,
        LeftWindowsVirtualKey, RightWindowsVirtualKey
    ];

    private readonly IntPtr windowHandle;
    private readonly Func<bool> isEnabled;
    private readonly Action<BitmapSource> openEditor;
    private readonly Action<Exception> reportError;
    private readonly HwndSource windowSource;
    private readonly Dispatcher dispatcher;
    private readonly HookProcedure hookProcedure;
    private readonly ScreenshotCaptureSession session = new();
    private readonly HashSet<uint> pressedModifierKeys = [];
    private readonly CancellationTokenSource disposalCancellation = new();
    private IntPtr hookHandle;
    private bool clipboardListenerAdded;
    private bool captureKeyIsDown;
    private bool clipboardReadInProgress;
    private bool disposed;

    internal ScreenshotCaptureListener(
        IntPtr widgetWindowHandle,
        Func<bool> isEnabled,
        Action<BitmapSource> openEditor,
        Action<Exception> reportError)
    {
        ArgumentNullException.ThrowIfNull(isEnabled);
        ArgumentNullException.ThrowIfNull(openEditor);
        ArgumentNullException.ThrowIfNull(reportError);
        windowSource = HwndSource.FromHwnd(widgetWindowHandle)
            ?? throw new ArgumentException("A WPF window handle is required to listen for screenshots.", nameof(widgetWindowHandle));
        dispatcher = windowSource.Dispatcher;
        dispatcher.VerifyAccess();
        windowHandle = widgetWindowHandle;
        this.isEnabled = isEnabled;
        this.openEditor = openEditor;
        this.reportError = reportError;
        hookProcedure = HandleKeyboardEvent;

        foreach (var key in SpecificModifierKeys)
        {
            if ((GetAsyncKeyState((int)key) & KeyDownMask) != 0)
            {
                pressedModifierKeys.Add(key);
            }
        }

        windowSource.AddHook(HandleWindowMessage);

        try
        {
            if (!AddClipboardFormatListener(windowHandle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not listen for screenshot clipboard updates.");
            }

            clipboardListenerAdded = true;
            hookHandle = SetWindowsHookEx(LowLevelKeyboardHook, hookProcedure, GetModuleHandle(null), 0);

            if (hookHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not listen for the screenshot shortcut.");
            }
        }
        catch (Exception exception)
        {
            try
            {
                Dispose();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException("Screenshot listener setup and cleanup failed.", exception, cleanupException);
            }

            throw;
        }
    }

    public void Dispose()
    {
        dispatcher.VerifyAccess();

        if (disposed)
        {
            return;
        }

        disposed = true;
        session.Cancel();
        disposalCancellation.Cancel();
        disposalCancellation.Dispose();
        var failures = new List<Exception>();

        if (hookHandle != IntPtr.Zero && !UnhookWindowsHookEx(hookHandle))
        {
            failures.Add(new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not stop listening for the screenshot shortcut."));
        }

        // Destroying the source window already removes its clipboard registration.
        if (clipboardListenerAdded && !windowSource.IsDisposed && IsWindow(windowHandle) && !RemoveClipboardFormatListener(windowHandle))
        {
            failures.Add(new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not stop listening for screenshot clipboard updates."));
        }

        if (!windowSource.IsDisposed)
        {
            windowSource.RemoveHook(HandleWindowMessage);
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("Screenshot listener cleanup failed.", failures);
        }
    }

    internal void CancelPendingCapture()
    {
        dispatcher.VerifyAccess();
        session.Cancel();
    }

    private IntPtr HandleKeyboardEvent(int code, IntPtr windowParameter, IntPtr keyboardData)
    {
        if (code >= 0 && !disposed)
        {
            try
            {
                var message = windowParameter.ToInt32();

                if (message is KeyDownMessage or KeyUpMessage or SystemKeyDownMessage or SystemKeyUpMessage)
                {
                    var input = Marshal.PtrToStructure<LowLevelKeyboardInput>(keyboardData);

                    if ((input.Flags & InjectedKeyFlag) == 0)
                    {
                        ProcessPhysicalKey(input.VirtualKey, message is KeyDownMessage or SystemKeyDownMessage);
                    }
                }
            }
            catch (Exception exception)
            {
                session.Cancel();
                dispatcher.BeginInvoke(new Action(() => reportError(exception)));
            }
        }

        return CallNextHookEx(hookHandle, code, windowParameter, keyboardData);
    }

    private void ProcessPhysicalKey(uint key, bool isDown)
    {
        if (SpecificModifierKeys.Contains(key) || key is ShiftVirtualKey or ControlVirtualKey or AltVirtualKey)
        {
            if (isDown)
            {
                pressedModifierKeys.Add(key);
            }
            else
            {
                pressedModifierKeys.Remove(key);
            }

            return;
        }

        if (key == EscapeVirtualKey && isDown)
        {
            session.Cancel();
        }

        if (key != CaptureVirtualKey)
        {
            return;
        }

        var isFirstKeyDown = isDown && !captureKeyIsDown;
        captureKeyIsDown = isDown;

        if (isFirstKeyDown
            && (pressedModifierKeys.Contains(LeftWindowsVirtualKey) || pressedModifierKeys.Contains(RightWindowsVirtualKey))
            && IsModifierDown(ShiftVirtualKey, LeftShiftVirtualKey, RightShiftVirtualKey)
            && !IsModifierDown(ControlVirtualKey, LeftControlVirtualKey, RightControlVirtualKey)
            && !IsModifierDown(AltVirtualKey, LeftAltVirtualKey, RightAltVirtualKey))
        {
            // Arming inside the hook preserves the clipboard baseline before Windows handles the shortcut.
            session.Arm(isEnabled(), GetClipboardSequenceNumber(), DateTimeOffset.UtcNow);
        }
    }

    private bool IsModifierDown(uint genericKey, uint leftKey, uint rightKey) =>
        pressedModifierKeys.Contains(genericKey) || pressedModifierKeys.Contains(leftKey) || pressedModifierKeys.Contains(rightKey);

    private IntPtr HandleWindowMessage(IntPtr handle, int message, IntPtr windowParameter, IntPtr longParameter, ref bool handled)
    {
        if (message != ClipboardUpdateMessage || disposed || clipboardReadInProgress)
        {
            return IntPtr.Zero;
        }

        try
        {
            if (!session.IsPending(isEnabled(), DateTimeOffset.UtcNow))
            {
                return IntPtr.Zero;
            }

            var sequenceNumber = GetClipboardSequenceNumber();

            if (session.TryBeginRead(isEnabled(), sequenceNumber, GetClipboardSource(), DateTimeOffset.UtcNow, out var candidate))
            {
                ReadScreenshotAsync(candidate);
            }
        }
        catch (Exception exception)
        {
            session.Cancel();
            reportError(exception);
        }

        return IntPtr.Zero;
    }

    private async void ReadScreenshotAsync(ScreenshotCaptureCandidate candidate)
    {
        var cancellationToken = disposalCancellation.Token;

        try
        {
            for (var attempt = 0; attempt < ClipboardAccessAttemptCount; attempt++)
            {
                if (disposed || !session.IsCurrent(candidate, isEnabled(), DateTimeOffset.UtcNow)
                    || GetClipboardSequenceNumber() != candidate.SequenceNumber)
                {
                    return;
                }

                var clipboardOwner = GetClipboardOwner();

                if (GetClipboardSource(clipboardOwner) != ScreenshotClipboardSource.ScreenshotTool)
                {
                    session.Cancel();
                    return;
                }

                try
                {
                    BitmapSource? image;
                    clipboardReadInProgress = true;

                    try
                    {
                        image = ReadDetachedClipboardImage();
                    }
                    finally
                    {
                        clipboardReadInProgress = false;
                    }

                    if (GetClipboardOwner() != clipboardOwner)
                    {
                        session.Cancel();
                        return;
                    }

                    // Reading a delayed format can advance the sequence while the same snipping owner retains the clipboard.
                    var renderedSequenceNumber = GetClipboardSequenceNumber();

                    if (session.TryCompleteRead(candidate, image is not null, isEnabled(), DateTimeOffset.UtcNow, renderedSequenceNumber) && image is not null)
                    {
                        openEditor(image);
                    }

                    return;
                }
                catch (ExternalException) when (attempt < ClipboardAccessAttemptCount - 1)
                {
                    await Task.Delay(ClipboardRetryDelay, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Disposing the listener cancels clipboard retries during application shutdown.
        }
        catch (Exception exception)
        {
            if (session.IsCurrent(candidate, isEnabled(), DateTimeOffset.UtcNow))
            {
                session.Cancel();
            }

            reportError(exception);
        }
    }

    private static ScreenshotClipboardSource GetClipboardSource() => GetClipboardSource(GetClipboardOwner());

    private static ScreenshotClipboardSource GetClipboardSource(IntPtr owner)
    {
        if (owner == IntPtr.Zero)
        {
            return ScreenshotClipboardSource.OtherApplication;
        }

        GetWindowThreadProcessId(owner, out var processId);

        if (processId == Environment.ProcessId)
        {
            return ScreenshotClipboardSource.ThisApplication;
        }

        if (processId == 0)
        {
            return ScreenshotClipboardSource.OtherApplication;
        }

        using var process = Process.GetProcessById(checked((int)processId));
        var name = process.ProcessName;
        return name.Equals("SnippingTool", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ScreenClippingHost", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ScreenSketch", StringComparison.OrdinalIgnoreCase)
                ? ScreenshotClipboardSource.ScreenshotTool
                : ScreenshotClipboardSource.OtherApplication;
    }

    private static BitmapSource? ReadDetachedClipboardImage()
    {
        var image = Clipboard.GetImage();

        if (image is not null)
        {
            return DetachImage(image);
        }

        if (!Clipboard.ContainsData(PngClipboardFormat))
        {
            return null;
        }

        using var pngStream = Clipboard.GetData(PngClipboardFormat) switch
        {
            Stream stream => stream,
            byte[] bytes => new MemoryStream(bytes, false),
            null => null,
            _ => throw new InvalidDataException("The screenshot clipboard PNG data has an unsupported format.")
        };

        if (pngStream is null)
        {
            return null;
        }

        if (!pngStream.CanSeek || pngStream.Length <= 0 || pngStream.Length > MaximumPngBytes)
        {
            throw new InvalidDataException("The screenshot clipboard PNG data is empty or exceeds the supported size.");
        }

        pngStream.Position = 0;
        var decoder = new PngBitmapDecoder(pngStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
        return DetachImage(decoder.Frames[0]);
    }

    private static BitmapSource DetachImage(BitmapSource image)
    {
        var width = image.PixelWidth;
        var height = image.PixelHeight;

        if (width <= 0 || height <= 0 || width > MaximumImageDimension || height > MaximumImageDimension
            || (long)width * height > MaximumImagePixels)
        {
            throw new InvalidDataException("The screenshot exceeds the supported image dimensions or pixel count.");
        }

        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var stride = checked(width * PixelFormats.Bgra32.BitsPerPixel / BitsPerByte);
        var pixels = new byte[checked(stride * height)];
        converted.CopyPixels(pixels, stride, 0);
        var detached = BitmapSource.Create(width, height, image.DpiX, image.DpiY, PixelFormats.Bgra32, null, pixels, stride);
        detached.Freeze();
        return detached;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardOwner();

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr windowParameter, IntPtr keyboardData);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookIdentifier, HookProcedure procedure, IntPtr moduleHandle, uint threadIdentifier);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    private delegate IntPtr HookProcedure(int code, IntPtr windowParameter, IntPtr keyboardData);

    [StructLayout(LayoutKind.Sequential)]
    private struct LowLevelKeyboardInput
    {
        internal uint VirtualKey;
        internal uint ScanCode;
        internal int Flags;
        internal uint Time;
        internal IntPtr ExtraInfo;
    }
}
