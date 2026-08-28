using System.Security.Cryptography;
using WinUtil.Models;

namespace WinUtil.Services;

internal static class PasswordGenerator
{
    private const string Digits = "23456789";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Symbols = "!@#$-_";
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    internal static string Generate(WidgetSettings settings)
    {
        var normalizedSettings = settings.Normalize();
        var characterSets = GetCharacterSets(normalizedSettings);
        var allCharacters = string.Concat(characterSets);
        var characters = new char[normalizedSettings.PasswordLength];

        for (var index = 0; index < characterSets.Count; index++)
        {
            var characterSet = characterSets[index];
            characters[index] = characterSet[RandomNumberGenerator.GetInt32(characterSet.Length)];
        }

        for (var index = characterSets.Count; index < characters.Length; index++)
        {
            characters[index] = allCharacters[RandomNumberGenerator.GetInt32(allCharacters.Length)];
        }

        Shuffle(characters);

        return new string(characters);
    }

    private static List<string> GetCharacterSets(WidgetSettings settings)
    {
        var characterSets = new List<string>();

        if (settings.PasswordIncludesUppercase)
        {
            characterSets.Add(Uppercase);
        }

        if (settings.PasswordIncludesLowercase)
        {
            characterSets.Add(Lowercase);
        }

        if (settings.PasswordIncludesDigits)
        {
            characterSets.Add(Digits);
        }

        if (settings.PasswordIncludesSymbols)
        {
            characterSets.Add(Symbols);
        }

        return characterSets;
    }

    private static void Shuffle(Span<char> characters)
    {
        for (var index = characters.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[swapIndex]) = (characters[swapIndex], characters[index]);
        }
    }
}
