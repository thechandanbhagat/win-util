using WinUtil.Models;

namespace WinUtil.Tests;

internal static class WidgetSettingsTests
{
    internal static void NormalizeClampsOpacityValues()
    {
        var settings = new WidgetSettings
        {
            BackgroundOpacity = 0,
            FadedOpacity = 1,
            Opacity = 1
        };

        var normalized = settings.Normalize();

        TestAssert.Equal(WidgetSettings.MinimumBackgroundOpacity, normalized.BackgroundOpacity);
        TestAssert.Equal(WidgetSettings.MaximumFadedOpacity, normalized.FadedOpacity);
        TestAssert.Equal(WidgetSettings.MaximumOpacity, normalized.Opacity);
    }

    internal static void NormalizeUsesDefaultsForInvalidOpacityValues()
    {
        var settings = new WidgetSettings
        {
            BackgroundOpacity = double.NaN,
            FadedOpacity = double.PositiveInfinity,
            Opacity = double.NegativeInfinity
        };

        var normalized = settings.Normalize();

        TestAssert.Equal(WidgetSettings.Default.BackgroundOpacity, normalized.BackgroundOpacity);
        TestAssert.Equal(WidgetSettings.Default.FadedOpacity, normalized.FadedOpacity);
        TestAssert.Equal(WidgetSettings.Default.Opacity, normalized.Opacity);
    }

    internal static void NormalizeClampsSpotlightSettingsAndRestoresEmptyCharacterSets()
    {
        var settings = new WidgetSettings
        {
            PasswordIncludesDigits = false,
            PasswordIncludesLowercase = false,
            PasswordIncludesSymbols = false,
            PasswordIncludesUppercase = false,
            PasswordLength = 0,
            JwtSecretLengthBytes = 1,
            UuidVersion = (UuidVersion)99
        };

        var normalized = settings.Normalize();

        TestAssert.Equal(WidgetSettings.MinimumPasswordLength, normalized.PasswordLength);
        TestAssert.Equal(WidgetSettings.DefaultJwtSecretLengthBytes, normalized.JwtSecretLengthBytes);
        TestAssert.True(normalized.PasswordIncludesDigits);
        TestAssert.True(normalized.PasswordIncludesLowercase);
        TestAssert.True(normalized.PasswordIncludesSymbols);
        TestAssert.True(normalized.PasswordIncludesUppercase);
        TestAssert.Equal(UuidVersion.Version4, normalized.UuidVersion);
    }
}
