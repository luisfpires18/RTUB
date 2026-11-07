using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTUB.Application.Data;
using RTUB.Core.Entities;

namespace RTUB.Web.Services;

/// <summary>Task 083 (#264). Bound to "ReleaseNotes".</summary>
public sealed class ReleaseNotesOptions
{
    /// <summary>
    /// A DEV build (2.3.4-dev.N) is not a release: its notes are hidden unless this is on (DEV App Setting
    /// ReleaseNotes__ShowPrerelease=true, to try the dialog before the release). Production never sets it.
    /// </summary>
    public bool ShowPrerelease { get; set; }

    /// <summary>Where releases/*.json live; null = the running build's own copy (AppContext.BaseDirectory/releases).</summary>
    public string? Directory { get; set; }
}

/// <summary>
/// The member-facing release notes (task 083, issue #264; docs/release-and-rollback.md → Release notes). The notes
/// are the <c>releases/*.json</c> files packaged inside this build (RTUB.csproj), read once: the running artifact can
/// only ever show its own notes, never a newer release's, a rolled-back one's or anything fetched at runtime. A
/// member's acknowledgement is a <see cref="PendingActionDismissal"/> row of kind <see cref="AckKind"/> with the version
/// as source (no migration), so it follows the account across devices and never another member on the same device.
/// "Pending" = every bundled version newer than the member's highest acknowledged one and not newer than this build,
/// newest first, at most <see cref="MaxPending"/>. Versions compare as SemVer numbers (2.1.10 &gt; 2.1.9), never as text.
/// A member who never acknowledged anything sees only what is bundled: the launch baseline is the first file.
/// </summary>
public sealed class ReleaseNotesService
{
    public const string AckKind = "release.notes";
    public const int MaxPending = 5;

    public sealed record Note(string Version, string Title, IReadOnlyList<string> Items);

    public sealed record State(string Version, Note? Current, IReadOnlyList<Note> Pending);

    /// <summary>The bundled files, read once per process per directory: the artifact never changes while it runs.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IReadOnlyList<Note>> Bundles = new(StringComparer.OrdinalIgnoreCase);

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly TimeProvider _time;
    private readonly IReadOnlyList<Note> _notes;

    /// <summary>This build's MAJOR.MINOR.PATCH, or null when it is a prerelease whose notes stay hidden.</summary>
    public string? ReleaseVersion { get; }

    public ReleaseNotesService(IDbContextFactory<ApplicationDbContext> contexts, IOptions<ReleaseNotesOptions> options,
        ILogger<ReleaseNotesService> logger, BuildInfo? build = null, TimeProvider? time = null)
    {
        _contexts = contexts;
        _time = time ?? TimeProvider.System;
        var o = options.Value;
        var version = (build ?? BuildInfo.Current).Version;
        var dash = version.IndexOf('-');
        var prerelease = dash >= 0;
        var baseVersion = prerelease ? version[..dash] : version;
        ReleaseVersion = Version.TryParse(baseVersion, out var parsed) && parsed.Build >= 0 && (!prerelease || o.ShowPrerelease) ? baseVersion : null;
        var directory = o.Directory ?? Path.Combine(AppContext.BaseDirectory, "releases");
        _notes = Bundles.GetOrAdd(directory, d => Load(d, logger));
    }

    /// <summary>Every bundled note not newer than this build, newest first.</summary>
    public IReadOnlyList<Note> Bundled => ReleaseVersion is null
        ? []
        : _notes.Where(n => Compare(n.Version, ReleaseVersion) <= 0).OrderByDescending(n => n.Version, SemVer).ToList();

    /// <summary>Null when the account no longer exists.</summary>
    public async Task<State?> GetAsync(string userId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        if (!await db.Users.AnyAsync(u => u.Id == userId))
        {
            return null;
        }

        var acknowledged = await HighestAcknowledgedAsync(db, userId);
        var bundled = Bundled;
        var current = ReleaseVersion is null ? null : bundled.FirstOrDefault(n => n.Version == ReleaseVersion);
        var pending = acknowledged is null ? bundled : bundled.Where(n => Compare(n.Version, acknowledged) > 0).ToList();
        return new State(ReleaseVersion ?? "", current, pending.Take(MaxPending).ToList());
    }

    /// <summary>
    /// Stores that the member saw this build's notes. Only this build's version is accepted (a client can never
    /// acknowledge a release it has not seen); it covers every older pending version too. Returns false when the
    /// account is gone, null when the version is not this build's.
    /// </summary>
    public async Task<bool?> AcknowledgeAsync(string userId, string? version)
    {
        if (ReleaseVersion is null || version != ReleaseVersion)
        {
            return null;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (!await db.Users.AnyAsync(u => u.Id == userId))
        {
            return false;
        }

        if (await db.PendingActionDismissals.AnyAsync(d => d.UserId == userId && d.ActionKind == AckKind && d.SourceKey == version && d.Period == ""))
        {
            return true;
        }

        db.PendingActionDismissals.Add(new PendingActionDismissal
        {
            UserId = userId, ActionKind = AckKind, SourceKey = version, Period = "", DismissedAt = _time.GetUtcNow().UtcDateTime,
        });
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            // Another tab stored the same receipt first: that is what was asked. Anything else is reported.
            await using var check = await _contexts.CreateDbContextAsync();
            if (await check.PendingActionDismissals.AnyAsync(d => d.UserId == userId && d.ActionKind == AckKind && d.SourceKey == version && d.Period == ""))
            {
                return true;
            }

            throw;
        }
    }

    private static async Task<string?> HighestAcknowledgedAsync(ApplicationDbContext db, string userId)
    {
        var versions = await db.PendingActionDismissals.AsNoTracking()
            .Where(d => d.UserId == userId && d.ActionKind == AckKind)
            .Select(d => d.SourceKey)
            .ToListAsync();
        return versions.Where(v => Version.TryParse(v, out _)).OrderByDescending(v => v, SemVer).FirstOrDefault();
    }

    private static readonly IComparer<string> SemVer = Comparer<string>.Create(Compare);

    public static int Compare(string a, string b) => Version.Parse(a).CompareTo(Version.Parse(b));

    private static IReadOnlyList<Note> Load(string directory, ILogger logger)
    {
        if (!System.IO.Directory.Exists(directory))
        {
            logger.LogWarning("No release notes directory at {Directory}", directory);
            return [];
        }

        var notes = new List<Note>();
        foreach (var file in System.IO.Directory.GetFiles(directory, "*.json"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                var version = root.GetProperty("version").GetString();
                var title = root.GetProperty("title").GetString();
                var items = root.GetProperty("items").EnumerateArray().Select(i => i.GetString()).ToList();
                if (version is null || title is null || items.Count == 0 || items.Any(string.IsNullOrWhiteSpace)
                    || Path.GetFileNameWithoutExtension(file) != version || !Version.TryParse(version, out var v) || v.Build < 0)
                {
                    logger.LogWarning("Release notes file {File} is not valid and is ignored", Path.GetFileName(file));
                    continue;
                }

                notes.Add(new Note(version, title, items.Select(i => i!).ToList()));
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IOException)
            {
                logger.LogWarning(ex, "Release notes file {File} could not be read and is ignored", Path.GetFileName(file));
            }
        }

        return notes;
    }
}
