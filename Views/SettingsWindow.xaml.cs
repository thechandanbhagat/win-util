using System.Windows;
using System.Windows.Input;
using WinUtil.Models;
using WinUtil.Services;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;

namespace WinUtil.Views;

public partial class SettingsWindow : Window
{
    private const double PercentageMultiplier = 100;
    private const int JwtSecret256BitOptionIndex = 0;
    private const int JwtSecret384BitOptionIndex = 1;
    private const int JwtSecret512BitOptionIndex = 2;
    private const int UuidSettingsPageIndex = 0;
    private const int PasswordSettingsPageIndex = 1;
    private const int JwtSecretSettingsPageIndex = 2;
    private const int TextToolsSettingsPageIndex = 3;

    private readonly WidgetSettings initialSettings;
    private readonly SettingsStore settingsStore;
    private readonly Action<Action<ShortcutCaptureResult>> beginShortcutCapture;
    private readonly Action endShortcutCapture;
    private readonly Func<SpotlightShortcuts, string?> tryApplySpotlightShortcuts;
    private bool isChangingSettingsPage;
    private GlobalShortcut? base64DecodeShortcut;
    private GlobalShortcut? base64EncodeShortcut;
    private GlobalShortcut? formatJsonShortcut;
    private GlobalShortcut? jwtSecretShortcut;
    private GlobalShortcut? lowercaseShortcut;
    private GlobalShortcut? minifyJsonShortcut;
    private GlobalShortcut? passwordShortcut;
    private GlobalShortcut? uppercaseShortcut;
    private GlobalShortcut? urlDecodeShortcut;
    private GlobalShortcut? urlEncodeShortcut;
    private GlobalShortcut? uuidShortcut;

    internal SettingsWindow(
        SettingsStore settingsStore,
        WidgetSettings settings,
        Func<SpotlightShortcuts, string?> tryApplySpotlightShortcuts,
        Action<Action<ShortcutCaptureResult>> beginShortcutCapture,
        Action endShortcutCapture)
    {
        initialSettings = settings;
        this.settingsStore = settingsStore;
        this.tryApplySpotlightShortcuts = tryApplySpotlightShortcuts;
        this.beginShortcutCapture = beginShortcutCapture;
        this.endShortcutCapture = endShortcutCapture;
        InitializeComponent();

        KeepOnTopCheckBox.IsChecked = settings.KeepOnTop;
        ShowSecondsCheckBox.IsChecked = settings.ShowSeconds;
        AutoHideDelayComboBox.SelectedIndex = settings.AutoHideDelaySeconds == WidgetSettings.ShortAutoHideDelaySeconds
            ? 0
            : 1;
        BackgroundOpacitySlider.Value = settings.BackgroundOpacity;
        FadedOpacitySlider.Value = settings.FadedOpacity;
        OpacitySlider.Value = settings.Opacity;
        PasswordIncludesDigitsCheckBox.IsChecked = settings.PasswordIncludesDigits;
        PasswordIncludesLowercaseCheckBox.IsChecked = settings.PasswordIncludesLowercase;
        PasswordIncludesSymbolsCheckBox.IsChecked = settings.PasswordIncludesSymbols;
        PasswordIncludesUppercaseCheckBox.IsChecked = settings.PasswordIncludesUppercase;
        PasswordLengthSlider.Value = settings.PasswordLength;
        UuidVersionComboBox.SelectedIndex = settings.UuidVersion == UuidVersion.Version7 ? 1 : 0;
        JwtSecretStrengthComboBox.SelectedIndex = settings.JwtSecretLengthBytes switch
        {
            WidgetSettings.JwtSecretBytesFor384Bits => JwtSecret384BitOptionIndex,
            WidgetSettings.JwtSecretBytesFor512Bits => JwtSecret512BitOptionIndex,
            _ => JwtSecret256BitOptionIndex
        };
        uuidShortcut = settings.SpotlightShortcuts.Uuid;
        passwordShortcut = settings.SpotlightShortcuts.Password;
        jwtSecretShortcut = settings.SpotlightShortcuts.JwtSecret;
        formatJsonShortcut = settings.SpotlightShortcuts.FormatJson;
        minifyJsonShortcut = settings.SpotlightShortcuts.MinifyJson;
        base64EncodeShortcut = settings.SpotlightShortcuts.Base64Encode;
        base64DecodeShortcut = settings.SpotlightShortcuts.Base64Decode;
        urlEncodeShortcut = settings.SpotlightShortcuts.UrlEncode;
        urlDecodeShortcut = settings.SpotlightShortcuts.UrlDecode;
        uppercaseShortcut = settings.SpotlightShortcuts.Uppercase;
        lowercaseShortcut = settings.SpotlightShortcuts.Lowercase;
        UpdateShortcutText(SpotlightActionIds.Uuid);
        UpdateShortcutText(SpotlightActionIds.Password);
        UpdateShortcutText(SpotlightActionIds.JwtSecret);
        UpdateShortcutText(SpotlightActionIds.FormatJson);
        UpdateShortcutText(SpotlightActionIds.MinifyJson);
        UpdateShortcutText(SpotlightActionIds.Base64Encode);
        UpdateShortcutText(SpotlightActionIds.Base64Decode);
        UpdateShortcutText(SpotlightActionIds.UrlEncode);
        UpdateShortcutText(SpotlightActionIds.UrlDecode);
        UpdateShortcutText(SpotlightActionIds.Uppercase);
        UpdateShortcutText(SpotlightActionIds.Lowercase);
        SettingsNavigationListBox.SelectedIndex = 0;
    }

