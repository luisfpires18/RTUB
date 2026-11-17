using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// Pending action: members with missing info.

/// <summary>
/// Membros (task 076; system role Mod, Admin or Owner - the ones who may use the limited member-information edit): every
/// member in scope (not expelled, Leitão / Caloiro / Tuno) with an applicable field still empty (<see cref="MemberInfoRules"/>),
/// counted and paged in SQL. "Completar" opens that member's focused edit on /members; the row leaves only once the field
/// is saved, by anyone.
/// </summary>
public sealed class MembersInfoPendingActionSource : IPendingActionSource
{
    public const string KindCode = "members.info";

    public string Panel => "members";
    public string Title => "Membros";
    public string Category => "responsibilities";
    public IReadOnlyList<(string Key, string Label)> Filters => [];

    /// <summary>Mod or Admin; the Owner is left out on purpose (087, owner decision), even when the account also holds Admin.</summary>
    public bool AppliesTo(PendingActionMember member) =>
        !member.Roles.Contains("Owner") && (member.Roles.Contains("Mod") || member.Roles.Contains("Admin"));

    public Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) => Query(db).CountAsync();

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take)
    {
        var users = await Query(db).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.Id).Skip(skip).Take(take).ToListAsync();
        var ids = users.Select(u => u.Id).ToList();
        var playing = (await db.MemberInstruments.AsNoTracking().Where(i => ids.Contains(i.MemberId)).Select(i => i.MemberId).Distinct().ToListAsync()).ToHashSet();
        return users.Select(u => Row(u, playing.Contains(u.Id))).ToList();
    }

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey)
    {
        if (kind != KindCode || await Query(db).FirstOrDefaultAsync(u => u.Id == sourceKey) is not { } u)
        {
            return null;
        }

        return (Row(u, await db.MemberInstruments.AnyAsync(i => i.MemberId == u.Id)), "");
    }

    /// <summary><see cref="MemberInfoRules.InScope"/> and <see cref="MemberInfoRules.Missing"/>, as SQL.</summary>
    private static IQueryable<ApplicationUser> Query(ApplicationDbContext db) =>
        db.Users.AsNoTracking()
            .Where(u => !u.IsExpelled && (u.Categories.Contains(MemberCategory.Leitao) || u.Categories.Contains(MemberCategory.Caloiro)
                                          || u.Categories.Contains(MemberCategory.Tuno)))
            // Whitespace-only counts as empty, as MemberInfoRules.Missing (trim() in SQL).
            .Where(u => u.City == null || u.City.Trim() == "" || u.PhoneNumber == null || u.PhoneNumber.Trim() == ""
                        || (!u.Categories.Contains(MemberCategory.TunoHonorario) && !db.MemberInstruments.Any(i => i.MemberId == u.Id))
                        || (!u.Categories.Contains(MemberCategory.Fundador) && !u.Categories.Contains(MemberCategory.TunoHonorario)
                            && (u.YearLeitao == null || u.MentorId == null || u.MentorId == ""
                                || ((u.Categories.Contains(MemberCategory.Caloiro) || u.Categories.Contains(MemberCategory.Tuno)) && u.YearCaloiro == null)))
                        || (u.Categories.Contains(MemberCategory.Tuno) && !u.Categories.Contains(MemberCategory.Fundador) && u.YearTuno == null));

    private static PendingActionRowDto Row(ApplicationUser u, bool hasInstrument)
    {
        var who = GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, null);
        var missing = MemberInfoRules.Missing(u, hasInstrument).Select(f => MemberInfoRules.Labels[f]);
        return new PendingActionRowDto(
            KindCode, u.Id, who.DisplayName, null, null, $"Em falta: {string.Join(", ", missing)}",
            Required: false, Dismissible: false,
            [new PendingActionButtonDto("Completar", $"/members?info={Uri.EscapeDataString(u.Id)}", true)],
            $"/members?info={Uri.EscapeDataString(u.Id)}");
    }
}
