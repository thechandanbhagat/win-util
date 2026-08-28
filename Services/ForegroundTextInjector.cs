using System.ComponentModel;
using System.Runtime.InteropServices;
using Clipboard = System.Windows.Clipboard;
using IDataObject = System.Windows.IDataObject;

namespace WinUtil.Services;

internal sealed class ForegroundTextInjector : IForegroundTextInjector
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventUnicode = 0x0004;
    private const uint KeyEventKeyUp = 0x0002;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyCopy = 0x43;
    private const ushort VirtualKeyPaste = 0x56;
    private const int ClipboardAccessAttemptCount = 5;
    private const int ClipboardCopyAttemptCount = 10;
    private const int MaximumInsertTextLength = 4096;
    private const int MaximumReplaceTextLength = 1_048_576;
    private const int RestoreWindowCommand = 9;
    private static readonly TimeSpan ClipboardPasteDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ClipboardRetryDelay = TimeSpan.FromMilliseconds(50);

    internal static int InputStructureSize => Marshal.SizeOf<Input>();

    public IntPtr CaptureForegroundWindow() => GetForegroundWindow();

    public void InsertText(IntPtr targetWindow, string text)
    {
        ValidateTargetWindow(targetWindow);

        if (string.IsNullOrEmpty(text) || text.Length > MaximumInsertTextLength)
        {
            throw new ArgumentException("The text has an invalid length.", nameof(text));
        }

        RestoreTargetWindow(targetWindow);

        var inputs = new Input[text.Length * 2];

        for (var index = 0; index < text.Length; index++)
        {
            var inputOffset = index * 2;
            inputs[inputOffset] = CreateUnicodeInput(text[index], KeyEventUnicode);
            inputs[inputOffset + 1] = CreateUnicodeInput(text[index], KeyEventUnicode | KeyEventKeyUp);
        }

        SendInputs(inputs, "Windows could not insert the entire generated value.");
    }

    public async Task ReplaceSelectedTextAsync(IntPtr targetWindow, Func<string, string> transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);
        ValidateTargetWindow(targetWindow);
        RestoreTargetWindow(targetWindow);

        var originalClipboard = await GetClipboardDataAsync();

        try
        {
            await AccessClipboardAsync(Clipboard.Clear);
            SendKeyChord(VirtualKeyCopy);

            var selectedText = await ReadSelectedTextAsync();
            var replacementText = transformer(selectedText);

            if (string.IsNullOrEmpty(replacementText) || replacementText.Length > MaximumReplaceTextLength)
            {
                throw new ArgumentException("The transformed text has an invalid length.", nameof(transformer));
            }

            await AccessClipboardAsync(() => Clipboard.SetText(replacementText));
            SendKeyChord(VirtualKeyPaste);
            await Task.Delay(ClipboardPasteDelay);
        }
        finally
        {
            await RestoreClipboardAsync(originalClipboard);
        }
    }

    private static async Task AccessClipboardAsync(Action access)
    {
        await AccessClipboardAsync(() =>
        {
            access();
            return true;
        });
    }

    private static async Task<T> AccessClipboardAsync<T>(Func<T> access)
    {
        for (var attempt = 0; attempt < ClipboardAccessAttemptCount; attempt++)
        {
            try
            {
                return access();
            }
            catch (ExternalException) when (attempt < ClipboardAccessAttemptCount - 1)
            {
                await Task.Delay(ClipboardRetryDelay);
            }
        }

        throw new InvalidOperationException("The clipboard could not be accessed.");
    }

    private static Input CreateVirtualKeyInput(ushort virtualKey, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputData
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = flags
            }
        }
    };

    private static async Task<IDataObject?> GetClipboardDataAsync() =>
        await AccessClipboardAsync(Clipboard.GetDataObject);

    private static async Task<string> ReadSelectedTextAsync()
    {
        for (var attempt = 0; attempt < ClipboardCopyAttemptCount; attempt++)
        {
            await Task.Delay(ClipboardRetryDelay);
            var selectedText = await AccessClipboardAsync(() => Clipboard.ContainsText() ? Clipboard.GetText() : null);

            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                if (selectedText.Length > MaximumReplaceTextLength)
                {
                    throw new ArgumentException("The selected text is too long.", nameof(selectedText));
                }

                return selectedText;
            }
        }

        throw new SelectedTextUnavailableException();
    }

    private static async Task RestoreClipboardAsync(IDataObject? originalClipboard)
    {
        if (originalClipboard is null)
        {
            await AccessClipboardAsync(Clipboard.Clear);
            return;
        }

        await AccessClipboardAsync(() => Clipboard.SetDataObject(originalClipboard, true));
    }

    private static void RestoreTargetWindow(IntPtr targetWindow)
    {
        if (IsIconic(targetWindow))
        {
            ShowWindow(targetWindow, RestoreWindowCommand);
        }

        RestoreForegroundWindow(targetWindow);
    }

    private static void RestoreForegroundWindow(IntPtr targetWindow)
    {
        if (SetForegroundWindow(targetWindow) || GetForegroundWindow() == targetWindow)
        {
            return;
        }

        var currentThreadId = GetCurrentThreadId();
        var targetThreadId = GetWindowThreadProcessId(targetWindow, out _);

        if (targetThreadId == 0 || targetThreadId == currentThreadId)
        {
            throw new InvalidOperationException("Windows did not allow the original active window to regain focus.");
        }

        if (!AttachThreadInput(currentThreadId, targetThreadId, true))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not connect to the original active window.");
        }

        try
        {
            SetFocus(targetWindow);

            if (!SetForegroundWindow(targetWindow) && GetForegroundWindow() != targetWindow)
            {
                throw new InvalidOperationException("Windows did not allow the original active window to regain focus.");
            }
        }
        finally
        {
            if (!AttachThreadInput(currentThreadId, targetThreadId, false))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not disconnect from the original active window.");
            }
        }
    }

    private static Input CreateUnicodeInput(char character, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputData
        {
            Keyboard = new KeyboardInput
            {
                ScanCode = character,
                Flags = flags
            }
        }
    };

    private static void SendInputs(Input[] inputs, string failureMessage)
    {
        var inputCount = (uint)inputs.Length;
        var insertedInputCount = SendInput(inputCount, inputs, Marshal.SizeOf<Input>());

        if (insertedInputCount != inputCount)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), failureMessage);
        }
    }

    private static void SendKeyChord(ushort virtualKey) => SendInputs(
    [
        CreateVirtualKeyInput(VirtualKeyControl, 0),
        CreateVirtualKeyInput(virtualKey, 0),
        CreateVirtualKeyInput(virtualKey, KeyEventKeyUp),
        CreateVirtualKeyInput(VirtualKeyControl, KeyEventKeyUp)
    ], "Windows could not send a keyboard shortcut to the original app.");

    private static void ValidateTargetWindow(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero || !IsWindow(targetWindow))
        {
            throw new ArgumentException("A valid foreground window is required.", nameof(targetWindow));
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AttachThreadInput(uint attachThreadId, uint attachToThreadId, bool attach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        internal uint Type;
        internal InputData Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)]
        internal MouseInput Mouse;

        [FieldOffset(0)]
        internal KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        internal int X;
        internal int Y;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        internal ushort VirtualKey;
        internal ushort ScanCode;
        internal uint Flags;
        internal uint Time;
        internal IntPtr ExtraInfo;
    }
}
