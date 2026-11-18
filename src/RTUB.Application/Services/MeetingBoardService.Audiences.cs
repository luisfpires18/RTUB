using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Meeting board: email and push audiences, sending helpers.
public sealed partial class MeetingBoardService
{
    // ---------- audiences ----------

    private sealed record Audience(List<ApplicationUser> Receiving, List<ApplicationUser> NotReceiving);

    private static bool InCouncilAudience(ApplicationUser u) =>
        u.CurrentRole is MeetingAccess.Veterano or MeetingAccess.Tunossauro || u.Positions?.Contains(Position.Magister) == true;

    private static bool InDirecaoAudience(ApplicationUser u, HashSet<string> admins) =>
        MeetingAccess.HoldsDirecaoPosition(u) || (admins.Contains(u.Id) && u.IsTuno());

    private static IEnumerable<ApplicationUser> ByName(IEnumerable<ApplicationUser> users) => users.OrderBy(u => u.Nickname ?? u.FirstName);

    /// <summary>
    /// The email / cancellation audiences. CV: Veterano / Tunossauro / Magister with email on (not receiving: the plain
    /// Tunos). Direção: Direção positions and Admins with the Tuno category. AG: everyone but Leitões with email on (not
    /// receiving: confirmed addresses with email off; the cancel form also leaves Leitões out of that list).
    /// </summary>
    private async Task<Audience> EmailAudienceAsync(Meeting meeting, List<ApplicationUser> users, bool forCancellation)
    {
        switch (meeting.Type)
        {
            case MeetingType.ConselhoVeteranos:
                return new Audience(
                    ByName(users.Where(u => u.Subscribed && u.EmailConfirmed && InCouncilAudience(u))).ToList(),
                    ByName(users.Where(u => u.EmailConfirmed && u.IsTuno() && u.CurrentRole == MeetingAccess.Tuno
                                            && u.Positions?.Contains(Position.Magister) != true)).ToList());

            case MeetingType.ReuniaoDirecao:
                var admins = await AdminIdsAsync();
                return new Audience(
                    ByName(users.Where(u => u.Subscribed && u.EmailConfirmed && InDirecaoAudience(u, admins))).ToList(),
                    ByName(users.Where(u => u.EmailConfirmed && !u.Subscribed && InDirecaoAudience(u, admins))).ToList());

            default:
                return new Audience(
                    ByName(users.Where(u => u.Subscribed && u.EmailConfirmed && !u.IsLeitao())).ToList(),
                    ByName(users.Where(u => u.EmailConfirmed && !u.Subscribed && (!forCancellation || !u.IsLeitao()))).ToList());
        }
    }

    /// <summary>The push audience: the meeting type's members, split by an active push subscription.</summary>
    private async Task<Audience> PushAudienceAsync(Meeting meeting, List<ApplicationUser> users)
    {
        var subscribed = (await _push.GetSubscribedUserIdsAsync()).ToHashSet();
        IEnumerable<ApplicationUser> eligible = meeting.Type switch
        {
            MeetingType.ConselhoVeteranos => users.Where(InCouncilAudience),
            MeetingType.ReuniaoDirecao => await DirecaoAsync(users),
            _ => users.Where(u => !u.IsLeitao()),
        };

        var ordered = ByName(eligible).ToList();
        return new Audience(ordered.Where(u => subscribed.Contains(u.Id)).ToList(), ordered.Where(u => !subscribed.Contains(u.Id)).ToList());
    }

    private async Task<IEnumerable<ApplicationUser>> DirecaoAsync(List<ApplicationUser> users)
    {
        var admins = await AdminIdsAsync();
        return users.Where(u => InDirecaoAudience(u, admins));
    }

    /// <summary>The Tunos a CV email may add (confirmed address, plain Tuno by time).</summary>
    private static List<ApplicationUser> EmailTunoOptions(Meeting meeting, List<ApplicationUser> users) =>
        meeting.Type == MeetingType.ConselhoVeteranos
            ? ByName(users.Where(u => u.EmailConfirmed && u.CurrentRole == MeetingAccess.Tuno)).ToList()
            : new List<ApplicationUser>();

