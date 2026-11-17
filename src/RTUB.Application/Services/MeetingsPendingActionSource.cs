using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// Pending action: presidents' meetings and atas.
// "As minhas ações pendentes" (task 075): the presidents' meetings and atas, and the Ensaiador / Magister's presenças and
// repertoire. Shown by the current position (ApplicationUser.Positions); the writes are the modules' own, which check
// the same positions (MeetingAccess, RehearsalsAuthorization.CanApprove, EventRepertoireAdminService).

/// <summary>
/// Reuniões (current Presidente da Mesa, Magister - the Direção's president - and Presidente do CV): for each body the
/// member presides over, its past, not cancelled meetings of the current fiscal year with no ata saved (required
/// "Criar ata": a saved draft resolves it, publishing is separate; older gaps belong to earlier mandates), then an
/// optional monthly "Registar reunião" (a meeting the app cannot know about; "Agora não" for this month). Someone presiding over several bodies gets the union.
/// </summary>
public sealed class MeetingsPendingActionSource : IPendingActionSource
{
    public const string KindCode = "meetings";

    public string Panel => "meetings";
    public string Title => "Reuniões";
    public string Category => "responsibilities";
    public IReadOnlyList<(string Key, string Label)> Filters => [];

    private static readonly MeetingType[] Bodies = [MeetingType.AssembleiaGeralOrdinaria, MeetingType.ReuniaoDirecao, MeetingType.ConselhoVeteranos];

    public bool AppliesTo(PendingActionMember member) => Bodies.Any(b => member.Holds(MeetingAccess.PresidentOf(b)));

    public async Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) =>
        await Missing(db, member).CountAsync() + (await RegisterRowsAsync(db, member)).Count;

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take)
    {
        var missing = await Missing(db, member).CountAsync();
        var rows = new List<PendingActionRowDto>();
        if (skip < missing)
        {
            var meetings = await Missing(db, member).OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).Skip(skip).Take(take)
                .Select(m => new { m.Id, m.Title, m.Date, m.Type, m.Location }).ToListAsync();
            rows.AddRange(meetings.Select(m => AtaRow(m.Id, m.Title, m.Date, m.Type, m.Location)));
        }

        var register = await RegisterRowsAsync(db, member);
        rows.AddRange(register.Skip(Math.Max(0, skip - missing)).Take(take - rows.Count));
        return rows;
    }

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey)
    {
        if (kind != Kind)
        {
            return null;
        }

        if (sourceKey.StartsWith("ata:", StringComparison.Ordinal) && AttendanceFilters.Id(sourceKey[4..]) is { } id)
        {
            var m = await Missing(db, member).Where(x => x.Id == id).Select(x => new { x.Id, x.Title, x.Date, x.Type, x.Location }).FirstOrDefaultAsync();
            return m is null ? null : (AtaRow(m.Id, m.Title, m.Date, m.Type, m.Location), "");
        }

        var row = (await RegisterRowsAsync(db, member)).FirstOrDefault(r => r.SourceKey == sourceKey);
        return row is null ? null : (row, Month(member));
    }

    private string Kind => KindCode;

    /// <summary>The member's bodies' types (AG covers AG and AGE).</summary>
    private static List<MeetingType> Types(PendingActionMember member) =>
        Enum.GetValues<MeetingType>().Where(t => member.Holds(MeetingAccess.PresidentOf(t))).ToList();

    private static IQueryable<Meeting> Missing(ApplicationDbContext db, PendingActionMember member)
    {
        var types = Types(member);
        var (from, today) = (member.FiscalYearStartDate, member.Today);
        // The current fiscal year only: the mandate in office answers for its own meetings, not every older gap.
        return db.Meetings.AsNoTracking()
            .Where(m => types.Contains(m.Type) && !m.IsCancelled && m.Date >= from && m.Date < today && !db.MeetingAtas.Any(a => a.MeetingId == m.Id));
    }

    private static string Month(PendingActionMember member) => member.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private async Task<List<PendingActionRowDto>> RegisterRowsAsync(ApplicationDbContext db, PendingActionMember member)
    {
        var month = Month(member);
        var dismissed = await db.PendingActionDismissals.AsNoTracking()
            .Where(d => d.UserId == member.UserId && d.ActionKind == KindCode && d.Period == month)
            .Select(d => d.SourceKey).ToListAsync();
        return Bodies.Where(b => member.Holds(MeetingAccess.PresidentOf(b)))
            .Select(b => $"register:{b}")
            .Where(key => !dismissed.Contains(key))
            .Select(key => RegisterRow(Enum.Parse<MeetingType>(key["register:".Length..])))
            .ToList();
    }

    internal static string BodyName(MeetingType type) => type switch
    {
        MeetingType.ConselhoVeteranos => "Conselho de Veteranos",
        MeetingType.ReuniaoDirecao => "Direção",
        _ => "Assembleia Geral",
    };

    private static string TypeName(MeetingType type) => type switch
    {
        MeetingType.ConselhoVeteranos => "Conselho de Veteranos",
        MeetingType.AssembleiaGeralOrdinaria => "Assembleia Geral Ordinária",
        MeetingType.AssembleiaGeralExtraordinaria => "Assembleia Geral Extraordinária",
        MeetingType.ReuniaoDirecao => "Reunião de Direção",
        _ => "Reunião",
    };

    private static PendingActionRowDto AtaRow(int id, string title, DateTime date, MeetingType type, string? location) => new(
        KindCode, $"ata:{id.ToString(CultureInfo.InvariantCulture)}", title, AttendanceFilters.Day(date), AttendanceFilters.Clock(date.TimeOfDay),
        AttendanceFilters.Join(TypeName(type), location, "Sem ata"),
        Required: true, Dismissible: false,
        [new PendingActionButtonDto("Criar ata", $"/meetings?meeting={id}&ata=1", true)],
        $"/meetings?meeting={id}");

    private static PendingActionRowDto RegisterRow(MeetingType body) => new(
        KindCode, $"register:{body}", $"Registar reunião · {BodyName(body)}", null, null,
        "Houve ou vai haver uma reunião que ainda não está na app? Regista-a (uma reunião passada não é anunciada).",
        Required: false, Dismissible: true,
        [new PendingActionButtonDto("Registar reunião", $"/meetings?new={body}", true)]);
}
