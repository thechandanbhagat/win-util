namespace WinUtil.Services;

internal enum ScreenshotClipboardSource
{
    ScreenshotTool,
    ThisApplication,
    OtherApplication
}

internal readonly record struct ScreenshotCaptureCandidate(long Generation, uint SequenceNumber);

internal sealed class ScreenshotCaptureSession
{
    internal static readonly TimeSpan CaptureTimeout = TimeSpan.FromMinutes(2);

    private DateTimeOffset? expiresAt;
    private long generation;
    private uint initialSequenceNumber;
    private uint? lastConsumedSequenceNumber;
    private uint? pendingSequenceNumber;

    internal void Arm(bool enabled, uint sequenceNumber, DateTimeOffset now)
    {
        Cancel();

        if (enabled)
        {
            initialSequenceNumber = sequenceNumber;
            expiresAt = now + CaptureTimeout;
        }
    }

    internal void Cancel()
    {
        generation++;
        expiresAt = null;
        pendingSequenceNumber = null;
    }

    internal bool IsPending(bool enabled, DateTimeOffset now)
    {
        if (!enabled || expiresAt is { } expiry && now >= expiry)
        {
            Cancel();
        }

        return expiresAt is not null;
    }

    internal bool TryBeginRead(
        bool enabled,
        uint sequenceNumber,
        ScreenshotClipboardSource source,
        DateTimeOffset now,
        out ScreenshotCaptureCandidate candidate)
    {
        candidate = default;

        if (!IsPending(enabled, now)
            || sequenceNumber == initialSequenceNumber
            || sequenceNumber == lastConsumedSequenceNumber
            || sequenceNumber == pendingSequenceNumber)
        {
            return false;
        }

        if (source != ScreenshotClipboardSource.ScreenshotTool)
        {
            Cancel();
            return false;
        }

        pendingSequenceNumber = sequenceNumber;
        candidate = new ScreenshotCaptureCandidate(generation, sequenceNumber);
        return true;
    }

    internal bool IsCurrent(ScreenshotCaptureCandidate candidate, bool enabled, DateTimeOffset now) =>
        IsPending(enabled, now)
        && candidate.Generation == generation
        && candidate.SequenceNumber == pendingSequenceNumber;

    internal bool TryCompleteRead(
        ScreenshotCaptureCandidate candidate,
        bool hasImage,
        bool enabled,
        DateTimeOffset now,
        uint? renderedSequenceNumber = null)
    {
        if (!IsCurrent(candidate, enabled, now))
        {
            return false;
        }

        if (hasImage)
        {
            lastConsumedSequenceNumber = renderedSequenceNumber ?? candidate.SequenceNumber;
        }

        Cancel();
        return hasImage;
    }
}
