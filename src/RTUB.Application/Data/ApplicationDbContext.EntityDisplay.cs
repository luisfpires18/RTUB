using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Data;

public partial class ApplicationDbContext
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");

    /// <summary>
    /// Names the audit needs but the change tracker may not hold (#220): a member's nickname, an event's name, a
    /// rehearsal's date... Filled by <see cref="LoadAuditNamesAsync"/> before the save, so a deleted row is named
    /// while it still exists. Keyed by (kind, key).
    /// </summary>
    private Dictionary<(string Kind, string Key), string> _auditNames = new();

    /// <summary>
    /// Resolves a UserId to a user's nickname: the change tracker first, then the batch loaded for this save.
    /// Returns null if the user is not known.
    /// </summary>
    private string? ResolveUserIdToNickname(string? userId) => Name("user", userId);

    private static string Nick(string? nickname, string? userName) => string.IsNullOrWhiteSpace(nickname) ? userName ?? "" : nickname;

    private static string Day(DateTime date) => date.ToString("dd/MM/yyyy", Pt);

    private static string Euros(decimal amount) => amount.ToString("0.00", Pt) + " €";

    /// <summary>Whitespace collapsed, cut at <paramref name="max"/> characters with an ellipsis.</summary>
    private static string Preview(string? text, int max)
    {
        var flat = string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length > max ? WordCut(flat, max) + "…" : flat;
    }

    /// <summary>A name from the change tracker (tracked rows, new ones included) or from this save's batch.</summary>
    private string? Name(string kind, string? key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        var id = int.TryParse(key, out var n) ? n : 0;
        var local = kind switch
        {
            "user" => Users.Local.FirstOrDefault(u => u.Id == key) is { } u ? Nick(u.Nickname, u.UserName) : null,
            "event" => Events.Local.FirstOrDefault(e => e.Id == id)?.Name,
            "rehearsal" => Rehearsals.Local.FirstOrDefault(r => r.Id == id)?.Date.ToString("yyyy-MM-dd"),
            "meeting" => Meetings.Local.FirstOrDefault(m => m.Id == id)?.Title,
            "song" => Songs.Local.FirstOrDefault(s => s.Id == id)?.Title,
            "album" => Albums.Local.FirstOrDefault(a => a.Id == id)?.Title,
            "activity" => Activities.Local.FirstOrDefault(a => a.Id == id)?.Name,
            "product" => Products.Local.FirstOrDefault(p => p.Id == id)?.Name,
            "card" => LogisticsCards.Local.FirstOrDefault(c => c.Id == id)?.Title,
            "question" => Questions.Local.FirstOrDefault(q => q.Id == id)?.Title,
            "naipe" => NaipeContents.Local.FirstOrDefault(c => c.Id == id)?.Title,
            "post" => Posts.Local.FirstOrDefault(p => p.Id == id)?.Title,
            "ata" => MeetingAtas.Local.FirstOrDefault(a => a.Id == id) is { } ata ? AtaName(ata.AtaNumber) : null,
            // Chained names (post → discussion → event, like → comment → target) only from the batch, unless the
            // middle row is tracked with its own parent.
            "postEvent" => Posts.Local.FirstOrDefault(p => p.Id == id) is { } post ? Name("discussionEvent", post.DiscussionId.ToString()) : null,
            "discussionEvent" => Discussions.Local.FirstOrDefault(d => d.Id == id) is { } d ? Name("event", d.EventId.ToString()) : null,
            "lbTarget" => LeaderboardComments.Local.FirstOrDefault(c => c.Id == id) is { } c ? Name("user", c.TargetUserId) : null,
            _ => null,
        };
        return local ?? _auditNames.GetValueOrDefault((kind, key));
    }

    private static string AtaName(string? number) => string.IsNullOrWhiteSpace(number) ? "Ata" : $"Ata n.º {number}";

    /// <summary>The (kind, key) names an entity's display name and its resolved Changes may need.</summary>
    private static IEnumerable<(string Kind, string? Key)> AuditKeys(BaseEntity entity) => entity switch
    {
        Enrollment e => [("user", e.UserId), ("event", e.EventId.ToString())],
        EventRepertoire r => [("event", r.EventId.ToString()), ("song", r.SongId.ToString())],
        RehearsalAttendance a => [("user", a.UserId), ("rehearsal", a.RehearsalId.ToString())],
        MeetingParticipation p => [("user", p.UserId), ("meeting", p.MeetingId.ToString())],
        RoleAssignment r => [("user", r.UserId)],
        SongYouTubeUrl y => [("song", y.SongId.ToString())],
        SongVideo v => [("song", v.SongId.ToString())],
        EventVideo v => [("event", v.EventId.ToString())],
        EventContact c => [("user", c.UserId), ("event", c.EventId.ToString())],
        Transaction t when t.ActivityId.HasValue => [("activity", t.ActivityId.Value.ToString())],
        LeaderboardComment c => [("user", c.AuthorId), ("user", c.TargetUserId)],
        LeaderboardCommentLike l => [("user", l.UserId), ("lbTarget", l.CommentId.ToString())],
        Post p => [("user", p.AuthorId), ("discussionEvent", p.DiscussionId.ToString())],
        Comment c => [("user", c.AuthorId), ("postEvent", c.PostId.ToString())],
        MemberInstrument m => [("user", m.MemberId)],
        PushSubscription s => [("user", s.UserId)],
        Question q => [("user", q.AuthorId), ("user", q.AssignedMemberId)],
        QuestionReply r => [("question", r.QuestionId.ToString())],
        Transportation t => [("post", t.PostId.ToString())],
        TransportationPassenger p => [("user", p.PassengerId)],
        AlbumAccess a => [("user", a.UserId), ("album", a.AlbumId.ToString())],
        MemberDebt d => [("user", d.UserId)],
        ProductReservation r => [("product", r.ProductId.ToString())],
        Trophy t => [("event", t.EventId.ToString())],
        LogisticsCardAssignment a => [("user", a.UserId), ("card", a.CardId.ToString())],
        LogisticsCardReminder r => [("card", r.CardId.ToString())],
        Discussion d => [("event", d.EventId.ToString())],
        MeetingAta a => [("meeting", a.MeetingId.ToString())],
        MeetingAtaAttachment a => [("ata", a.MeetingAtaId.ToString())],
        NaipeComment c => [("user", c.AuthorId), ("naipe", c.NaipeContentId.ToString())],
        _ => [],
    };

    /// <summary>
    /// Before a save (#220): the names the pending audited rows need and the change tracker cannot give, loaded with at
    /// most one batched query per table, and none when every name is already known. Deleted rows are named here, while
    /// they still exist.
    /// </summary>
    private async Task LoadAuditNamesAsync(IEnumerable<EntityEntry<BaseEntity>> entries, CancellationToken cancellationToken)
    {
        _auditNames = new();
        var wanted = entries
            .SelectMany(e => AuditKeys(e.Entity))
            .Where(k => !string.IsNullOrEmpty(k.Key) && Name(k.Kind, k.Key) is null)
            .Select(k => (k.Kind, Key: k.Key!))
            .Distinct()
            .GroupBy(k => k.Kind, k => k.Key)
            .ToList();

        foreach (var group in wanted)
        {
            var keys = group.ToList();
            var ids = keys.Select(k => int.TryParse(k, out var n) ? n : 0).Where(n => n != 0).ToList();
            IEnumerable<(object Key, string? Name)> found = group.Key switch
            {
                "user" => (await Users.AsNoTracking().Where(u => keys.Contains(u.Id))
                        .Select(u => new { u.Id, u.Nickname, u.UserName }).ToListAsync(cancellationToken))
                    .Select(u => ((object)u.Id, (string?)Nick(u.Nickname, u.UserName))),
                "event" => (await Events.AsNoTracking().Where(e => ids.Contains(e.Id)).Select(e => new { e.Id, e.Name }).ToListAsync(cancellationToken))
                    .Select(e => ((object)e.Id, (string?)e.Name)),
                "rehearsal" => (await Rehearsals.AsNoTracking().Where(r => ids.Contains(r.Id)).Select(r => new { r.Id, r.Date }).ToListAsync(cancellationToken))
                    .Select(r => ((object)r.Id, (string?)r.Date.ToString("yyyy-MM-dd"))),
                "meeting" => (await Meetings.AsNoTracking().Where(m => ids.Contains(m.Id)).Select(m => new { m.Id, m.Title }).ToListAsync(cancellationToken))
                    .Select(m => ((object)m.Id, (string?)m.Title)),
                "song" => (await Songs.AsNoTracking().Where(s => ids.Contains(s.Id)).Select(s => new { s.Id, s.Title }).ToListAsync(cancellationToken))
                    .Select(s => ((object)s.Id, (string?)s.Title)),
                "album" => (await Albums.AsNoTracking().Where(a => ids.Contains(a.Id)).Select(a => new { a.Id, a.Title }).ToListAsync(cancellationToken))
                    .Select(a => ((object)a.Id, (string?)a.Title)),
                "activity" => (await Activities.AsNoTracking().Where(a => ids.Contains(a.Id)).Select(a => new { a.Id, a.Name }).ToListAsync(cancellationToken))
                    .Select(a => ((object)a.Id, (string?)a.Name)),
                "product" => (await Products.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.Name }).ToListAsync(cancellationToken))
                    .Select(p => ((object)p.Id, (string?)p.Name)),
                "card" => (await LogisticsCards.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.Title }).ToListAsync(cancellationToken))
                    .Select(c => ((object)c.Id, (string?)c.Title)),
                "question" => (await Questions.AsNoTracking().Where(q => ids.Contains(q.Id)).Select(q => new { q.Id, q.Title }).ToListAsync(cancellationToken))
                    .Select(q => ((object)q.Id, (string?)q.Title)),
                "naipe" => (await NaipeContents.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.Title }).ToListAsync(cancellationToken))
                    .Select(c => ((object)c.Id, (string?)c.Title)),
                "post" => (await Posts.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.Title }).ToListAsync(cancellationToken))
                    .Select(p => ((object)p.Id, (string?)p.Title)),
                "ata" => (await MeetingAtas.AsNoTracking().Where(a => ids.Contains(a.Id)).Select(a => new { a.Id, a.AtaNumber }).ToListAsync(cancellationToken))
                    .Select(a => ((object)a.Id, (string?)AtaName(a.AtaNumber))),
                "postEvent" => (await Posts.AsNoTracking().Where(p => ids.Contains(p.Id))
                        .Select(p => new { p.Id, Name = p.Discussion != null && p.Discussion.Event != null ? p.Discussion.Event.Name : null }).ToListAsync(cancellationToken))
                    .Select(p => ((object)p.Id, p.Name)),
                "discussionEvent" => (await Discussions.AsNoTracking().Where(d => ids.Contains(d.Id))
                        .Select(d => new { d.Id, Name = d.Event != null ? d.Event.Name : null }).ToListAsync(cancellationToken))
                    .Select(d => ((object)d.Id, d.Name)),
                "lbTarget" => (await LeaderboardComments.AsNoTracking().Where(c => ids.Contains(c.Id))
                        .Select(c => new { c.Id, c.TargetUser!.Nickname, c.TargetUser.UserName }).ToListAsync(cancellationToken))
                    .Select(c => ((object)c.Id, (string?)Nick(c.Nickname, c.UserName))),
                _ => [],
            };

            foreach (var (key, name) in found)
            {
                if (!string.IsNullOrEmpty(name))
                    _auditNames[(group.Key, Convert.ToString(key, CultureInfo.InvariantCulture)!)] = name;
            }
        }
    }

    /// <summary>
    /// The human name of an audited row (#220): the person or object first, never a raw id. Uses the change tracker
    /// and the names loaded for this save (<see cref="LoadAuditNamesAsync"/>); no query here. Null when nothing useful
    /// is known; the tracing page then shows the friendly type and the id.
    /// </summary>
    private string? GetEntityDisplayName(EntityEntry entry)
    {
        try
        {
            string? User(string? id) => Name("user", id);
            string Attend(bool willAttend) => willAttend ? "Vai" : "Não vai";
            static string? Join(string separator, params string?[] parts) =>
                parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() is { Count: > 0 } kept ? string.Join(separator, kept) : null;

            return entry.Entity switch
            {
                Song song => song.Title,
                Event evt => evt.Name,
                Album album => album.Title,
                Activity activity => activity.Name,
                Instrument instrument => $"{instrument.Category} - {instrument.Name}",
                Product product => product.Name,
                Report report => report.Title,
                Question question => question.Title,
                LogisticsBoard board => board.Name,
                LogisticsList list => list.Name,
                LogisticsCard card => card.Title,
                Meeting meeting => meeting.Title,
                MeetingRequest meetingRequest => meetingRequest.Title,
                MbwayTransfer mbway => $"MBWAY - {mbway.Date:dd/MM/yyyy} €{mbway.Amount:F2}",
                NerbaOrder nerba => $"Nerba - {nerba.Item} (x{nerba.Stock})",
                FiscalYear year => $"{year.StartYear}-{year.EndYear}",
                GeocodingCache place => Join(", ", place.CityName, place.CountryCode),

                Enrollment e => Join(" - ", User(e.UserId), e.Event?.Name ?? Name("event", e.EventId.ToString())) is { } who
                    ? $"{who} - {Attend(e.WillAttend)}" : null,
                EventRepertoire r => Join(" - ", r.Event?.Name ?? Name("event", r.EventId.ToString()), r.Song?.Title ?? Name("song", r.SongId.ToString())),
                RehearsalAttendance a => Join(" - ", User(a.UserId), a.Rehearsal?.Date.ToString("yyyy-MM-dd") ?? Name("rehearsal", a.RehearsalId.ToString())) is { } who
                    ? $"{who} - {Attend(a.WillAttend)}" : null,
                MeetingParticipation p => Join(" - ", User(p.UserId), p.Meeting?.Title ?? Name("meeting", p.MeetingId.ToString())) is { } who
                    ? $"{who} - {Attend(p.WillAttend)}" : null,
                EventContact c => Join(" - ", User(c.UserId), Name("event", c.EventId.ToString())),
                RoleAssignment r => Join(" - ", User(r.UserId), r.Position.ToString()),
                MemberInstrument m => Join(" - ", User(m.MemberId), InstrumentTypeHelper.GetDisplayName(m.InstrumentType)),
                AlbumAccess a => Join(" → ", User(a.UserId), Name("album", a.AlbumId.ToString())),
                MemberDebt d => Join(" · ", User(d.UserId), Euros(d.AmountOwed)),
                LogisticsCardAssignment a => Join(" → ", User(a.UserId), Name("card", a.CardId.ToString())),
                LogisticsCardReminder r => Join(" · ", "Lembrete", Name("card", r.CardId.ToString())),
                ProductReservation r => Join(" → ", r.UserNickname, Name("product", r.ProductId.ToString()))
                    + (string.IsNullOrWhiteSpace(r.Size) ? "" : $" ({r.Size})"),
                PushSubscription s => Join(" · ", User(s.UserId), string.IsNullOrWhiteSpace(s.UserAgent) ? null : UserAgentHelper.GetShortUserAgent(s.UserAgent)),
                TransportationPassenger p => Join(": ", "Passageiro", User(p.PassengerId)),
                Transportation t => Name("post", t.PostId.ToString()) is { } post ? $"{t.VehicleDescription} ({post})" : t.VehicleDescription,

                Rehearsal r => Join(" · ", $"Ensaio {Day(r.Date)}", r.Theme),
                Transaction t => Join(" · ", t.Description, (t.Type == "Income" ? "+" : "-") + Euros(t.Amount),
                    t.ActivityId.HasValue ? t.Activity?.Name ?? Name("activity", t.ActivityId.Value.ToString()) : null),
                SongYouTubeUrl y => Join(" · ", y.Song?.Title ?? Name("song", y.SongId.ToString()), "YouTube"),
                SongVideo v => $"{(string.IsNullOrWhiteSpace(v.Title) ? "Vídeo" : v.Title)}"
                    + (Name("song", v.SongId.ToString()) is { } song ? $" ({song})" : ""),
                EventVideo v => $"{(string.IsNullOrWhiteSpace(v.Title) ? "Vídeo" : v.Title)}"
                    + (Name("event", v.EventId.ToString()) is { } evt ? $" ({evt})" : ""),
                Trophy t => Join(" · ", t.Name, Name("event", t.EventId.ToString())),
                Discussion d => Join(" · ", "Discussão", Name("event", d.EventId.ToString())),
                MeetingAta a => Join(" · ", AtaName(a.AtaNumber), Name("meeting", a.MeetingId.ToString())),
                MeetingAtaAttachment a => Join(" · ", a.Name, Name("ata", a.MeetingAtaId.ToString())),
                GalleryMedia m => string.IsNullOrWhiteSpace(m.Title)
                    ? $"{(m.MediaType == MediaType.Video ? "Vídeo" : "Foto")} {GalleryDate(m)}"
                    : m.Title,
                NewsPost n => string.IsNullOrWhiteSpace(n.Title) ? Preview(n.Body, 60) is { Length: > 0 } body ? body : null : n.Title.Trim(),
                QuestionReply r => Join(" · ", "Resposta", Name("question", r.QuestionId.ToString())),
                NaipeContent c => $"{c.Title} ({InstrumentTypeHelper.GetDisplayName(c.InstrumentType)})",
                NaipeComment c => Join(" · ", User(c.AuthorId), Name("naipe", c.NaipeContentId.ToString())),
                NaipeTypeConfig c => $"Naipe · {InstrumentTypeHelper.GetDisplayName(c.InstrumentType)}",
                // An outside person's request: the kind of event and the date; their name and contacts stay in Changes.
                Request r => Join(" · ", "Pedido", r.EventType, Day(r.PreferredDate)),

                LeaderboardComment c => Join(" → ", User(c.AuthorId), User(c.TargetUserId)),
                LeaderboardCommentLike l => Name("lbTarget", l.CommentId.ToString()) is { } target
                    ? $"{User(l.UserId) ?? "Alguém"} gostou do comentário a {target}"
                    : Join(" ", User(l.UserId), "gostou de um comentário"),
                Post p => Join(" - ", Name("discussionEvent", p.DiscussionId.ToString()), p.Title),
                Comment c => Join(" - ", Name("postEvent", c.PostId.ToString()), Join(": ", User(c.AuthorId), Preview(c.Body, 50))),
                _ => null,
            };
        }
        catch (InvalidOperationException)
        {
            // Never fail a save over a name: the row keeps a null name and the page shows the type and the id.
            return null;
        }
    }

    private static string GalleryDate(GalleryMedia m) =>
        m.Month is { } month ? m.Day is { } day ? $"{day:00}/{month:00}/{m.Year}" : $"{month:00}/{m.Year}" : m.Year.ToString();

    /// <summary>The first <paramref name="max"/> characters, back to the last whole word when one ends past half.</summary>
    private static string WordCut(string text, int max)
    {
        var cut = text[..max];
        var space = cut.LastIndexOf(' ');
        return (space > max / 2 ? cut[..space] : cut).TrimEnd(' ', ',', ';', ':', '.');
    }
}
