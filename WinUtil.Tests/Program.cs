namespace WinUtil.Tests;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Execute)[]
        {
            (nameof(SpotlightActionCatalogTests.FilterReturnsEveryActionForAnEmptyQuery), SpotlightActionCatalogTests.FilterReturnsEveryActionForAnEmptyQuery),
            (nameof(SpotlightActionCatalogTests.FilterMatchesActionKeywords), SpotlightActionCatalogTests.FilterMatchesActionKeywords),
            (nameof(SpotlightActionCatalogTests.FilterReturnsNoActionsForAnUnknownQuery), SpotlightActionCatalogTests.FilterReturnsNoActionsForAnUnknownQuery),
            (nameof(SpotlightActionCatalogTests.GenerateUuidUsesTheConfiguredVersion), SpotlightActionCatalogTests.GenerateUuidUsesTheConfiguredVersion),
            (nameof(SpotlightActionCatalogTests.FilterMatchesTheJwtSecretAction), SpotlightActionCatalogTests.FilterMatchesTheJwtSecretAction),
            (nameof(SpotlightActionCatalogTests.GenerateJwtSecretUsesTheConfiguredLength), SpotlightActionCatalogTests.GenerateJwtSecretUsesTheConfiguredLength),
            (nameof(SpotlightActionCatalogTests.FilterMatchesTheJsonTextTools), SpotlightActionCatalogTests.FilterMatchesTheJsonTextTools),
            (nameof(SpotlightActionCatalogTests.Base64EncodeActionTransformsSelectedText), SpotlightActionCatalogTests.Base64EncodeActionTransformsSelectedText),
            (nameof(SpotlightViewModelTests.InitialStateHidesActionsUntilAQueryIsEntered), SpotlightViewModelTests.InitialStateHidesActionsUntilAQueryIsEntered),
            (nameof(SpotlightViewModelTests.QueryRetainsTheSelectionWhenItStillMatches), SpotlightViewModelTests.QueryRetainsTheSelectionWhenItStillMatches),
            (nameof(SpotlightViewModelTests.ResetClearsTheQueryAndHidesActions), SpotlightViewModelTests.ResetClearsTheQueryAndHidesActions),
            (nameof(SpotlightViewModelTests.ChangingTheQueryClearsAnInsertionError), SpotlightViewModelTests.ChangingTheQueryClearsAnInsertionError),
            (nameof(SpotlightViewModelTests.MoveSelectionDoesNothingWhenThereAreNoMatches), SpotlightViewModelTests.MoveSelectionDoesNothingWhenThereAreNoMatches),
            (nameof(ForegroundTextInjectorTests.InputStructureMatchesTheWindowsAbi), ForegroundTextInjectorTests.InputStructureMatchesTheWindowsAbi),
            (nameof(PasswordGeneratorTests.GenerateUsesOnlyTheSelectedCharacterSets), PasswordGeneratorTests.GenerateUsesOnlyTheSelectedCharacterSets),
            (nameof(PasswordGeneratorTests.GenerateIncludesEverySelectedCharacterSet), PasswordGeneratorTests.GenerateIncludesEverySelectedCharacterSet),
            (nameof(JwtSecretGeneratorTests.GenerateCreatesABase64UrlSecret), JwtSecretGeneratorTests.GenerateCreatesABase64UrlSecret),
            (nameof(JwtSecretGeneratorTests.GenerateUsesTheConfiguredSecretLength), JwtSecretGeneratorTests.GenerateUsesTheConfiguredSecretLength),
            (nameof(JsonTextFormatterTests.FormatPrettyPrintsValidJson), JsonTextFormatterTests.FormatPrettyPrintsValidJson),
            (nameof(JsonTextFormatterTests.FormatRejectsInvalidJson), JsonTextFormatterTests.FormatRejectsInvalidJson),
            (nameof(JsonTextFormatterTests.MinifyRemovesInsignificantWhitespace), JsonTextFormatterTests.MinifyRemovesInsignificantWhitespace),
            (nameof(SingleInstanceGateTests.SecondGateDetectsAnExistingInstance), SingleInstanceGateTests.SecondGateDetectsAnExistingInstance),
            (nameof(StartupRegistrationTests.CreateCommandQuotesTheExecutablePath), StartupRegistrationTests.CreateCommandQuotesTheExecutablePath),
            (nameof(StartupRegistrationTests.CreateCommandRejectsMissingOrInvalidExecutablePaths), StartupRegistrationTests.CreateCommandRejectsMissingOrInvalidExecutablePaths),
            (nameof(StartupRegistrationTests.CreateCommandEnforcesTheWindowsRunCommandLimit), StartupRegistrationTests.CreateCommandEnforcesTheWindowsRunCommandLimit),
            (nameof(StartupRegistrationTests.ApplyRegistersAndUpdatesTheQuotedExecutable), StartupRegistrationTests.ApplyRegistersAndUpdatesTheQuotedExecutable),
            (nameof(StartupRegistrationTests.ApplyDisablesOnlyWinUtilAndAllowsAnAbsentValue), StartupRegistrationTests.ApplyDisablesOnlyWinUtilAndAllowsAnAbsentValue),
            (nameof(StartupRegistrationTests.ApplyValidatesBeforeOpeningTheRegistry), StartupRegistrationTests.ApplyValidatesBeforeOpeningTheRegistry),
            (nameof(StartupRegistrationTests.ApplyReportsRegistryFailures), StartupRegistrationTests.ApplyReportsRegistryFailures),
            (nameof(BatterySnapshotTests.HasDevicesIsFalseForAnEmptySnapshot), BatterySnapshotTests.HasDevicesIsFalseForAnEmptySnapshot),
            (nameof(BatterySnapshotTests.HasDevicesIsTrueWhenABatteryIsAvailable), BatterySnapshotTests.HasDevicesIsTrueWhenABatteryIsAvailable),
            (nameof(AudioDeviceSnapshotTests.HasDevicesTreatsBlankNamesAsUnavailable), AudioDeviceSnapshotTests.HasDevicesTreatsBlankNamesAsUnavailable),
            (nameof(AudioDeviceSnapshotTests.HasDevicesRecognizesReportedNames), AudioDeviceSnapshotTests.HasDevicesRecognizesReportedNames),
            (nameof(BatteryStatusProviderTests.ParseBluetoothDevicesExcludesDisconnectedDevices), BatteryStatusProviderTests.ParseBluetoothDevicesExcludesDisconnectedDevices),
            (nameof(BatteryStatusProviderTests.ParseBluetoothDevicesNormalizesConnectedDevices), BatteryStatusProviderTests.ParseBluetoothDevicesNormalizesConnectedDevices),
            (nameof(WidgetSettingsTests.NormalizeClampsOpacityValues), WidgetSettingsTests.NormalizeClampsOpacityValues),
            (nameof(WidgetSettingsTests.NormalizeUsesDefaultsForInvalidOpacityValues), WidgetSettingsTests.NormalizeUsesDefaultsForInvalidOpacityValues),
            (nameof(WidgetSettingsTests.NormalizeClampsSpotlightSettingsAndRestoresEmptyCharacterSets), WidgetSettingsTests.NormalizeClampsSpotlightSettingsAndRestoresEmptyCharacterSets),
            (nameof(SpotlightShortcutsTests.NormalizeRemovesInvalidShortcuts), SpotlightShortcutsTests.NormalizeRemovesInvalidShortcuts),
            (nameof(SpotlightShortcutsTests.NormalizePreservesValidShortcuts), SpotlightShortcutsTests.NormalizePreservesValidShortcuts),
            (nameof(SpotlightShortcutsTests.ValidateRejectsDuplicateFunctionShortcuts), SpotlightShortcutsTests.ValidateRejectsDuplicateFunctionShortcuts),
            (nameof(SpotlightShortcutsTests.ValidateReservesThePaletteShortcut), SpotlightShortcutsTests.ValidateReservesThePaletteShortcut),
            (nameof(AppTests.ApplySpotlightShortcutsKeepsASuccessfulRegistrationSuccessful), AppTests.ApplySpotlightShortcutsKeepsASuccessfulRegistrationSuccessful),
            (nameof(PhysicalShortcutProcessorTests.ProcessMatchesThePhysicalKeyAndWaitsForModifierRelease), PhysicalShortcutProcessorTests.ProcessMatchesThePhysicalKeyAndWaitsForModifierRelease),
            (nameof(PhysicalShortcutProcessorTests.ProcessCapturesTheRawPhysicalKey), PhysicalShortcutProcessorTests.ProcessCapturesTheRawPhysicalKey),
            (nameof(PhysicalShortcutProcessorTests.ProcessRejectsShortcutsWithoutAModifier), PhysicalShortcutProcessorTests.ProcessRejectsShortcutsWithoutAModifier),
            (nameof(TextTransformationsTests.Base64EncodeAndDecodeRoundTripUtf8Text), TextTransformationsTests.Base64EncodeAndDecodeRoundTripUtf8Text),
            (nameof(TextTransformationsTests.Base64DecodeRejectsInvalidText), TextTransformationsTests.Base64DecodeRejectsInvalidText),
            (nameof(TextTransformationsTests.UrlEncodeAndDecodeRoundTripText), TextTransformationsTests.UrlEncodeAndDecodeRoundTripText),
            (nameof(TextTransformationsTests.ChangeCaseUsesInvariantCasing), TextTransformationsTests.ChangeCaseUsesInvariantCasing),
            (nameof(WinUtilTrayIconTests.CreateProducesASystemTraySizedIcon), WinUtilTrayIconTests.CreateProducesASystemTraySizedIcon),
            (nameof(SettingsStoreTests.LoadReturnsDefaultsWhenNoFileExists), SettingsStoreTests.LoadReturnsDefaultsWhenNoFileExists),
            (nameof(SettingsStoreTests.SaveThenLoadRoundTripsSettings), SettingsStoreTests.SaveThenLoadRoundTripsSettings),
            (nameof(SettingsStoreTests.LoadQuarantinesCorruptJsonAndReturnsDefaults), SettingsStoreTests.LoadQuarantinesCorruptJsonAndReturnsDefaults),
            (nameof(SettingsStoreTests.LoadTreatsJsonNullAsDefaults), SettingsStoreTests.LoadTreatsJsonNullAsDefaults),
            (nameof(SettingsStoreTests.LoadDefaultsStartupToEnabledForExistingSettings), SettingsStoreTests.LoadDefaultsStartupToEnabledForExistingSettings),
            (nameof(CrashLogTests.WriteCreatesTheLogDirectoryAndAppendsTheException), CrashLogTests.WriteCreatesTheLogDirectoryAndAppendsTheException),
            (nameof(CrashLogTests.WriteDoesNotThrowWhenTheLogIsUnwritable), CrashLogTests.WriteDoesNotThrowWhenTheLogIsUnwritable),
            (nameof(SpotlightShortcutsTests.SupportedActionIdsMatchTheCatalog), SpotlightShortcutsTests.SupportedActionIdsMatchTheCatalog),
            (nameof(SpotlightShortcutsTests.WithShortcutRoundTripsThroughGetShortcut), SpotlightShortcutsTests.WithShortcutRoundTripsThroughGetShortcut),
            (nameof(SpotlightShortcutsTests.GetShortcutRejectsUnknownActions), SpotlightShortcutsTests.GetShortcutRejectsUnknownActions),
            (nameof(ShortcutSettingsViewModelTests.ConstructorExposesEveryCatalogAction), ShortcutSettingsViewModelTests.ConstructorExposesEveryCatalogAction),
            (nameof(ShortcutSettingsViewModelTests.TextToolGroupsOnlyContainSelectedTextActionsInCatalogOrder), ShortcutSettingsViewModelTests.TextToolGroupsOnlyContainSelectedTextActionsInCatalogOrder),
            (nameof(ShortcutSettingsViewModelTests.ToSpotlightShortcutsRoundTripsAssignedShortcuts), ShortcutSettingsViewModelTests.ToSpotlightShortcutsRoundTripsAssignedShortcuts),
            (nameof(ShortcutSettingsViewModelTests.SettingAShortcutUpdatesDisplayTextAndNotifies), ShortcutSettingsViewModelTests.SettingAShortcutUpdatesDisplayTextAndNotifies),
            (nameof(ShortcutSettingsViewModelTests.SettingTheSameShortcutDoesNotNotify), ShortcutSettingsViewModelTests.SettingTheSameShortcutDoesNotNotify),
            (nameof(ShortcutFormatterTests.FormatReturnsUnassignedForNull), ShortcutFormatterTests.FormatReturnsUnassignedForNull),
            (nameof(ShortcutFormatterTests.FormatOrdersModifiersCtrlAltShift), ShortcutFormatterTests.FormatOrdersModifiersCtrlAltShift),
            (nameof(ShortcutFormatterTests.FormatUsesPlainDigitsForNumberKeys), ShortcutFormatterTests.FormatUsesPlainDigitsForNumberKeys),
            (nameof(SettingsWindowTests.WindowRendersAShortcutEditorForEveryCatalogAction), SettingsWindowTests.WindowRendersAShortcutEditorForEveryCatalogAction),
            (nameof(SettingsWindowTests.StartupSettingAppliesAndPersistsWhenSaved), SettingsWindowTests.StartupSettingAppliesAndPersistsWhenSaved),
            (nameof(SettingsWindowTests.StartupSettingShowsAnErrorWhenRegistrationFails), SettingsWindowTests.StartupSettingShowsAnErrorWhenRegistrationFails),
            (nameof(SettingsWindowTests.StartupSettingRollsBackWhenSettingsCannotBeSaved), SettingsWindowTests.StartupSettingRollsBackWhenSettingsCannotBeSaved),
            (nameof(SettingsWindowTests.FailedSaveReportsShortcutAndStartupRollbackErrors), SettingsWindowTests.FailedSaveReportsShortcutAndStartupRollbackErrors)
        };
        var failures = new List<string>();

        foreach (var (name, execute) in tests)
        {
            try
            {
                execute();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures.Add($"FAIL {name}: {exception.Message}");
            }
        }

        foreach (var failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        return failures.Count == 0 ? 0 : 1;
    }
}
