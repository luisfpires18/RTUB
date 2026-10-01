using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The React /gallery timeline (React track 009). Reads the existing GalleryMedia and
/// GalleryMediaPersonTags; no schema change. Same rule as the Blazor gallery: IsPrivate items are
/// for signed-in members only (expelled members arrive anonymous: the cookie validator drops them).
/// Person tags are member-only here, a tightening: visitors never learn who is in a photo.
/// Writes stay with the Blazor /member/gallery and <see cref="IGalleryMediaService"/>.
/// </summary>
public sealed class GalleryTimelineService : IGalleryTimelineService
{
    public const int MaxPageSize = 60;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    public GalleryTimelineService(IDbContextFactory<ApplicationDbContext> contexts)
    {
        _contexts = contexts;
    }

    public async Task<GalleryTimelineDto> GetTimelineAsync(ClaimsPrincipal user, GalleryQuery query)
    {
        var isMember = IsMember(user) && !query.PublicOnly;
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        await using var db = await _contexts.CreateDbContextAsync();
        var visible = Visible(db, isMember);

        var years = await visible.Select(m => m.Year).Distinct().OrderByDescending(y => y).ToListAsync();

        var filtered = visible;
        if (query.Year is { } year)
        {
            filtered = filtered.Where(m => m.Year == year);
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLower();
            filtered = filtered.Where(m => m.Title.ToLower().Contains(term));
        }

        if (isMember && !string.IsNullOrEmpty(query.PersonId))
        {
            filtered = filtered.Where(m => m.PeopleInMedia.Any(p => p.UserId == query.PersonId));
        }

        var total = await filtered.CountAsync();

        // Newest first, as the timeline reads; Id breaks every remaining tie so pages never overlap.
        var rows = await filtered
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => m.Month ?? 0)
            .ThenByDescending(m => m.Day ?? 0)
            .ThenByDescending(m => m.TakenAt ?? m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MediaRow(m.Id, m.Title, m.MediaType, m.MediaUrl, m.Year, m.Month, m.Day, m.IsPrivate))
            .ToListAsync();

        var tags = isMember ? await TagsAsync(db, rows.Select(m => m.Id).ToList()) : null;
        var people = isMember ? await TaggedPeopleAsync(db) : Array.Empty<GalleryPersonDto>();

        return new GalleryTimelineDto(
            isMember,
            rows.Select(m => ToItem(m, tags)).ToList(),
            total,
            page,
            pageSize,
            years,
            people);
    }

    public async Task<GalleryItemDto?> GetItemAsync(int id, ClaimsPrincipal user)
    {
        var isMember = IsMember(user);
        await using var db = await _contexts.CreateDbContextAsync();

        var row = await Visible(db, isMember)
            .Where(m => m.Id == id)
            .Select(m => new MediaRow(m.Id, m.Title, m.MediaType, m.MediaUrl, m.Year, m.Month, m.Day, m.IsPrivate))
            .FirstOrDefaultAsync();

        if (row is null)
        {
            return null;
        }

        var tags = isMember ? await TagsAsync(db, new List<int> { id }) : null;
        return ToItem(row, tags);
    }

    private static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    private static IQueryable<GalleryMedia> Visible(ApplicationDbContext db, bool isMember) =>
        isMember ? db.GalleryMedia : db.GalleryMedia.Where(m => !m.IsPrivate);

    private static async Task<Dictionary<int, List<GalleryPersonDto>>> TagsAsync(ApplicationDbContext db, List<int> mediaIds)
    {
        var rows = await db.GalleryMediaPersonTags
            .Where(t => mediaIds.Contains(t.GalleryMediaId))
            .Join(db.Users, t => t.UserId, u => u.Id, (t, u) => new { t.GalleryMediaId, u.Id, u.Nickname, u.FirstName, u.LastName })
            .ToListAsync();

        return rows
            .GroupBy(r => r.GalleryMediaId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new GalleryPersonDto(r.Id, Name(r.Nickname, r.FirstName, r.LastName)))
                    .DistinctBy(p => p.Id)
                    .OrderBy(p => p.Name, StringComparer.Ordinal)
                    .ToList());
    }

    private static async Task<IReadOnlyList<GalleryPersonDto>> TaggedPeopleAsync(ApplicationDbContext db)
    {
        var ids = await db.GalleryMediaPersonTags.Select(t => t.UserId).Distinct().ToListAsync();
        var rows = await db.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName })
            .ToListAsync();

        return rows
            .Select(r => new GalleryPersonDto(r.Id, Name(r.Nickname, r.FirstName, r.LastName)))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static GalleryItemDto ToItem(MediaRow m, Dictionary<int, List<GalleryPersonDto>>? tags) => new(
        m.Id,
        m.Title,
        m.Type == MediaType.Video ? "video" : "image",
        IsSafeUrl(m.Url) ? m.Url : null,
        m.Year,
        m.Month,
        m.Day,
        m.IsPrivate,
        tags is not null && tags.TryGetValue(m.Id, out var people) ? people : Array.Empty<GalleryPersonDto>());

    private static string Name(string? nickname, string? firstName, string? lastName) =>
        !string.IsNullOrWhiteSpace(nickname) ? nickname.Trim() : $"{firstName} {lastName}".Trim();

    /// <summary>An https URL or a same-site path; anything else (http:, javascript:, data:, //host) is dropped.</summary>
    private static bool IsSafeUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && (UrlHelper.IsLocalUrl(url)
            || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps));

    private sealed record MediaRow(int Id, string Title, MediaType Type, string Url, int Year, byte? Month, byte? Day, bool IsPrivate);
}
