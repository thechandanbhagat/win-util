using WinUtil.Models;
using WinUtil.Services;

namespace WinUtil.Tests;

internal static class SpotlightActionCatalogTests
{
    private const int UuidVersionCharacterIndex = 14;
    private const int JwtSecretLengthFor512Bits = 86;

    internal static void FilterReturnsEveryActionForAnEmptyQuery()
    {
        var matchingActions = SpotlightActionCatalog.Filter(string.Empty);

        TestAssert.SequenceEqual(
            SpotlightActionCatalog.All.Select(action => action.Id),
            matchingActions.Select(action => action.Id));
    }

    internal static void FilterMatchesActionKeywords()
    {
        var matchingActions = SpotlightActionCatalog.Filter("guid");

        var action = TestAssert.Single(matchingActions);
        TestAssert.Equal("uuid", action.Id);
    }

    internal static void FilterReturnsNoActionsForAnUnknownQuery()
    {
        var matchingActions = SpotlightActionCatalog.Filter("does-not-exist");

        TestAssert.Empty(matchingActions);
    }

    internal static void GenerateUuidUsesTheConfiguredVersion()
    {
        var settings = WidgetSettings.Default with { UuidVersion = UuidVersion.Version7 };
        var action = SpotlightActionCatalog.Create(() => settings).Single(candidate => candidate.Id == "uuid");

        var uuid = action.GenerateOutput();

        TestAssert.Equal('7', uuid[UuidVersionCharacterIndex]);
    }

    internal static void FilterMatchesTheJwtSecretAction()
    {
        var actions = SpotlightActionCatalog.Filter("jwt");

        var action = TestAssert.Single(actions);
        TestAssert.Equal("jwt-secret", action.Id);
    }

    internal static void GenerateJwtSecretUsesTheConfiguredLength()
    {
        var settings = WidgetSettings.Default with
        {
            JwtSecretLengthBytes = WidgetSettings.JwtSecretBytesFor512Bits
        };
        var action = SpotlightActionCatalog.Create(() => settings).Single(candidate => candidate.Id == "jwt-secret");

        var secret = action.GenerateOutput();

        TestAssert.Equal(JwtSecretLengthFor512Bits, secret.Length);
    }

    internal static void FilterMatchesTheJsonTextTools()
    {
        var actions = SpotlightActionCatalog.Filter("json");

        TestAssert.SequenceEqual(
            new[] { SpotlightActionIds.FormatJson, SpotlightActionIds.MinifyJson },
            actions.Select(action => action.Id));
        TestAssert.True(actions.All(action => action.RequiresSelectedText));
    }

    internal static void Base64EncodeActionTransformsSelectedText()
    {
        var action = SpotlightActionCatalog.All.Single(candidate => candidate.Id == SpotlightActionIds.Base64Encode);

        var transformedText = action.TransformSelectedText("Ada");

        TestAssert.Equal("QWRh", transformedText);
    }
}
