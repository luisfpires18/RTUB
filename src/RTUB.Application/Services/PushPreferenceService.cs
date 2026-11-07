using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// A member's own push preferences (task 082, issue #263): reads and replaces the muted keys of one account, and
/// answers the send path's "who turned this key off". Validation is the catalogue's: unknown keys and the mandatory key
/// are refused (never normalised away, so a stale client learns it). Saving is a whole replace of the account's rows,
/// last write wins; it never touches a device subscription or <see cref="ApplicationUser.PushNotificationsOptedOut"/>.
/// </summary>
public sealed class PushPreferenceService(IDbContextFactory<ApplicationDbContext> contexts, TimeProvider? time = null) : IPushPreferenceStore
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public sealed record State(string Key, string Group, string Label, string? Help, bool Mandatory, bool Enabled);

    public sealed record SaveResult(IReadOnlyList<State>? Items, IReadOnlyDictionary<string, string[]>? Errors);

    /// <summary>Every catalogue item with the member's choice; null when the account no longer exists.</summary>
    public async Task<IReadOnlyList<State>?> GetAsync(string userId)
    {
        await using var db = await contexts.CreateDbContextAsync();
        if (!await db.Users.AnyAsync(u => u.Id == userId))
        {
            return null;
        }

        var muted = await MutedKeysAsync(db, userId);
        return Project(muted);
    }

    /// <summary><paramref name="disabled"/> is the full set of keys the member wants off; everything else goes on.</summary>
    public async Task<SaveResult> SaveAsync(string userId, IEnumerable<string>? disabled)
    {
        var wanted = (disabled ?? []).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var unknown = wanted.Where(k => !PushPreferenceCatalog.IsKnown(k)).ToList();
        if (unknown.Count > 0)
        {
            return new SaveResult(null, new Dictionary<string, string[]> { ["disabled"] = [$"Tipo de notificação desconhecido: {unknown[0]}."] });
        }

        if (wanted.Any(PushPreferenceCatalog.IsMandatory))
        {
            return new SaveResult(null, new Dictionary<string, string[]> { ["disabled"] = ["Este lembrete é obrigatório e não pode ser desligado."] });
        }

        await using var db = await contexts.CreateDbContextAsync();
        if (!await db.Users.AnyAsync(u => u.Id == userId))
        {
            return new SaveResult(null, null);
        }

        var rows = await db.PushPreferences.Where(p => p.UserId == userId).ToListAsync();
        var now = _time.GetUtcNow().UtcDateTime;
        db.PushPreferences.RemoveRange(rows.Where(r => !wanted.Contains(r.Key)));
        foreach (var key in wanted.Where(k => rows.All(r => r.Key != k)))
        {
            db.PushPreferences.Add(new PushPreference { UserId = userId, Key = key, MutedAt = now });
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two tabs saved the same key at once: the row exists, which is what was asked. Re-read instead of failing.
        }

        await using var fresh = await contexts.CreateDbContextAsync();
        return new SaveResult(Project(await MutedKeysAsync(fresh, userId)), null);
    }

    public async Task<IReadOnlySet<string>> MutedUserIdsAsync(string key)
    {
        if (PushPreferenceCatalog.IsMandatory(key))
        {
            return new HashSet<string>();
        }

        await using var db = await contexts.CreateDbContextAsync();
        return (await db.PushPreferences.AsNoTracking().Where(p => p.Key == key).Select(p => p.UserId).ToListAsync()).ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<HashSet<string>> MutedKeysAsync(ApplicationDbContext db, string userId) =>
        (await db.PushPreferences.AsNoTracking().Where(p => p.UserId == userId).Select(p => p.Key).ToListAsync()).ToHashSet(StringComparer.Ordinal);

    private static List<State> Project(HashSet<string> muted) =>
        PushPreferenceCatalog.Items.Select(i => new State(i.Key, i.Group, i.Label, i.Help, i.Mandatory, i.Mandatory || !muted.Contains(i.Key))).ToList();
}
