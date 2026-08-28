using WinUtil.Services;

namespace WinUtil.Tests;

internal static class TextTransformationsTests
{
    internal static void Base64EncodeAndDecodeRoundTripUtf8Text()
    {
        var encodedText = TextTransformations.Base64Encode("Grüße");
        var decodedText = TextTransformations.Base64Decode(encodedText);

        TestAssert.Equal("R3LDvMOfZQ==", encodedText);
        TestAssert.Equal("Grüße", decodedText);
    }

    internal static void Base64DecodeRejectsInvalidText()
    {
        try
        {
            TextTransformations.Base64Decode("not Base64");
        }
        catch (TextTransformationException exception)
        {
            TestAssert.Equal("The selected text is not valid Base64.", exception.Message);
            return;
        }

        throw new InvalidOperationException("Expected invalid Base64 to be rejected.");
    }

    internal static void UrlEncodeAndDecodeRoundTripText()
    {
        var encodedText = TextTransformations.UrlEncode("name=Ada Lovelace&role=admin");
        var decodedText = TextTransformations.UrlDecode(encodedText);

        TestAssert.Equal("name%3DAda%20Lovelace%26role%3Dadmin", encodedText);
        TestAssert.Equal("name=Ada Lovelace&role=admin", decodedText);
    }

    internal static void ChangeCaseUsesInvariantCasing()
    {
        TestAssert.Equal("HELLO WORLD", TextTransformations.ToUpperInvariant("Hello world"));
        TestAssert.Equal("hello world", TextTransformations.ToLowerInvariant("Hello WORLD"));
    }
}
