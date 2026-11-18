using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Factories;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Task 134: the daily calotes reminder reads the fiscal year from its own clock, on Portugal's calendar. The debts
/// and the push service are fakes. Fiscal year 2025-2026 has id 7 here.
/// </summary>
public class CalotesNotificationBackgroundServiceTests
{
    private const int FiscalYear2025Id = 7;
    private readonly Mock<IFiscalYearRepository> _fiscalYears = new();
    private readonly Mock<IMemberDebtService> _debts = new();
    private readonly Mock<IPushNotificationService> _push = new();

    public CalotesNotificationBackgroundServiceTests()
    {
        var year = FiscalYear.Create(2025, 2026);
        year.Id = FiscalYear2025Id;
        _fiscalYears.Setup(f => f.GetByStartYearAsync(2025)).ReturnsAsync(year);
        _debts.Setup(d => d.GetUsersWithDebtsAsync(FiscalYear2025Id))
            .ReturnsAsync(new Dictionary<string, decimal> { ["owes"] = 12.5m, ["paid"] = 0m });
    }

    [Fact]
    public async Task TheReminder_UsesTheFiscalYearOfItsClock_AndSkipsMembersWhoOweNothing()
    {
        // 31 August 2025 23:30 UTC is 1 September 00:30 in Portugal: the first hour of 2025-2026.
        await Service(TestClock.AtUtc(2025, 8, 31, 23, 30)).CheckAndSendCalotesNotificationsAsync(CancellationToken.None);

        _push.Verify(p => p.SendToUserAsync("owes", It.Is<SendPushNotificationDto>(n => n.Url == "/treasury/calotes")), Times.Once);
        _push.Verify(p => p.SendToUserAsync("paid", It.IsAny<SendPushNotificationDto>()), Times.Never);
    }

    [Fact]
    public async Task TheReminder_RunsOncePerPortugalDay()
    {
        var clock = TestClock.AtUtc(2025, 10, 1, 10, 30);
        var service = Service(clock);

        await service.CheckAndSendCalotesNotificationsAsync(CancellationToken.None);
        await service.CheckAndSendCalotesNotificationsAsync(CancellationToken.None);
        clock.Now = clock.Now.AddDays(1);
        await service.CheckAndSendCalotesNotificationsAsync(CancellationToken.None);

        _push.Verify(p => p.SendToUserAsync("owes", It.IsAny<SendPushNotificationDto>()), Times.Exactly(2));
    }

    private CalotesNotificationBackgroundService Service(TimeProvider clock)
    {
        var users = new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        var services = new ServiceCollection();
        services.AddSingleton(_fiscalYears.Object);
        services.AddSingleton(_debts.Object);
        services.AddSingleton(_push.Object);
        services.AddSingleton<IPushNotificationFactory, PushNotificationFactory>();
        services.AddSingleton(users.Object);
        return new CalotesNotificationBackgroundService(NullLogger<CalotesNotificationBackgroundService>.Instance,
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CalotesNotificationOptions()), clock);
    }
}
