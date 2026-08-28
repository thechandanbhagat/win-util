using System.Text.Json;

namespace WinUtil.Services;

internal static class JsonTextFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    internal static string Format(string json)
    {
        using var document = JsonDocument.Parse(json);

        return JsonSerializer.Serialize(document.RootElement, SerializerOptions);
    }

    internal static string Minify(string json)
    {
        using var document = JsonDocument.Parse(json);

        return JsonSerializer.Serialize(document.RootElement);
    }
}