    internal event Action<WidgetSettings>? SettingsSaved;

    private void HandleCancel(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void HandleOpacityValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText is not null)
        {
            OpacityValueText.Text = $"{OpacitySlider.Value * PercentageMultiplier:0}%";
        }
    }

    private void HandleBackgroundOpacityValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BackgroundOpacityValueText is not null)
        {
            BackgroundOpacityValueText.Text = $"{BackgroundOpacitySlider.Value * PercentageMultiplier:0}%";
        }
    }

    private void HandleFadedOpacityValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FadedOpacityValueText is not null)
        {
            FadedOpacityValueText.Text = $"{FadedOpacitySlider.Value * PercentageMultiplier:0}%";
        }
    }

    private void HandlePasswordLengthValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PasswordLengthValueText is not null)
        {
            PasswordLengthValueText.Text = $"{PasswordLengthSlider.Value:0} characters";
        }
    }

    private void HandlePasswordCharacterSetChanged(object sender, RoutedEventArgs e) =>
        PasswordCharacterSetErrorText.Visibility = Visibility.Collapsed;

    private void HandleSpotlightNavigationSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (isChangingSettingsPage || SpotlightNavigationListBox.SelectedIndex < 0)
        {
            return;
        }

        ShowSettingsPage(SpotlightNavigationListBox.SelectedIndex switch
        {
            UuidSettingsPageIndex => SettingsPage.Uuid,
            PasswordSettingsPageIndex => SettingsPage.Password,
            JwtSecretSettingsPageIndex => SettingsPage.JwtSecret,
            TextToolsSettingsPageIndex => SettingsPage.TextTools,
            _ => throw new ArgumentOutOfRangeException(nameof(SpotlightNavigationListBox.SelectedIndex))
        });
    }

    private void HandleWidgetNavigationSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (isChangingSettingsPage || SettingsNavigationListBox.SelectedIndex < 0)
        {
            return;
        }

        ShowSettingsPage(SettingsPage.Widget);
    }

    private void HandleSave(object sender, RoutedEventArgs e)
    {
        if (!HasPasswordCharacterSet())
        {
            PasswordCharacterSetErrorText.Visibility = Visibility.Visible;
            return;
        }

        var settings = new WidgetSettings
        {
            KeepOnTop = KeepOnTopCheckBox.IsChecked ?? WidgetSettings.Default.KeepOnTop,
            AutoHideDelaySeconds = AutoHideDelayComboBox.SelectedIndex == 0
                ? WidgetSettings.ShortAutoHideDelaySeconds
                : WidgetSettings.DefaultAutoHideDelaySeconds,
            BackgroundOpacity = BackgroundOpacitySlider.Value,
            FadedOpacity = FadedOpacitySlider.Value,
            Left = initialSettings.Left,
            Opacity = OpacitySlider.Value,
            JwtSecretLengthBytes = JwtSecretStrengthComboBox.SelectedIndex switch
            {
                JwtSecret384BitOptionIndex => WidgetSettings.JwtSecretBytesFor384Bits,
                JwtSecret512BitOptionIndex => WidgetSettings.JwtSecretBytesFor512Bits,
                _ => WidgetSettings.JwtSecretBytesFor256Bits
            },
            PasswordIncludesDigits = PasswordIncludesDigitsCheckBox.IsChecked ?? WidgetSettings.Default.PasswordIncludesDigits,
            PasswordIncludesLowercase = PasswordIncludesLowercaseCheckBox.IsChecked ?? WidgetSettings.Default.PasswordIncludesLowercase,
            PasswordIncludesSymbols = PasswordIncludesSymbolsCheckBox.IsChecked ?? WidgetSettings.Default.PasswordIncludesSymbols,
            PasswordIncludesUppercase = PasswordIncludesUppercaseCheckBox.IsChecked ?? WidgetSettings.Default.PasswordIncludesUppercase,
            PasswordLength = (int)PasswordLengthSlider.Value,
            ShowSeconds = ShowSecondsCheckBox.IsChecked ?? WidgetSettings.Default.ShowSeconds,
            SpotlightShortcuts = new SpotlightShortcuts
            {
                FormatJson = formatJsonShortcut,
                Base64Decode = base64DecodeShortcut,
                Base64Encode = base64EncodeShortcut,
                JwtSecret = jwtSecretShortcut,
                Lowercase = lowercaseShortcut,
                MinifyJson = minifyJsonShortcut,
                Password = passwordShortcut,
                Uppercase = uppercaseShortcut,
                UrlDecode = urlDecodeShortcut,
                UrlEncode = urlEncodeShortcut,
                Uuid = uuidShortcut
            },
            Top = initialSettings.Top,
            UuidVersion = UuidVersionComboBox.SelectedIndex == 1 ? UuidVersion.Version7 : UuidVersion.Version4
        };

        var shortcutError = tryApplySpotlightShortcuts(settings.SpotlightShortcuts);

        if (shortcutError is not null)
        {
            ShowShortcutError(shortcutError);
            return;
        }

        settingsStore.Save(settings);
        SettingsSaved?.Invoke(settings);
        Close();
    }

    private bool HasPasswordCharacterSet() => PasswordIncludesDigitsCheckBox.IsChecked == true
        || PasswordIncludesLowercaseCheckBox.IsChecked == true
        || PasswordIncludesSymbolsCheckBox.IsChecked == true
        || PasswordIncludesUppercaseCheckBox.IsChecked == true;

    private void HandleClearShortcut(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string actionId })
        {
            throw new InvalidOperationException("The shortcut clear button must identify a Spotlight action.");
        }

        SetShortcut(actionId, null);
        ShortcutErrorText.Visibility = Visibility.Collapsed;
    }

    private void HandleShortcutCaptureGotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox { Tag: string actionId })
        {
            throw new InvalidOperationException("The shortcut input must identify a Spotlight action.");
        }

        beginShortcutCapture(result => HandleShortcutCaptureResult(actionId, result));
    }

    private void HandleShortcutCaptureLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e) =>
        endShortcutCapture();

    private void HandleShortcutCaptureResult(string actionId, ShortcutCaptureResult result)
    {
        if (result.ErrorMessage is { } errorMessage)
        {
            ShowShortcutError(errorMessage);
            return;
        }

        if (result.Shortcut is null)
        {
            throw new InvalidOperationException("A shortcut capture result must include a shortcut or an error.");
        }

        SetShortcut(actionId, result.Shortcut);
        ShortcutErrorText.Visibility = Visibility.Collapsed;
    }

    private static string FormatShortcut(GlobalShortcut? shortcut)
    {
        if (shortcut is null)
        {
            return "Not assigned";
        }

        var parts = new List<string>();

        if ((shortcut.Modifiers & GlobalShortcut.ControlModifier) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((shortcut.Modifiers & GlobalShortcut.AltModifier) != 0)
        {
            parts.Add("Alt");
        }

        if ((shortcut.Modifiers & GlobalShortcut.ShiftModifier) != 0)
        {
            parts.Add("Shift");
        }

        parts.Add(FormatKey(KeyInterop.KeyFromVirtualKey((int)shortcut.VirtualKey)));
        return string.Join('+', parts);
    }

    private static string FormatKey(Key key) => key switch
    {
        Key.D0 => "0",
        Key.D1 => "1",
        Key.D2 => "2",
        Key.D3 => "3",
        Key.D4 => "4",
        Key.D5 => "5",
        Key.D6 => "6",
        Key.D7 => "7",
        Key.D8 => "8",
        Key.D9 => "9",
        Key.OemMinus => "-",
        Key.OemPlus => "+",
        Key.OemQuestion => "/",
        _ => key.ToString()
    };

    private GlobalShortcut? GetShortcut(string actionId) => actionId switch
    {
        SpotlightActionIds.Uuid => uuidShortcut,
        SpotlightActionIds.Password => passwordShortcut,
        SpotlightActionIds.JwtSecret => jwtSecretShortcut,
        SpotlightActionIds.FormatJson => formatJsonShortcut,
        SpotlightActionIds.MinifyJson => minifyJsonShortcut,
        SpotlightActionIds.Base64Encode => base64EncodeShortcut,
        SpotlightActionIds.Base64Decode => base64DecodeShortcut,
        SpotlightActionIds.UrlEncode => urlEncodeShortcut,
        SpotlightActionIds.UrlDecode => urlDecodeShortcut,
        SpotlightActionIds.Uppercase => uppercaseShortcut,
        SpotlightActionIds.Lowercase => lowercaseShortcut,
        _ => throw new ArgumentOutOfRangeException(nameof(actionId), actionId, "Unsupported Spotlight action.")
    };

    private void SetShortcut(string actionId, GlobalShortcut? shortcut)
    {
        switch (actionId)
        {
            case SpotlightActionIds.Uuid:
                uuidShortcut = shortcut;
                break;
            case SpotlightActionIds.Password:
                passwordShortcut = shortcut;
                break;
            case SpotlightActionIds.JwtSecret:
                jwtSecretShortcut = shortcut;
                break;
            case SpotlightActionIds.FormatJson:
                formatJsonShortcut = shortcut;
                break;
            case SpotlightActionIds.MinifyJson:
                minifyJsonShortcut = shortcut;
                break;
            case SpotlightActionIds.Base64Encode:
                base64EncodeShortcut = shortcut;
                break;
            case SpotlightActionIds.Base64Decode:
                base64DecodeShortcut = shortcut;
                break;
            case SpotlightActionIds.UrlEncode:
                urlEncodeShortcut = shortcut;
                break;
            case SpotlightActionIds.UrlDecode:
                urlDecodeShortcut = shortcut;
                break;
            case SpotlightActionIds.Uppercase:
                uppercaseShortcut = shortcut;
                break;
            case SpotlightActionIds.Lowercase:
                lowercaseShortcut = shortcut;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(actionId), actionId, "Unsupported Spotlight action.");
        }

        UpdateShortcutText(actionId);
    }

    private void ShowShortcutError(string message)
    {
        ShortcutErrorText.Text = message;
        ShortcutErrorText.Visibility = Visibility.Visible;
    }

    private void UpdateShortcutText(string actionId)
    {
        var textBox = actionId switch
        {
            SpotlightActionIds.Uuid => UuidShortcutTextBox,
            SpotlightActionIds.Password => PasswordShortcutTextBox,
            SpotlightActionIds.JwtSecret => JwtSecretShortcutTextBox,
            SpotlightActionIds.FormatJson => FormatJsonShortcutTextBox,
            SpotlightActionIds.MinifyJson => MinifyJsonShortcutTextBox,
            SpotlightActionIds.Base64Encode => Base64EncodeShortcutTextBox,
            SpotlightActionIds.Base64Decode => Base64DecodeShortcutTextBox,
            SpotlightActionIds.UrlEncode => UrlEncodeShortcutTextBox,
            SpotlightActionIds.UrlDecode => UrlDecodeShortcutTextBox,
            SpotlightActionIds.Uppercase => UppercaseShortcutTextBox,
            SpotlightActionIds.Lowercase => LowercaseShortcutTextBox,
            _ => throw new ArgumentOutOfRangeException(nameof(actionId), actionId, "Unsupported Spotlight action.")
        };

        textBox.Text = FormatShortcut(GetShortcut(actionId));
    }

    private void ShowSettingsPage(SettingsPage page)
    {
        isChangingSettingsPage = true;

        try
        {
            WidgetSettingsPanel.Visibility = page == SettingsPage.Widget ? Visibility.Visible : Visibility.Collapsed;
            UuidSettingsPanel.Visibility = page == SettingsPage.Uuid ? Visibility.Visible : Visibility.Collapsed;
            PasswordSettingsPanel.Visibility = page == SettingsPage.Password ? Visibility.Visible : Visibility.Collapsed;
            JwtSecretSettingsPanel.Visibility = page == SettingsPage.JwtSecret ? Visibility.Visible : Visibility.Collapsed;
            TextToolsSettingsPanel.Visibility = page == SettingsPage.TextTools ? Visibility.Visible : Visibility.Collapsed;
            SettingsNavigationListBox.SelectedIndex = page == SettingsPage.Widget ? 0 : -1;
            SpotlightNavigationListBox.SelectedIndex = page switch
            {
                SettingsPage.Uuid => UuidSettingsPageIndex,
                SettingsPage.Password => PasswordSettingsPageIndex,
                SettingsPage.JwtSecret => JwtSecretSettingsPageIndex,
                SettingsPage.TextTools => TextToolsSettingsPageIndex,
                _ => -1
            };
        }
        finally
        {
            isChangingSettingsPage = false;
        }
    }

    private enum SettingsPage
    {
        Widget,
        Uuid,
        Password,
        JwtSecret,
        TextTools
    }
}
