using System.Security.Cryptography;
using WinUtil.Models;

namespace WinUtil.Services;

internal static class JwtSecretGenerator
{
    internal static string Generate(WidgetSettings settings)
    {
        var secretBytes = RandomNumberGenerator.GetBytes(settings.Normalize().JwtSecretLengthBytes);

        return Convert.ToBase64String(secretBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
