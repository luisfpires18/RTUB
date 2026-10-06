using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /member/map behind the React /members/map (task 032, docs/react-member-area.md). Same data and rules:
/// - every account, as before (expelled accounts included; a follow-up decides whether they should be);
/// - grouped by the profile's city text, trimmed and case-insensitive;
/// - a city's coordinates come only from the geocoding cache (<see cref="IGeocodingService"/>): a city not cached yet
///   is queued for the background worker and listed as pending, as before;
/// - City-level only: names and avatars, never an email, a phone, an address or a member's own coordinates.
/// Signed-in members only (Leitões included), as the [Authorize] page was. No schema change.
/// </summary>
public sealed class MemberMapService : IMemberMapService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IGeocodingService _geocoding;

    public MemberMapService(IDbContextFactory<ApplicationDbContext> contexts, IGeocodingService geocoding)
    {
        _contexts = contexts;
        _geocoding = geocoding;
    }

    public async Task<EventResult<MemberMapDto>> GetAsync(ClaimsPrincipal user)
    {
        if (!MembersAuthorization.IsMember(user))
        {
            return EventResult<MemberMapDto>.Fail(EventResultStatus.SignInRequired);
        }

        List<ApplicationUser> users;
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            users = await db.Users.AsNoTracking().ToListAsync();
        }

        var cities = new List<MemberMapCityDto>();
        var pending = new List<string>();
        foreach (var group in users.Where(u => !string.IsNullOrWhiteSpace(u.City)).GroupBy(u => u.City!.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            if (await _geocoding.GetCoordinatesAsync(group.Key) is { } at)
            {
                cities.Add(new MemberMapCityDto(group.Key, at.Latitude, at.Longitude, Ordered(group)));
            }
            else
            {
                pending.Add(group.Key);
            }
        }

        return EventResult<MemberMapDto>.Ok(new MemberMapDto(
            users.Count,
            cities.OrderByDescending(c => c.Members.Count).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            Ordered(users.Where(u => string.IsNullOrWhiteSpace(u.City))),
            pending.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList()));
    }

    private static List<GovernanceMemberDto> Ordered(IEnumerable<ApplicationUser> users) =>
        users
            .Select(u => GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl))
            .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
