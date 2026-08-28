using System.IO;
using System.Text.Json;
using WinUtil.Models;

namespace WinUtil.Services;

internal sealed class SettingsStore
{
    private const string ApplicationDirectoryName = "WinUtil";
    private const string SettingsFileName = "settings.json";
    private const string TemporaryFileExtension = ".tmp";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string settingsFilePath;

    internal SettingsStore()
    {
        var applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        settingsFilePath = Path.Combine(applicationDataPath, ApplicationDirectoryName, SettingsFileName);
    }

    internal WidgetSettings Load()
    {
        if (!File.Exists(settingsFilePath))
        {
            return WidgetSettings.Default;
        }

        var serializedSettings = File.ReadAllText(settingsFilePath);
        var settings = JsonSerializer.Deserialize<WidgetSettings>(serializedSettings, SerializerOptions);

        return (settings ?? WidgetSettings.Default).Normalize();
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
}
