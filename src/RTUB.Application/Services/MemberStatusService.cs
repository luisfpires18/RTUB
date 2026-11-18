using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Member activity status. Two strictly separate paths over the same batch-loaded activity and
/// <see cref="MemberActivityStatusEvaluator"/>:
/// <list type="bullet">
/// <item><b>Read</b> (<see cref="GetMemberStatusAsync"/>, <see cref="GetMemberStatusesBatchAsync"/>): calculate and
/// display only. The persisted <see cref="ApplicationUser.IsRetired"/> is shown; progress is computed in memory.
/// No database write, no audit, no notification.</item>
/// <item><b>Reconcile</b> (<see cref="ReconcileMemberStatusAsync"/>, <see cref="ReconcileAllMemberStatusesAsync"/>):
/// calculate, then in one SaveChanges per member write the flag, its <see cref="MemberStatus"/> projection and, on a
/// transition, a "MemberActivityStatus" audit entry. Callers: the background service, the rehearsal attendance and
/// event enrollment triggers, the Owner sync. Idempotent. Only the scheduled background run notifies, and never for a
/// member seen for the first time (no MemberStatus row yet: a new account, the first population).</item>
/// </list>
/// </summary>
public partial class MemberStatusService : IMemberStatusService
{
    public const string AuditEntityType = "MemberActivityStatus";

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly ILogger<MemberStatusService> _logger;
    private readonly MemberStatusUpdateOptions _options;
    private readonly TimeProvider _time;

    public MemberStatusService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IPushNotificationService pushNotificationService,
        IPushNotificationFactory pushNotificationFactory,
        ILogger<MemberStatusService> logger,
        IOptions<MemberStatusUpdateOptions> options,
        TimeProvider time)
    {
        _contextFactory = contextFactory;
        _pushNotificationService = pushNotificationService;
        _pushNotificationFactory = pushNotificationFactory;
        _logger = logger;
        _options = options.Value;
        _time = time;
    }

    // ---------- Read: calculate and display, never write ----------

    public async Task<MemberStatusResult> GetMemberStatusAsync(string userId)
    {
        RequireId(userId);
        return (await ReadAsync(new[] { userId })).GetValueOrDefault(userId) ?? throw new InvalidOperationException($"User '{userId}' not found");
    }

    public async Task<Dictionary<string, MemberStatusResult?>> GetMemberStatusesBatchAsync(IEnumerable<string> userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var results = await ReadAsync(ids);
        return ids.ToDictionary(id => id, id => (MemberStatusResult?)results.GetValueOrDefault(id), StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, MemberStatusResult>> ReadAsync(IReadOnlyCollection<string> ids)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        await using var db = await _contextFactory.CreateDbContextAsync();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync();
        var activity = await LoadActivityAsync(db, ids, now);

        return users.ToDictionary(u => u.Id, u =>
        {
            var userActivity = activity.GetValueOrDefault(u.Id) ?? Activity.None;
            // The persisted flag is what the member is until a reconciliation changes it.
            return ToResult(u.IsRetired, Evaluate(u, userActivity, now), userActivity, now);
        }, StringComparer.Ordinal);
    }
}
