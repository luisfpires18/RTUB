using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RTUB.Application.Helpers;

/// <summary>
/// Helper class for username normalization
/// </summary>
public static class UsernameHelper
{
    /// <summary>
    /// Normalizes a username by:
    /// - Converting to lowercase
    /// - Removing accents/diacritics
    /// - Removing special characters (keeping only alphanumeric)
    /// - Removing spaces and hyphens
    /// 
    /// Examples:
    /// "Jeans" -> "jeans"
    /// "Saca-Rabos" -> "sacarabos"
    /// "Auto Escópio" -> "autoescopio"
    /// "1/2 kg" -> "12kg"
    /// "22" -> "22"
    /// </summary>
    /// <param name="nickname">The nickname/display name to normalize</param>
    /// <returns>Normalized username suitable for system use</returns>
    public static string NormalizeUsername(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return string.Empty;
        }

        // Convert to lowercase
        var normalized = nickname.ToLowerInvariant();

        // Remove diacritics (accents)
        normalized = RemoveDiacritics(normalized);

        // Remove all characters except alphanumeric
        // This removes spaces, hyphens, slashes, and other special characters
        normalized = Regex.Replace(normalized, @"[^a-z0-9]", string.Empty);

        return normalized;
    }

    /// <summary>
    /// Removes diacritics (accent marks) from characters
    /// </summary>
    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}
