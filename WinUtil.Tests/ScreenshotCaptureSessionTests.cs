using System.Windows.Interop;
using WinUtil.Services;

namespace WinUtil.Tests;

internal static class ScreenshotCaptureSessionTests
{
    private const uint InitialSequence = 20;
    private const uint CaptureSequence = 21;
    private const uint LaterSequence = 22;
    private static readonly IntPtr MessageOnlyWindowHandle = new(-3);
    private static readonly DateTimeOffset StartTime = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    internal static void DisabledCaptureDoesNotArmOrComplete()
    {
        var session = new ScreenshotCaptureSession();
        session.Arm(false, InitialSequence, StartTime);
        TestAssert.False(TryCapture(session, CaptureSequence, out _));

        session.Arm(true, InitialSequence, StartTime);
        TestAssert.True(TryCapture(session, CaptureSequence, out var candidate));
        TestAssert.False(session.TryCompleteRead(candidate, true, false, StartTime));
        TestAssert.False(session.IsPending(true, StartTime));
    }

    internal static void UnarmedAndExistingClipboardImagesAreIgnored()
    {
        var session = new ScreenshotCaptureSession();
        TestAssert.False(TryCapture(session, CaptureSequence, out _));
        session.Arm(true, InitialSequence, StartTime);
        TestAssert.False(TryCapture(session, InitialSequence, out _));
        TestAssert.True(session.IsPending(true, StartTime));
        TestAssert.True(TryCapture(session, CaptureSequence, out _));
    }

    internal static void UnrelatedAndOwnClipboardUpdatesCancelCapture()
    {
        foreach (var source in new[] { ScreenshotClipboardSource.OtherApplication, ScreenshotClipboardSource.ThisApplication })
        {
            var session = new ScreenshotCaptureSession();
            session.Arm(true, InitialSequence, StartTime);
            TestAssert.False(session.TryBeginRead(true, CaptureSequence, source, StartTime, out _));
            TestAssert.False(TryCapture(session, LaterSequence, out _));
        }
    }

    internal static void CaptureExpiresAndCanBeCancelledBeforeOrDuringRead()
    {
        var session = new ScreenshotCaptureSession();
        session.Arm(true, InitialSequence, StartTime);
        TestAssert.True(session.IsPending(true, StartTime + ScreenshotCaptureSession.CaptureTimeout - TimeSpan.FromMilliseconds(1)));
        TestAssert.False(session.TryBeginRead(true, CaptureSequence, ScreenshotClipboardSource.ScreenshotTool,
            StartTime + ScreenshotCaptureSession.CaptureTimeout, out _));

        session.Arm(true, InitialSequence, StartTime);
        session.Cancel();
        TestAssert.False(TryCapture(session, CaptureSequence, out _));

        session.Arm(true, InitialSequence, StartTime);
        TestAssert.True(TryCapture(session, CaptureSequence, out var candidate));
        session.Cancel();
        TestAssert.False(session.TryCompleteRead(candidate, true, true, StartTime));

        session.Arm(true, InitialSequence, StartTime);
        TestAssert.True(TryCapture(session, CaptureSequence, out candidate));
        TestAssert.False(session.TryCompleteRead(candidate, true, true, StartTime + ScreenshotCaptureSession.CaptureTimeout));
    }

    internal static void CapturesAreConsumedOnceAndDuplicateNotificationsAreIgnored()
    {
        var session = new ScreenshotCaptureSession();
        session.Arm(true, InitialSequence, StartTime);
        TestAssert.True(TryCapture(session, CaptureSequence, out var candidate));
        TestAssert.False(TryCapture(session, CaptureSequence, out _));
        TestAssert.True(session.TryCompleteRead(candidate, true, true, StartTime));
        TestAssert.False(session.TryCompleteRead(candidate, true, true, StartTime));
        TestAssert.False(TryCapture(session, LaterSequence, out _));

        session.Arm(true, InitialSequence, StartTime);
        TestAssert.False(TryCapture(session, CaptureSequence, out _));
        TestAssert.True(TryCapture(session, LaterSequence, out _));
    }

