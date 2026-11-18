// Rehearsal agenda: rules, mapping and the caller presence context.
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public sealed partial class RehearsalAgendaService
{
    // ---------- rules and mapping (shared with RehearsalAdminService) ----------

    // "Today" is Portugal's calendar date.
    private DateTime Today => PortugalTime.Today(_time);

    internal static bool IsPast(Rehearsal r, DateTime today) => r.Date.Date < today;

    internal static bool IsApprovable(Rehearsal r, DateTime today) => IsPast(r, today) || (r.Date.Date == today && r.StartTime.Hours >= 21);

    internal static string Status(RehearsalAttendance a) => !a.WillAttend ? "notGoing" : a.Attended ? "approved" : "pending";

    internal static RehearsalSummaryDto ToSummary(Rehearsal r, int going, int pending, RehearsalAttendance? mine, DateTime today) => new(
        r.Id,
        DateText(r.Date),
        Time(r.StartTime),
        Time(r.EndTime),
        r.Location,
        Blank(r.Theme),
        Blank(r.Description),
        r.IsCanceled,
        r.IsCanceled ? Blank(r.CancellationReason) : null,
        IsPast(r, today),
        IsApprovable(r, today),
        Season(r.Date),
        going,
        pending,
        mine is null ? null : new RehearsalMineDto(Status(mine),
            mine.WillAttend && mine.Instrument is { } i ? StatusHelper.GetInstrumentDisplay(i) : null, Blank(mine.Notes)));

    /// <summary>Nickname first; Magister, else the category at the rehearsal (else the current one), as the badge.</summary>
    internal static EventAuthorDto Person(RehearsalAttendance a)
    {
        var u = a.User!;
        var who = EventDiscussionBoardService.Author(u.Nickname, u.FirstName, u.LastName, u.ImageUrl, null, null);
        var badge = u.Positions?.Contains(Position.Magister) == true ? "MAGISTER"
            : a.EffectiveCategory() is { } c ? StatusHelper.GetCategoryDisplay(c) : null;
        return who with { Badge = badge };
    }

    private static IReadOnlyList<RehearsalInstrumentCountDto> Counts(IEnumerable<(InstrumentType Instrument, int Count)> counts) =>
        counts.OrderByDescending(c => c.Count).ThenBy(c => c.Instrument)
            .Select(c => new RehearsalInstrumentCountDto(StatusHelper.GetInstrumentDisplay(c.Instrument), c.Count)).ToList();

    internal static string DateText(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Time(TimeSpan t) => t.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    internal static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>September to August.</summary>
    private static string Season(DateTime date)
    {
        var start = date.Month >= 9 ? date.Year : date.Year - 1;
        return $"{start}-{start + 1}";
    }

    // ---------- the caller's presença ----------

    private sealed record Context(
        RehearsalSummaryDto Rehearsal,
        string State,
        RehearsalAttendance? Attendance,
        bool IsLeitao,
        IReadOnlyList<InstrumentType> Registered,
        IReadOnlyList<InstrumentType> Options)
    {
        public RehearsalAttendanceDto ToDto() => new(
            Rehearsal,
            State,
            Attendance is null ? null : Status(Attendance),
            Attendance is { WillAttend: true, Instrument: { } i } ? i.ToString() : null,
            Attendance?.Notes,
            IsLeitao,
            Options.Select(i => new EventInstrumentOptionDto(i.ToString(), StatusHelper.GetInstrumentDisplay(i))).ToList(),
            Registered.Count > 0 ? Registered[0].ToString() : null);
    }

    private async Task<Context?> LoadAsync(int id, string userId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var r = await db.Rehearsals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null)
        {
            return null;
        }

        var attendance = await db.RehearsalAttendances.AsNoTracking().FirstOrDefaultAsync(a => a.RehearsalId == id && a.UserId == userId);
        var going = await db.RehearsalAttendances.CountAsync(a => a.RehearsalId == id && a.WillAttend);
        var categories = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Categories).FirstOrDefaultAsync();
        var isLeitao = categories?.Contains(MemberCategory.Leitao) == true;
        var registered = await db.MemberInstruments.AsNoTracking()
            .Where(i => i.MemberId == userId)
            .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.Id)
            .Select(i => i.InstrumentType)
            .ToListAsync();
        var options = registered.Count > 0 ? registered : isLeitao ? Enum.GetValues<InstrumentType>().ToList() : new List<InstrumentType>();
        var state = r.IsCanceled ? "cancelled" : IsPast(r, Today) ? "past" : "open";
        return new Context(ToSummary(r, going, 0, attendance, Today), state, attendance, isLeitao, registered, options);
    }
}
