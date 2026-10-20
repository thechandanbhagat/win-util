using Microsoft.Win32;
using WinUtil.Services;

namespace WinUtil.Tests;

internal static class StartupRegistrationTests
{
    private const string ExecutablePath = @"C:\Users\Test User\AppData\Local\Programs\WinUtil\WinUtil.exe";

    internal static void CreateCommandQuotesTheExecutablePath()
    {
        TestAssert.Equal($"\"{ExecutablePath}\"", StartupRegistration.CreateCommand(ExecutablePath));
        TestAssert.Equal("\"C:\\WinUtil.exe\"", StartupRegistration.CreateCommand(@"C:\WinUtil.exe"));
    }

    internal static void CreateCommandRejectsMissingOrInvalidExecutablePaths()
    {
        foreach (var path in new string?[] { null, "", " ", "WinUtil.exe", @"C:\WinUtil.dll", "C:\\bad\"path\\WinUtil.exe", "C:\\bad\npath\\WinUtil.exe", "C:\\bad\0path\\WinUtil.exe" })
        {
            ExpectException<ArgumentException>(() => StartupRegistration.CreateCommand(path));
        }
    }

    internal static void CreateCommandEnforcesTheWindowsRunCommandLimit()
    {
        const string prefix = @"C:\";
        const string extension = ".exe";
        const int surroundingQuoteCount = 2;
        var longestPath = prefix + new string('a', StartupRegistration.MaximumCommandLength - prefix.Length - extension.Length - surroundingQuoteCount) + extension;

        TestAssert.Equal(StartupRegistration.MaximumCommandLength, StartupRegistration.CreateCommand(longestPath).Length);
        ExpectException<ArgumentException>(() => StartupRegistration.CreateCommand(prefix + "a" + longestPath[prefix.Length..]));
    }

    internal static void ApplyRegistersAndUpdatesTheQuotedExecutable()
    {
        WithTemporaryRegistryKey(keyPath =>
        {
            var registration = CreateRegistration(keyPath);
            registration.Apply(true);

            using (var key = Registry.CurrentUser.OpenSubKey(keyPath)!)
            {
                TestAssert.Equal($"\"{ExecutablePath}\"", key.GetValue(StartupRegistration.RunValueName));
                TestAssert.Equal(RegistryValueKind.String, key.GetValueKind(StartupRegistration.RunValueName));
            }

            CreateRegistration(keyPath, @"C:\Updated\WinUtil.exe").Apply(true);

            using var updatedKey = Registry.CurrentUser.OpenSubKey(keyPath)!;
            TestAssert.Equal("\"C:\\Updated\\WinUtil.exe\"", updatedKey.GetValue(StartupRegistration.RunValueName));
        });
    }

    internal static void ApplyDisablesOnlyWinUtilAndAllowsAnAbsentValue()
    {
        WithTemporaryRegistryKey(keyPath =>
        {
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                key.SetValue(StartupRegistration.RunValueName, "old registration");
                key.SetValue("OtherApp", "keep this registration");
            }

            var registration = CreateRegistration(keyPath, null);
            registration.Apply(false);
            registration.Apply(false);

            using var keyAfterDisable = Registry.CurrentUser.OpenSubKey(keyPath)!;
            TestAssert.Equal<object?>(null, keyAfterDisable.GetValue(StartupRegistration.RunValueName));
            TestAssert.Equal("keep this registration", keyAfterDisable.GetValue("OtherApp"));
        });
    }

    internal static void ApplyValidatesBeforeOpeningTheRegistry()
    {
        var wasOpened = false;
        var registration = new StartupRegistration(null, () =>
        {
            wasOpened = true;
            return null;
        });

        ExpectException<ArgumentException>(() => registration.Apply(true));
        TestAssert.False(wasOpened);
    }

    internal static void ApplyReportsRegistryFailures()
    {
        var deniedRegistration = new StartupRegistration(ExecutablePath, () => throw new UnauthorizedAccessException("Test access denied."));
        var missingKeyRegistration = new StartupRegistration(ExecutablePath, () => null);

        ExpectException<UnauthorizedAccessException>(() => deniedRegistration.Apply(true));
        ExpectException<InvalidOperationException>(() => missingKeyRegistration.Apply(true));
        ExpectException<ArgumentNullException>(() => new StartupRegistration(ExecutablePath, null!));
    }

    private static StartupRegistration CreateRegistration(string keyPath, string? executablePath = ExecutablePath) =>
        new(executablePath, () => Registry.CurrentUser.CreateSubKey(keyPath, writable: true));

    private static void WithTemporaryRegistryKey(Action<string> test)
    {
        var keyPath = $@"Software\WinUtil.Tests\{Guid.NewGuid():N}";

        try
        {
            test(keyPath);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
    }

    private static void ExpectException<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }
}
