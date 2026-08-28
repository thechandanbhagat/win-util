using WinUtil.Models;
using WinUtil.Services;

namespace WinUtil.Tests;

internal static class JwtSecretGeneratorTests
{
    private const int JwtSecretLength = 43;
    private const int JwtSecretLengthFor512Bits = 86;

    internal static void GenerateCreatesABase64UrlSecret()
    {
        var secret = JwtSecretGenerator.Generate(WidgetSettings.Default);

        TestAssert.Equal(JwtSecretLength, secret.Length);
        TestAssert.True(secret.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
    }

    internal static void GenerateUsesTheConfiguredSecretLength()
    {
        var settings = WidgetSettings.Default with
        {
            JwtSecretLengthBytes = WidgetSettings.JwtSecretBytesFor512Bits
        };

        var secret = JwtSecretGenerator.Generate(settings);

        TestAssert.Equal(JwtSecretLengthFor512Bits, secret.Length);
    }
}
