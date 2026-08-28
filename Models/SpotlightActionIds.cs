namespace WinUtil.Models;

internal static class SpotlightActionIds
{
    internal const string Base64Decode = "base64-decode";
    internal const string Base64Encode = "base64-encode";
    internal const string FormatJson = "format-json";
    internal const string JwtSecret = "jwt-secret";
    internal const string Lowercase = "lowercase";
    internal const string MinifyJson = "minify-json";
    internal const string Password = "password";
    internal const string Uppercase = "uppercase";
    internal const string UrlDecode = "url-decode";
    internal const string UrlEncode = "url-encode";
    internal const string Uuid = "uuid";

    internal static string GetDisplayName(string actionId) => actionId switch
    {
        Uuid => "Generate UUID",
        Password => "Generate password",
        JwtSecret => "Generate JWT secret",
        FormatJson => "Format JSON",
        MinifyJson => "Minify JSON",
        Base64Encode => "Base64 encode",
        Base64Decode => "Base64 decode",
        UrlEncode => "URL encode",
        UrlDecode => "URL decode",
        Uppercase => "Convert to uppercase",
        Lowercase => "Convert to lowercase",
        _ => throw new ArgumentOutOfRangeException(nameof(actionId), actionId, "Unsupported Spotlight action.")
    };
}
