using System.IO;
using Microsoft.Win32;

namespace WinUtil.Services;

internal sealed class StartupRegistration
{
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string RunValueName = "WinUtil";
    internal const int MaximumCommandLength = 260;

    private readonly string? executablePath;
    private readonly Func<RegistryKey?> openRunKey;

    internal StartupRegistration()
        : this(Environment.ProcessPath, () => Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true))
    {
    }

    internal StartupRegistration(string? executablePath, Func<RegistryKey?> openRunKey)
    {
        ArgumentNullException.ThrowIfNull(openRunKey);
        this.executablePath = executablePath;
        this.openRunKey = openRunKey;
    }

    internal void Apply(bool enabled)
    {
        var command = enabled ? CreateCommand(executablePath) : null;
        using var key = openRunKey()
            ?? throw new InvalidOperationException("The Windows startup registry key could not be opened.");

        if (enabled)
        {
            if (!string.Equals(key.GetValue(RunValueName) as string, command, StringComparison.Ordinal)
                || key.GetValueKind(RunValueName) != RegistryValueKind.String)
            {
                key.SetValue(RunValueName, command!, RegistryValueKind.String);
            }
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }

    internal static string CreateCommand(string? executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        if (!Path.IsPathFullyQualified(executablePath)
            || executablePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0
            || executablePath.Contains('"')
            || executablePath.Any(char.IsControl)
            || !string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Windows startup requires the full path to the WinUtil executable.", nameof(executablePath));
        }

        var command = $"\"{executablePath}\"";

        if (command.Length > MaximumCommandLength)
        {
            throw new ArgumentException($"The Windows startup command cannot exceed {MaximumCommandLength} characters. Install WinUtil in a shorter path.", nameof(executablePath));
        }

        return command;
    }
}
