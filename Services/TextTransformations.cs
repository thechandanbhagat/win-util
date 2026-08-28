using System.Text;

namespace WinUtil.Services;

internal static class TextTransformations
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static string Base64Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            return Utf8.GetString(Convert.FromBase64String(text));
        }
        catch (Exception exception) when (exception is FormatException or DecoderFallbackException)
        {
            throw new TextTransformationException("The selected text is not valid Base64.");
        }
    }

    internal static string Base64Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Convert.ToBase64String(Utf8.GetBytes(text));
    }

    internal static string ToLowerInvariant(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.ToLowerInvariant();
    }

    internal static string ToUpperInvariant(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.ToUpperInvariant();
    }

    internal static string UrlDecode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Uri.UnescapeDataString(text);
    }

    internal static string UrlEncode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Uri.EscapeDataString(text);
    }
}

internal sealed class TextTransformationException : Exception
{
    internal TextTransformationException(string message)
        : base(message)
    {
    }
}
