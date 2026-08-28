using System.Text.Json;
using WinUtil.Services;

namespace WinUtil.Tests;

internal static class JsonTextFormatterTests
{
    internal static void FormatPrettyPrintsValidJson()
    {
        var formattedJson = JsonTextFormatter.Format("{\"name\":\"Ada\",\"roles\":[\"admin\"]}");

        TestAssert.True(formattedJson.Contains("\n  \"name\": \"Ada\""));
        TestAssert.True(formattedJson.Contains("\n    \"admin\""));
    }

    internal static void FormatRejectsInvalidJson()
    {
        try
        {
            JsonTextFormatter.Format("not-json");
        }
        catch (JsonException)
        {
            return;
        }

        throw new InvalidOperationException("Expected invalid JSON to be rejected.");
    }

    internal static void MinifyRemovesInsignificantWhitespace()
    {
        var minifiedJson = JsonTextFormatter.Minify("{\n  \"name\": \"Ada\",\n  \"roles\": [\"admin\"]\n}");

        TestAssert.Equal("{\"name\":\"Ada\",\"roles\":[\"admin\"]}", minifiedJson);
    }
}
