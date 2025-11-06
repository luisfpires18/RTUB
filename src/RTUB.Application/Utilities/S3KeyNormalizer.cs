using System.Globalization;
using System.Text;

namespace RTUB.Application.Utilities;

/// <summary>
/// Utility methods for S3 storage operations
/// </summary>
public static class S3KeyNormalizer
{
    /// <summary>
    /// Normalizes a string for use in S3 object keys
    /// Removes accents, converts to lowercase, and replaces special characters with underscores
    /// </summary>
    /// <param name="input">The string to normalize</param>
    /// <returns>Normalized string suitable for S3 keys</returns>
    public static string NormalizeForS3Key(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        
        // Remove accents/diacritics
        var normalizedString = input.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();
        
        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }
        
        var result = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        
        // Convert to lowercase
        result = result.ToLowerInvariant();
        
        // Replace spaces and special characters with underscores
        result = System.Text.RegularExpressions.Regex.Replace(result, @"[^a-z0-9]+", "_");
        
        // Remove leading/trailing underscores
        result = result.Trim('_');
        
        return result;
    }
}
