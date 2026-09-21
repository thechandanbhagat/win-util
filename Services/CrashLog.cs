using System.IO;

namespace WinUtil.Services;

internal sealed class CrashLog
{
    private const string ApplicationDirectoryName = "WinUtil";
    private const string LogDirectoryName = "logs";
    private const string LogFileName = "crash.log";
    private const long MaximumLogSizeBytes = 512 * 1024;

    private readonly string logFilePath;

    internal CrashLog()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ApplicationDirectoryName,
            LogDirectoryName,
            LogFileName))
    {
    }

    internal CrashLog(string logFilePath)
    {
        this.logFilePath = logFilePath;
    }

    internal string LogFilePath => logFilePath;

    internal void Write(string source, Exception exception)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(logFilePath)
                ?? throw new InvalidOperationException("The crash log path must include a directory.");
            Directory.CreateDirectory(directoryPath);
            TruncateIfOversized();
            File.AppendAllText(logFilePath, $"[{DateTimeOffset.Now:O}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception writeException) when (writeException is IOException or UnauthorizedAccessException)
        {
            // Logging must never take the process down with it; there is nowhere else to report the failure.
        }
    }

    private void TruncateIfOversized()
    {
        var logFile = new FileInfo(logFilePath);
        if (logFile.Exists && logFile.Length > MaximumLogSizeBytes)
        {
            logFile.Delete();
        }
    }
}
