using System.IO;
using System.Text.Json;
using WinUtil.Models;

namespace WinUtil.Services;

internal sealed record SettingsLoadResult(WidgetSettings Settings, string? Warning);

internal sealed class SettingsStore
{
    private const string ApplicationDirectoryName = "WinUtil";
    private const string SettingsFileName = "settings.json";
    private const string TemporaryFileExtension = ".tmp";
    private const string CorruptBackupExtension = ".corrupt";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string settingsFilePath;

    internal SettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ApplicationDirectoryName,
            SettingsFileName))
    {
    }

    internal SettingsStore(string settingsFilePath)
    {
        this.settingsFilePath = settingsFilePath;
    }

    internal SettingsLoadResult Load()
    {
        if (!File.Exists(settingsFilePath))
        {
            return new SettingsLoadResult(WidgetSettings.Default, null);
        }

        try
        {
            var serializedSettings = File.ReadAllText(settingsFilePath);
            var settings = JsonSerializer.Deserialize<WidgetSettings>(serializedSettings, SerializerOptions);

            return new SettingsLoadResult((settings ?? WidgetSettings.Default).Normalize(), null);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            var backupPath = QuarantineCorruptFile();
            var warning = backupPath is null
                ? $"Settings could not be read ({exception.Message}). Defaults are in use."
                : $"Settings could not be read and were moved to {Path.GetFileName(backupPath)}. Defaults are in use.";

            return new SettingsLoadResult(WidgetSettings.Default, warning);
        }
    }

    internal void Save(WidgetSettings settings)
    {
        var normalizedSettings = settings.Normalize();
        var directoryPath = Path.GetDirectoryName(settingsFilePath)
            ?? throw new InvalidOperationException("The settings file path must include a directory.");
        var temporaryFilePath = settingsFilePath + TemporaryFileExtension;

        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(temporaryFilePath, JsonSerializer.Serialize(normalizedSettings, SerializerOptions));
        File.Move(temporaryFilePath, settingsFilePath, true);
    }

    private string? QuarantineCorruptFile()
    {
        var backupPath = settingsFilePath + CorruptBackupExtension;

        try
        {
            File.Move(settingsFilePath, backupPath, true);
            return backupPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