    internal static void RearmingAndNewerUpdatesInvalidatePendingReads()
    {
        var session = new ScreenshotCaptureSession();
        session.Arm(true, InitialSequence, StartTime);
        TestAssert.True(TryCapture(session, CaptureSequence, out var originalCandidate));
        session.Arm(true, CaptureSequence, StartTime);
        TestAssert.False(session.IsCurrent(originalCandidate, true, StartTime));
        TestAssert.True(TryCapture(session, LaterSequence, out var nextCandidate));
        TestAssert.False(session.TryCompleteRead(originalCandidate, true, true, StartTime));
        TestAssert.True(session.TryCompleteRead(nextCandidate, true, true, StartTime));

        session.Arm(true, InitialSequence, StartTime);
        TestAssert.True(TryCapture(session, CaptureSequence, out originalCandidate));
        TestAssert.True(TryCapture(session, LaterSequence + 1, out nextCandidate));
        TestAssert.False(session.TryCompleteRead(originalCandidate, true, true, StartTime));
        TestAssert.True(session.TryCompleteRead(nextCandidate, true, true, StartTime));
    }

    internal static void MissingImagesCancelAndSequenceNumbersCanWrap()
    {
        var session = new ScreenshotCaptureSession();
        session.Arm(true, InitialSequence, StartTime);
        TestAssert.True(TryCapture(session, CaptureSequence, out var candidate));
        TestAssert.False(session.TryCompleteRead(candidate, false, true, StartTime));
        TestAssert.False(TryCapture(session, LaterSequence, out _));

        session.Arm(true, uint.MaxValue, StartTime);
        TestAssert.True(TryCapture(session, uint.MinValue, out candidate));
        TestAssert.True(session.TryCompleteRead(candidate, true, true, StartTime));
    }

    internal static void DelayedRenderingConsumesTheFinalSequence()
    {
        var session = new ScreenshotCaptureSession();
        session.Arm(true, InitialSequence, StartTime);
        TestAssert.True(TryCapture(session, CaptureSequence, out var candidate));
        TestAssert.True(session.TryCompleteRead(candidate, true, true, StartTime, LaterSequence));
        session.Arm(true, InitialSequence, StartTime);
        TestAssert.False(TryCapture(session, LaterSequence, out _));
        TestAssert.True(TryCapture(session, LaterSequence + 1, out _));
    }

    internal static void NativeListenerCanBeDisposedTwiceWithoutReadingTheClipboard() => RunNativeListenerLifecycleTest((_, listener) =>
    {
        listener.CancelPendingCapture();
        listener.Dispose();
        listener.Dispose();
    });

    internal static void NativeListenerCanBeDisposedAfterItsWindowIsDestroyed() => RunNativeListenerLifecycleTest((source, listener) =>
    {
        source.Dispose();
        listener.Dispose();
        listener.Dispose();
    });

    private static void RunNativeListenerLifecycleTest(Action<HwndSource, ScreenshotCaptureListener> test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var source = new HwndSource(new HwndSourceParameters("WinUtil screenshot listener lifecycle test")
                {
                    ParentWindow = MessageOnlyWindowHandle,
                    WindowStyle = 0
                });
                using var listener = new ScreenshotCaptureListener(source.Handle, () => false,
                    _ => throw new InvalidOperationException("A disabled screenshot listener must not open an editor."),
                    exception => throw new InvalidOperationException("The screenshot listener reported an unexpected error.", exception));
                test(source, listener);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException("The screenshot listener lifecycle test failed.", failure);
        }
    }

    private static bool TryCapture(ScreenshotCaptureSession session, uint sequenceNumber, out ScreenshotCaptureCandidate candidate) =>
        session.TryBeginRead(true, sequenceNumber, ScreenshotClipboardSource.ScreenshotTool, StartTime, out candidate);
}
