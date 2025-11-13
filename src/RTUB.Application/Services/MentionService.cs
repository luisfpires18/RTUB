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
public class MentionService : IMentionService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private static readonly Regex MentionRegex = new(@"@(\w+)", RegexOptions.Compiled);

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

        foreach (var username in usernames)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user != null)
            {
                mentionedUsers[username] = user.Id;
            }
        }

        if (mentionedUsers.Count == 0)
            return null;

        return JsonSerializer.Serialize(mentionedUsers);
    }

    public async Task<IEnumerable<(string userId, string username, string displayName)>> GetSuggestionsAsync(string query, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<(string, string, string)>();

        var lowerQuery = query.ToLower();
        var users = await _userManager.Users
            .Where(u => u.UserName!.ToLower().Contains(lowerQuery) ||
                       (u.Nickname != null && u.Nickname.ToLower().Contains(lowerQuery)))
            .Take(maxResults)
            .ToListAsync();

        return users.Select(u => (
            u.Id,
            u.UserName ?? string.Empty,
            u.Nickname ?? $"{u.FirstName} {u.LastName}".Trim()
        ));
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

            var displayNames = new Dictionary<string, string>();
            foreach (var (username, userId) in mentions)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    displayNames[username] = user.Nickname ?? $"{user.FirstName} {user.LastName}".Trim();
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
