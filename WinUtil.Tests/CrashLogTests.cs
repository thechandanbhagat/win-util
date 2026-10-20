using WinUtil.Services;

namespace WinUtil.Tests;

internal static class CrashLogTests
{
    internal static void WriteCreatesTheLogDirectoryAndAppendsTheException()
    {
        using var directory = new TemporaryDirectory();
        var log = new CrashLog(directory.PathFor("logs", "crash.log"));

        log.Write("Test", new InvalidOperationException("first"));
        log.Write("Test", new InvalidOperationException("second"));

        var content = File.ReadAllText(log.LogFilePath);
        TestAssert.True(content.Contains("first", StringComparison.Ordinal));
        TestAssert.True(content.Contains("second", StringComparison.Ordinal));
    }

    internal static void WriteDoesNotThrowWhenTheLogIsUnwritable()
    {
        using var directory = new TemporaryDirectory();
        var blockingFilePath = directory.PathFor("logs");
        File.WriteAllText(blockingFilePath, string.Empty);
        var log = new CrashLog(Path.Combine(blockingFilePath, "crash.log"));

        log.Write("Test", new InvalidOperationException("ignored"));
    }
}
