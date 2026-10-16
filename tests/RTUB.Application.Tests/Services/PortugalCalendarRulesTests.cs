using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using RTUB.Application.Factories;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// #226 F02: the rules that used the server's DateTime.Today / Now (UTC on Azure) now follow Portugal's calendar. The
/// failure window was 00:00-01:00 in Portugal in summer (23:00-00:00 UTC, when the server still had yesterday) and, for
/// the "Gerado em" stamps, the whole of summer (one hour behind).
/// </summary>
public class PortugalCalendarRulesTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // 14 July 2026 23:30 UTC = 15 July 00:30 in Portugal (summer); 14 January 23:30 UTC = 23:30 in Portugal (winter).
    private static readonly Clock SummerAfterMidnight = new(new DateTimeOffset(2026, 7, 14, 23, 30, 0, TimeSpan.Zero));
    private static readonly Clock WinterBeforeMidnight = new(new DateTimeOffset(2026, 1, 14, 23, 30, 0, TimeSpan.Zero));

    [Fact]
    public void AnEventThatEndedYesterdayInPortugal_IsPast_AndTakesPrizes()
    {
        var festival = new Event { Date = new DateTime(2026, 7, 14), Type = EventType.Festival };
        EventAdminService.TakesPrizes(festival, PortugalTime.Today(SummerAfterMidnight)).Should().BeTrue("it is already 15 July in Portugal");
        EventAdminService.TakesPrizes(festival, new DateTime(2026, 7, 14)).Should().BeFalse("the server's UTC date would still be the 14th");

        var winter = new Event { Date = new DateTime(2026, 1, 14), Type = EventType.Festival };
        EventAdminService.TakesPrizes(winter, PortugalTime.Today(WinterBeforeMidnight)).Should().BeFalse("still the 14th in winter");
    }

    [Fact]
    public void Rehearsals_ArePastAndApprovable_ByPortugalsDate()
    {
        var yesterday = new Rehearsal { Date = new DateTime(2026, 7, 14) };
        var today = PortugalTime.Today(SummerAfterMidnight);
        RehearsalAgendaService.IsPast(yesterday, today).Should().BeTrue();
        RehearsalAgendaService.IsApprovable(yesterday, today).Should().BeTrue();

        var tonight = new Rehearsal { Date = new DateTime(2026, 1, 14), StartTime = new TimeSpan(21, 30, 0) };
        var winterToday = PortugalTime.Today(WinterBeforeMidnight);
        RehearsalAgendaService.IsPast(tonight, winterToday).Should().BeFalse();
        RehearsalAgendaService.IsApprovable(tonight, winterToday).Should().BeTrue("a 21:30 rehearsal is approvable on its own day");
        RehearsalAgendaService.IsApprovable(new Rehearsal { Date = new DateTime(2026, 1, 14), StartTime = new TimeSpan(18, 0, 0) }, winterToday)
            .Should().BeFalse();
    }

    [Fact]
    public void TheFiscalYear_TurnsOn1SeptemberInPortugal()
    {
        FiscalYearHelper.GetCurrentFiscalYearStartYear(new Clock(new DateTimeOffset(2026, 8, 31, 22, 59, 0, TimeSpan.Zero))).Should().Be(2025);
        FiscalYearHelper.GetCurrentFiscalYearStartYear(new Clock(new DateTimeOffset(2026, 8, 31, 23, 30, 0, TimeSpan.Zero)))
            .Should().Be(2026, "00:30 on 1 September in Portugal");
        FiscalYearHelper.GetCurrentFiscalYearString(new Clock(new DateTimeOffset(2026, 8, 31, 23, 30, 0, TimeSpan.Zero))).Should().Be("2026-2027");
    }

    [Fact]
    public void PushReminders_SayHojeAndAmanha_ByPortugalsDate()
    {
        var factory = new PushNotificationFactory(SummerAfterMidnight);
        factory.CreateRehearsalReminderNotification(new Rehearsal { Id = 1, Date = new DateTime(2026, 7, 15) }, "https://rtub.test")
            .Body.Should().Contain("é hoje");
        factory.CreateRehearsalReminderNotification(new Rehearsal { Id = 2, Date = new DateTime(2026, 7, 16) }, "https://rtub.test")
            .Body.Should().Contain("é amanhã");

        var meeting = new Meeting { Id = 3, Date = new DateTime(2026, 7, 15, 21, 0, 0), Type = MeetingType.AssembleiaGeralOrdinaria, Title = "AG" };
        factory.CreateMeetingNotification(meeting, isReminder: true, "https://rtub.test").Body.Should().Contain("A reunião é hoje");

        new PushNotificationFactory(WinterBeforeMidnight)
            .CreateRehearsalReminderNotification(new Rehearsal { Id = 4, Date = new DateTime(2026, 1, 15) }, "https://rtub.test")
            .Body.Should().Contain("é amanhã");
    }

    [Fact]
    public void PdfStamps_ShowPortugalsWallClock()
    {
        var summerNoon = new Clock(new DateTimeOffset(2026, 7, 14, 11, 0, 0, TimeSpan.Zero));
        var cache = new MemoryCache(new MemoryCacheOptions());
        new AtaPdfService(cache, summerNoon).GeneratedAt().Should().Be("14/07/2026 12:00", "summer: UTC+1, not the server's 11:00");
        new ReportPdfService(cache, summerNoon).GeneratedAt().Should().Be("14/07/2026 12:00");
        new ReportService(Mock.Of<IReportRepository>(), summerNoon).GeneratedAt().Should().Be("14/07/2026 12:00");
        new AtaPdfService(cache, WinterBeforeMidnight).GeneratedAt().Should().Be("14/01/2026 23:30", "winter: UTC+0");
    }
}
