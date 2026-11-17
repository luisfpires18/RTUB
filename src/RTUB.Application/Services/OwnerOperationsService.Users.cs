// Owner tools: user list, role change, unlock, member status sync.
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public partial class OwnerOperationsService
{
    // ---------- /users ----------

    public async Task<OwnerUserPage> GetUsersAsync(string? q, int? page, int? pageSize, bool staff = false)
    {
        var size = UserPageSizes.Contains(pageSize ?? 0) ? pageSize!.Value : UserPageSizes[0];
        List<ApplicationUser> users;
        Dictionary<string, string> roles;
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            users = await db.Users.AsNoTracking().ToListAsync();
            roles = await PrimaryRolesAsync(db);
        }

        var search = new SearchHelper<ApplicationUser> { SearchTerm = q?.Trim() ?? string.Empty };
        var filtered = search.FilterMultiple(users,
            [u => u.Id ?? "", u => u.FirstName ?? "", u => u.LastName ?? "", u => u.Nickname ?? "", u => u.UserName ?? "", u => u.Email ?? ""])
            .Where(u => !staff || roles.GetValueOrDefault(u.Id) is "Owner" or "Admin" or "Mod")
            .OrderByDescending(IsOnline)
            .ThenByDescending(u => u.LastLoginDate ?? DateTime.MinValue)
            .ThenBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToList();

        var pages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)size));
        var current = Math.Clamp(page ?? 1, 1, pages);
        var items = filtered.Skip((current - 1) * size).Take(size)
            .Select(u => new OwnerUserCard(u.Id, u.UserName ?? "", u.Nickname ?? "", FullName(u), u.Email, u.ProfilePictureSrc,
                roles.GetValueOrDefault(u.Id, NoRole), IsOnline(u), IsLocked(u), Utc(u.LastLoginDate)))
            .ToList();

        return new OwnerUserPage(items, filtered.Count, current, size);
    }

    public async Task<EventResult<OwnerUserDetail>> GetUserAsync(string id)
    {
        var user = await _users.FindByIdAsync(id);
        return user is null ? EventResult<OwnerUserDetail>.Fail(EventResultStatus.NotFound) : EventResult<OwnerUserDetail>.Ok(await DetailAsync(user));
    }

    public async Task<EventResult<OwnerUserDetail>> ChangeRoleAsync(string id, OwnerRoleInput input)
    {
        var role = Roles.FirstOrDefault(r => string.Equals(r, input.Role?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (role is null)
        {
            return EventResult<OwnerUserDetail>.Invalid("role", "Escolha Member, Mod, Admin ou Owner.");
        }

        var user = await _users.FindByIdAsync(id);
        if (user is null)
        {
            return EventResult<OwnerUserDetail>.Fail(EventResultStatus.NotFound);
        }

        var current = await _users.GetRolesAsync(user);
        if (PrimaryRole(current) == role)
        {
            return EventResult<OwnerUserDetail>.Ok(await DetailAsync(user));
        }

        // As before: the account keeps one role; Owner also gets Admin for the admin features.
        if (current.Count > 0)
        {
            var removed = await _users.RemoveFromRolesAsync(user, current);
            if (!removed.Succeeded)
            {
                return EventResult<OwnerUserDetail>.Invalid("role", $"Erro ao remover funções antigas: {Describe(removed)}");
            }
        }

        var added = await _users.AddToRoleAsync(user, role);
        if (!added.Succeeded)
        {
            return EventResult<OwnerUserDetail>.Invalid("role", $"Erro ao adicionar nova função: {Describe(added)}");
        }

        if (role == "Owner")
        {
            await _users.AddToRoleAsync(user, "Admin");
        }

        // A new security stamp: the role change applies on the account's next request, not at its next sign-in.
        await _users.UpdateSecurityStampAsync(user);
        return EventResult<OwnerUserDetail>.Ok(await DetailAsync(user));
    }

    public async Task<EventResult<OwnerUserDetail>> UnlockAsync(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null)
        {
            return EventResult<OwnerUserDetail>.Fail(EventResultStatus.NotFound);
        }

        var unlocked = await _users.SetLockoutEndDateAsync(user, null);
        if (!unlocked.Succeeded)
        {
            return EventResult<OwnerUserDetail>.Invalid("lockout", $"Erro ao desbloquear conta: {Describe(unlocked)}");
        }

        var reset = await _users.ResetAccessFailedCountAsync(user);
        if (!reset.Succeeded)
        {
            return EventResult<OwnerUserDetail>.Invalid("lockout", $"Erro ao reiniciar tentativas: {Describe(reset)}");
        }

        return EventResult<OwnerUserDetail>.Ok(await DetailAsync(user));
    }

    public async Task<OwnerSyncResult> SyncMemberStatusesAsync()
    {
        var run = await _memberStatus.ReconcileAllMemberStatusesAsync();
        return new OwnerSyncResult(run.Evaluated + run.Excluded, run.Changed, run.Failed);
    }

    private async Task<OwnerUserDetail> DetailAsync(ApplicationUser user) => new(
        user.Id, user.UserName ?? "", user.Nickname ?? "", FullName(user), user.Email, user.ProfilePictureSrc,
        PrimaryRole(await _users.GetRolesAsync(user)), Utc(user.LastLoginDate), user.Subscribed, IsLocked(user),
        IsLocked(user) ? user.LockoutEnd : null, user.RequirePasswordChange);

    private static async Task<Dictionary<string, string>> PrimaryRolesAsync(ApplicationDbContext db)
    {
        var pairs = await (from link in db.UserRoles
                           join role in db.Roles on link.RoleId equals role.Id
                           select new { link.UserId, role.Name }).ToListAsync();
        return pairs.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => PrimaryRole(g.Select(p => p.Name ?? "").ToList()));
    }

    private const string NoRole = "Sem Função";

    private static string PrimaryRole(IList<string> roles) => Roles.FirstOrDefault(roles.Contains) ?? NoRole;

    /// <summary>Signed in within the last hour, as the old card showed it.</summary>
    private static bool IsOnline(ApplicationUser user) =>
        user.LastLoginDate.HasValue && (DateTime.UtcNow - user.LastLoginDate.Value).TotalHours < 1;

    /// <summary>Login dates are stored in UTC; said so, the page shows them in the reader's time.</summary>
    private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    private static bool IsLocked(ApplicationUser user) => user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

    private static string Describe(IdentityResult result) => string.Join(", ", result.Errors.Select(e => e.Description));

    // ---------- shared ----------

    private static OwnerPerson Person(ApplicationUser u) =>
        new(u.Id, string.IsNullOrWhiteSpace(u.Nickname) ? FullName(u) : u.Nickname, FullName(u), u.Email, u.ProfilePictureSrc);

    private static string FullName(ApplicationUser u) => $"{u.FirstName} {u.LastName}".Trim();

    private static string DisplayName(ApplicationUser u) => u.Nickname ?? FullName(u);
}
