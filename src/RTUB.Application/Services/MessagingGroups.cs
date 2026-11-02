using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The premade /messages groups (task 058; v1 synced them, 027 froze them). The groups RTUB made itself carry
/// <c>CreatedByUserId = "system"</c>; members' own groups are never touched. Four are kept and their members follow the
/// rules below, every other premade group (VETERANOS, TUNOSSAUROS, ANUNCIOS, ORGANIZAÇÃO, Órgãos Sociais...) is archived
/// (hidden, its messages kept). Expelled accounts are in none. Runs with every reconciliation of all members: the daily
/// job and the /users "Sincronizar estado da Tuna" button (<see cref="MemberStatusService"/>).
/// </summary>
public static class MessagingGroups
{
    public const string SystemCreator = "system";

    /// <summary>Title → who belongs. Missing groups are created.</summary>
    internal static readonly IReadOnlyList<(string Title, Func<ApplicationUser, bool> Member)> Managed =
    [
        ("GERAL", _ => true),
        ("NO ATIVO", u => !u.IsRetired),
        // As v1: Tunos, without Tunos Honorários.
        ("TUNOS", u => u.Categories.Contains(MemberCategory.Tuno) && !u.Categories.Contains(MemberCategory.TunoHonorario)),
        ("LEITÕES & CALOIROS", u => u.Categories.Contains(MemberCategory.Leitao) || u.Categories.Contains(MemberCategory.Caloiro)),
    ];

    /// <summary>Brings the premade groups in line; returns how many conversations changed.</summary>
    public static async Task<int> SyncAsync(ApplicationDbContext db, DateTime now)
    {
        var users = await db.Users.AsNoTracking().Where(u => !u.IsExpelled).ToListAsync();
        var groups = await db.Conversations
            .Where(c => c.IsGroup && !c.IsSystemConversation && c.CreatedByUserId == SystemCreator)
            .OrderBy(c => c.Id)
            .ToListAsync();

        var changed = 0;
        var kept = new List<Conversation>();
        foreach (var (title, member) in Managed)
        {
            var ids = users.Where(member).Select(u => u.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
            var group = groups.FirstOrDefault(g => string.Equals(g.Title, title, StringComparison.OrdinalIgnoreCase));
            if (group is null)
            {
                group = new Conversation { Title = title, IsGroup = true, CreatedByUserId = SystemCreator, CreatedAt = now, LastMessageAt = now };
                db.Conversations.Add(group);
            }

            kept.Add(group);
            if (group.Id != 0 && !group.IsArchived && group.GetParticipantIds().OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(ids))
            {
                continue;
            }

            group.IsArchived = false;
            group.SetParticipants(ids);
            changed++;
        }

        // Every other premade group, a duplicate of a kept title included.
        foreach (var retired in groups.Where(g => !g.IsArchived && !kept.Contains(g)))
        {
            retired.IsArchived = true;
            changed++;
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync();
        }

        return changed;
    }
}
