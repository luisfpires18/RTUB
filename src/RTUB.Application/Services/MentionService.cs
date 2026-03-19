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

    public async Task<Dictionary<string, string>> GetDisplayNamesAsync(string? mentionsJson)
    {
        if (string.IsNullOrWhiteSpace(mentionsJson))
            return new Dictionary<string, string>();

        try
        {
            var mentions = JsonSerializer.Deserialize<Dictionary<string, string>>(mentionsJson);
            if (mentions == null || mentions.Count == 0)
                return new Dictionary<string, string>();

            // Batch query: Try to load all mentioned users in a single database query
            var userIds = mentions.Values.ToList();
            Dictionary<string, (string? Nickname, string? FirstName, string? LastName)> userLookup;

            try
            {
                var users = await _userManager.Users
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName })
                    .ToListAsync()
                    .ConfigureAwait(false);

                userLookup = users.ToDictionary(
                    u => u.Id,
                    u => (u.Nickname, u.FirstName, u.LastName));
            }
            catch (InvalidOperationException)
            {
                // Fallback for non-EF scenarios (mocked UserManager in tests)
                userLookup = new Dictionary<string, (string?, string?, string?)>();
                foreach (var userId in userIds)
                {
                    var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
                    if (user != null)
                    {
                        userLookup[userId] = (user.Nickname, user.FirstName, user.LastName);
                    }
                }
            }

            var displayNames = new Dictionary<string, string>();
            foreach (var (username, userId) in mentions)
            {
                if (userLookup.TryGetValue(userId, out var userData))
                {
                    displayNames[username] = userData.Nickname ?? $"{userData.FirstName} {userData.LastName}".Trim();
                }
            }

            return displayNames;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }
}
