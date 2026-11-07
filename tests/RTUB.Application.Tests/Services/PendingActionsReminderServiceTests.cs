using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Factories;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Task 078: the daily "As minhas ações pendentes" push. The page's count and the push service are fakes; the claims
/// use the real table and unique index. Clock: Friday 6 Nov 2026 (winter: Portugal = UTC).
/// </summary>
public class PendingActionsReminderServiceTests
{
    private static readonly DateTime Day = new(2026, 11, 6);
    private readonly DatabaseFixture _db = new();
    private readonly Mock<IPendingActionsService> _pending = new();
    private readonly Mock<IPushNotificationService> _push = new();
    private readonly Dictionary<string, int> _counts = [];

    public PendingActionsReminderServiceTests()
    {
        _pending.Setup(p => p.CountPendingAsync(It.IsAny<string>())).ReturnsAsync((string id) => _counts.GetValueOrDefault(id));
    }

    /// <summary>How many claim saves fail with a write error that is not a duplicate (e.g. the database unavailable).</summary>
    private int _claimFailures;

    private PendingActionsReminderService Service(DateTimeOffset now, int catchUp = 6)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(new Contexts(_db, () => _claimFailures-- > 0));
        services.AddSingleton(_pending.Object);
        services.AddSingleton(_push.Object);
        services.AddSingleton<IPushNotificationFactory, PushNotificationFactory>();
        return new PendingActionsReminderService(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PendingActionsReminderService>.Instance,
            Options.Create(new PendingActionsReminderOptions { CatchUpHours = catchUp }), new FixedClock(now));
    }

    private static readonly DateTimeOffset Evening = new(2026, 11, 6, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OnlyMembersWithSomethingPending_GetOneSummary_EmptyPagesGetNothing_ExpelledAreNotEvaluated()
    {
        var busy = AddUser();
        var idle = AddUser();
        var expelled = AddUser(expelled: true);
        var retired = AddUser(retired: true);
        _counts[busy] = 7; // pending on a later page: the count covers every panel and page
        _counts[expelled] = 3;
        _counts[retired] = 2; // 087: a Reformado is not even evaluated

        var summary = await Service(Evening).RunAsync(Day);

        summary.Should().Be(new PendingActionsReminderService.RunSummary(2, 1, 0, 0));
        _push.Verify(p => p.SendToUserAsync(retired, It.IsAny<SendPushNotificationDto>()), Times.Never);
        _push.Verify(p => p.SendToUserAsync(busy, It.Is<SendPushNotificationDto>(n =>
            n.Title == "As minhas ações pendentes" && n.Url == "/pending-actions" && !n.Body.Contains('7'))), Times.Once);
        _push.Verify(p => p.SendToUserAsync(idle, It.IsAny<SendPushNotificationDto>()), Times.Never);
        _push.Verify(p => p.SendToUserAsync(expelled, It.IsAny<SendPushNotificationDto>()), Times.Never);
    }

    [Fact]
    public async Task ASecondRunTheSameDay_SendsNothing_TheNextDaySendsAgain()
    {
        var busy = AddUser();
        _counts[busy] = 1;

        await Service(Evening).RunAsync(Day);
        var again = await Service(Evening.AddHours(2)).RunAsync(Day); // a restart inside the catch-up window
        await Service(Evening.AddDays(1)).RunAsync(Day.AddDays(1));

        again.Should().Be(new PendingActionsReminderService.RunSummary(1, 0, 1, 0));
        _push.Verify(p => p.SendToUserAsync(busy, It.IsAny<SendPushNotificationDto>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ParallelRuns_SendOnce()
    {
        var busy = AddUser();
        _counts[busy] = 1;

        await Task.WhenAll(Service(Evening).RunAsync(Day), Service(Evening).RunAsync(Day));

        _push.Verify(p => p.SendToUserAsync(busy, It.IsAny<SendPushNotificationDto>()), Times.Once);
    }

    [Fact]
    public async Task ACountThatFails_IsRetried_IsNeverTreatedAsEmptyOrAsPending_AndDoesNotStopOthers()
    {
        var flaky = AddUser();
        var broken = AddUser();
        var fine = AddUser();
        _counts[fine] = 1;
        var calls = 0;
        _pending.Setup(p => p.CountPendingAsync(flaky)).ReturnsAsync(() => ++calls == 1 ? throw new InvalidOperationException("db") : 2);
        _pending.Setup(p => p.CountPendingAsync(broken)).ThrowsAsync(new InvalidOperationException("db"));

        var summary = await Service(Evening).RunAsync(Day);

        summary.Should().Be(new PendingActionsReminderService.RunSummary(3, 2, 0, 1));
        _push.Verify(p => p.SendToUserAsync(flaky, It.IsAny<SendPushNotificationDto>()), Times.Once);
        _push.Verify(p => p.SendToUserAsync(broken, It.IsAny<SendPushNotificationDto>()), Times.Never);
        _pending.Verify(p => p.CountPendingAsync(broken), Times.Exactly(PendingActionsReminderService.Attempts));
        Claims().Should().NotContain(broken);
    }

    [Fact]
    public async Task APushFailure_IsLogged_AndNotRetried_AtMostOnce()
    {
        var busy = AddUser();
        _counts[busy] = 1;
        _push.Setup(p => p.SendToUserAsync(busy, It.IsAny<SendPushNotificationDto>())).ThrowsAsync(new HttpRequestException());

        var first = await Service(Evening).RunAsync(Day);
        await Service(Evening.AddHours(1)).RunAsync(Day);

        first.Failed.Should().Be(1);
        _push.Verify(p => p.SendToUserAsync(busy, It.IsAny<SendPushNotificationDto>()), Times.Once);
    }

    [Fact]
    public async Task AClaimThatCannotBeStored_IsNotADuplicate_ItIsRetried_ThenAFailure_AndNothingIsSentWithoutAClaim()
    {
        var busy = AddUser();
        _counts[busy] = 1;

        _claimFailures = 1; // fails once, then the retry stores it
        var recovered = await Service(Evening).RunAsync(Day);
        recovered.Should().Be(new PendingActionsReminderService.RunSummary(1, 1, 0, 0));

        var other = AddUser();
        _counts[other] = 1;
        _claimFailures = int.MaxValue; // never stored
        var broken = await Service(Evening).RunAsync(Day);

        broken.Should().Be(new PendingActionsReminderService.RunSummary(2, 0, 1, 1), "busy was already sent; other failed, not 'already sent'");
        _push.Verify(p => p.SendToUserAsync(other, It.IsAny<SendPushNotificationDto>()), Times.Never);
        Claims().Should().NotContain(other);
    }

    [Theory]
    [InlineData("2026-11-06T17:59:00Z", null)] // before 18:00
    [InlineData("2026-11-06T18:00:00Z", "2026-11-06")]
    [InlineData("2026-11-06T23:59:00Z", "2026-11-06")] // catch-up after a restart
    [InlineData("2026-11-07T00:30:00Z", null)] // a new day before its time: no backlog of yesterday
    [InlineData("2026-07-01T17:00:00Z", "2026-07-01")] // summer: 18:00 Lisbon = 17:00 UTC
    [InlineData("2026-07-01T16:59:00Z", null)]
    public void DueDay_IsPortugalsDay_OnlyFromTheTimeToTheEndOfTheCatchUpWindow(string utc, string? expected)
    {
        var due = Service(DateTimeOffset.Parse(utc)).DueDay(new TimeSpan(18, 0, 0));

        due?.ToString("yyyy-MM-dd").Should().Be(expected);
        if (expected is null)
        {
            due.Should().BeNull();
        }
    }

    [Fact]
    public async Task TheClaim_HidesNothingOnThePage()
    {
        var busy = AddUser();
        _counts[busy] = 1;
        await Service(Evening).RunAsync(Day);

        using var db = _db.CreateContext();
        db.PendingActionDismissals.Single().ActionKind.Should().Be(PendingActionsReminderService.ClaimKind);
        new[] { GalleryPendingActionSource.KindCode, NewsPendingActionSource.KindCode, RepertoirePendingActionSource.KindCode }
            .Should().NotContain(PendingActionsReminderService.ClaimKind);
    }

    private List<string> Claims()
    {
        using var db = _db.CreateContext();
        return db.PendingActionDismissals.Select(d => d.UserId).ToList();
    }

    private string AddUser(bool expelled = false, bool retired = false)
    {
        using var db = _db.CreateContext();
        var id = Guid.NewGuid().ToString();
        db.Users.Add(new ApplicationUser
        {
            Id = id, UserName = id, Email = $"{id}@test.pt", FirstName = "Ana", LastName = "Silva", Nickname = "Ana", IsExpelled = expelled, IsRetired = retired,
        });
        db.SaveChanges();
        return id;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Contexts(DatabaseFixture db, Func<bool> failClaim) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
        {
            var context = db.CreateContext();
            context.SavingChanges += (_, _) =>
            {
                if (context.ChangeTracker.Entries<PendingActionDismissal>().Any(e => e.State == EntityState.Added) && failClaim())
                {
                    throw new DbUpdateException("disk I/O error");
                }
            };
            return context;
        }
    }
}
