using System.Security.Cryptography;
using System.Text;

namespace RTUB.Core.Helpers.AfterHours;

/// <summary>
/// Family rules. Game Manual v2: at most 4 active members; creating needs level 5 and 1,500 cash;
/// 72 hours after leaving before joining or creating again. <b>AH-007 defaults</b>: the 1,500 comes from
/// the wallet (never the bank), name 3–24 and motto ≤ 120 characters after trimming.
/// </summary>
public static class FamilyRules
{
    public const int MaxActiveMembers = 4;
    public const int CreateLevel = 5;
    public const long CreateCost = 1_500;
    public static readonly TimeSpan LeaveCooldown = TimeSpan.FromHours(72);

    // AH-007 defaults
    public const int NameMinLength = 3;
    public const int NameMaxLength = 24;
    public const int MottoMaxLength = 120;

    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    /// <summary>The trimmed name, or an error.</summary>
    public static string? ValidateName(string? name, out string trimmed)
    {
        trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return "Enter a family name.";
        if (trimmed.Length < NameMinLength || trimmed.Length > NameMaxLength)
            return $"Family names are {NameMinLength}–{NameMaxLength} characters.";
        return null;
    }

    /// <summary>The trimmed motto (null when empty), or an error.</summary>
    public static string? ValidateMotto(string? motto, out string? trimmed)
    {
        var value = motto?.Trim();
        trimmed = string.IsNullOrEmpty(value) ? null : value;
        return trimmed?.Length > MottoMaxLength ? $"Mottos are at most {MottoMaxLength} characters." : null;
    }

    /// <summary>When a user who last left at <paramref name="lastLeftAtUtc"/> may join or create again.</summary>
    public static DateTime? JoinAllowedFromUtc(DateTime? lastLeftAtUtc) => lastLeftAtUtc + LeaveCooldown;

    public static bool InLeaveCooldown(DateTime? lastLeftAtUtc, DateTime utcNow) => utcNow < JoinAllowedFromUtc(lastLeftAtUtc);

    /// <summary>Short stable fingerprint of free text for a receipt request (mottos do not fit in it).</summary>
    public static string TextFingerprint(string? text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty)))[..12];

    public static string CreateRequest(string? name, string? motto) =>
        $"family-create:{NormalizeName(name ?? string.Empty)}:{TextFingerprint(motto?.Trim())}";

    public static string ProfileRequest(string? name, string? motto) =>
        $"family-profile:{NormalizeName(name ?? string.Empty)}:{TextFingerprint(motto?.Trim())}";
}