    /// <summary>The Tunos a CV push may add (plain Tuno by time).</summary>
    private static List<ApplicationUser> PushTunoOptions(Meeting meeting, List<ApplicationUser> users) =>
        meeting.Type == MeetingType.ConselhoVeteranos
            ? ByName(users.Where(u => u.CurrentRole == MeetingAccess.Tuno)).ToList()
            : new List<ApplicationUser>();

    /// <summary>The Tuno representative choices: plain Tunos by time, no Magister.</summary>
    private static IEnumerable<ApplicationUser> TunoRepresentativeCandidates(List<ApplicationUser> users) =>
        ByName(users.Where(u => u.CurrentRole == MeetingAccess.Tuno && u.Positions?.Contains(Position.Magister) != true));

    private async Task<HashSet<string>> AdminIdsAsync()
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var ids = await (from link in db.UserRoles
                         join role in db.Roles on link.RoleId equals role.Id
                         where role.Name == "Admin"
                         select link.UserId).ToListAsync();
        return ids.ToHashSet();
    }

    // ponytail: whole Users table in memory (~100 rows); filter in SQL if it grows.
    private async Task<List<ApplicationUser>> UsersAsync()
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AsNoTracking().ToListAsync();
    }

    private static ApplicationUser? Holder(List<ApplicationUser> users, Position position) =>
        users.FirstOrDefault(u => u.Positions != null && u.Positions.Contains(position));

    // ---------- email helpers ----------

    private static (List<string> Emails, Dictionary<string, (string nickname, string fullName)> Data) Addresses(IEnumerable<ApplicationUser> people)
    {
        var withAddress = people
            .Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .DistinctBy(u => u.Email!, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return (
            withAddress.Select(u => u.Email!).ToList(),
            withAddress.ToDictionary(u => u.Email!, u => (u.Nickname ?? string.Empty, $"{u.FirstName ?? string.Empty} {u.LastName ?? string.Empty}".Trim())));
    }

    /// <summary>Runs one email send; a thrown error or a refusal comes back as text for the dialog.</summary>
    private async Task<(int Count, string? Warning, string? Error)> SendEmailsAsync(
        Func<Task<(bool success, int count, string? errorMessage)>> send, int meetingId)
    {
        try
        {
            var (success, count, error) = await send();
            return success ? (count, error, null) : (0, null, error ?? "Não foi possível enviar os emails.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send meeting emails for meeting {MeetingId}", meetingId);
            return (0, null, "Não foi possível enviar os emails.");
        }
    }

    /// <summary>The sender: for a CV the Presidente do CV (when there is one), otherwise whoever sends, with their first position.</summary>
    private static (string Name, string? Position) Sender(Meeting meeting, ApplicationUser me, IEnumerable<ApplicationUser> users)
    {
        if (meeting.Type == MeetingType.ConselhoVeteranos
            && users.FirstOrDefault(u => u.Positions != null && u.Positions.Contains(Position.PresidenteConselhoVeteranos)) is { } president)
        {
            return ($"{president.FirstName} \"{president.Nickname}\" {president.LastName}",
                PositionHelper.GetDisplayName(Position.PresidenteConselhoVeteranos));
        }

        return ($"{me.FirstName} \"{me.Nickname}\" {me.LastName}",
            me.Positions is { Count: > 0 } positions ? PositionHelper.GetDisplayName(positions[0]) : null);
    }

    private string SenderCity() => $"Bragança, {PortugalTime.Today(_time).ToString("dd 'de' MMMM 'de' yyyy", Portuguese)}";

    // Meeting days are Portugal's calendar.
    private DateTime Today => PortugalTime.Today(_time);

    private static string LongDate(DateTime date) => date.ToString("dddd, dd 'de' MMMM 'de' yyyy • HH:mm", Portuguese);
}
