using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Mention service implementation for parsing and resolving @mentions
/// </summary>
public partial class MentionService : IMentionService
{
    private readonly UserManager<ApplicationUser> _userManager;

    [GeneratedRegex(@"@(\w+)")]
    private static partial Regex MentionRegexGenerated();

    private static readonly Regex MentionRegex = MentionRegexGenerated();

    public MentionService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<string?> ParseAndResolveAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var matches = MentionRegex.Matches(text);
        if (matches.Count == 0)
            return null;

        var usernames = matches.Select(m => m.Groups[1].Value).Distinct().ToList();
        var mentionedUsers = new Dictionary<string, string>(); // username -> userId

        // Use individual lookups to maintain compatibility with mocked UserManager in tests
        foreach (var username in usernames)
        {
            var user = await _userManager.FindByNameAsync(username).ConfigureAwait(false);
            if (user != null)
            {
                mentionedUsers[username] = user.Id;
            }
        }

        if (mentionedUsers.Count == 0)
            return null;

        return JsonSerializer.Serialize(mentionedUsers);
    }

    public async Task<IEnumerable<(string userId, string username, string displayName, string fullName, string avatarUrl)>> GetSuggestionsAsync(string query, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<(string, string, string, string, string)>();

        try
        {
            var users = await _userManager.Users
                .Where(u => (u.UserName != null && EF.Functions.Like(u.UserName, $"%{query}%")) ||
                           (u.Nickname != null && EF.Functions.Like(u.Nickname, $"%{query}%")))
                .Take(maxResults)
                .ToListAsync()
                .ConfigureAwait(false);

            return users.Select(u => (
                u.Id,
                u.UserName ?? string.Empty,
                u.Nickname ?? $"{u.FirstName} {u.LastName}".Trim(),
                $"{u.FirstName} {u.LastName}".Trim(),
                u.ProfilePictureSrc
            ));
        }
        catch (InvalidOperationException)
        {
            var users = _userManager.Users
                .Where(u => (u.UserName != null && u.UserName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                           (u.Nickname != null && u.Nickname.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .Take(maxResults)
                .ToList();

            return users.Select(u => (
                u.Id,
                u.UserName ?? string.Empty,
                u.Nickname ?? $"{u.FirstName} {u.LastName}".Trim(),
                $"{u.FirstName} {u.LastName}".Trim(),
                u.ProfilePictureSrc
            ));
        }
    }

}
