using WinUtil.Models;
using WinUtil.Services;

namespace WinUtil.Tests;

internal static class PasswordGeneratorTests
{
    private const int PasswordLength = 32;

    internal static void GenerateUsesOnlyTheSelectedCharacterSets()
    {
        var settings = new WidgetSettings
        {
            PasswordIncludesDigits = true,
            PasswordIncludesLowercase = false,
            PasswordIncludesSymbols = false,
            PasswordIncludesUppercase = false,
            PasswordLength = PasswordLength
        };

        var password = PasswordGenerator.Generate(settings);

        TestAssert.Equal(PasswordLength, password.Length);
        TestAssert.True(password.All(char.IsDigit));
    }

    internal static void GenerateIncludesEverySelectedCharacterSet()
    {
        var password = PasswordGenerator.Generate(WidgetSettings.Default);

        TestAssert.True(password.Any(char.IsUpper));
        TestAssert.True(password.Any(char.IsLower));
        TestAssert.True(password.Any(char.IsDigit));
        TestAssert.True(password.Any(character => "!@#$-_".Contains(character)));
    }
}
