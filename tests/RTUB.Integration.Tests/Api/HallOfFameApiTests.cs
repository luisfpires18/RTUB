// HallOfFameApiTests: test class moved here from MemberAreaApiTests.cs.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// The React /hall-of-fame on /api/hall-of-fame (task 032, was the Blazor page): the twelve records with ties, positive
/// durations only, and attended / "vou" activity on past, not-cancelled rehearsals and events.
/// </summary>
public class HallOfFameApiTests : IClassFixture<EventsApiFactory>
{
    private static readonly string[] Keys =
    {
        "ultimo-caloiro", "ultimo-tuno", "mais-afilhados", "mais-cargos", "mais-magister", "mais-instrumentos",
        "mais-tempo-leitao", "menos-tempo-leitao", "mais-tempo-caloiro", "menos-tempo-caloiro", "mais-ensaios", "mais-atuacoes",
    };

    private readonly EventsApiFactory _factory;

    public HallOfFameApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_GetNothing_AndTheHallOfFameIsAReactMembersPage()
    {
        var anonymous = AreaHttp.Anonymous(_factory, "10.50.0.1");
        (await anonymous.GetAsync("/api/hall-of-fame")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var page = await anonymous.GetAsync("/hall-of-fame");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=" + Uri.EscapeDataString("/hall-of-fame"));

        var (member, _) = await SignInAsync();
        (await member.GetStringAsync("/hall-of-fame")).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");
    }

    [Fact]
    public async Task Records_KeepTies_PositiveDurations_AndPastNotCancelledActivity()
    {
        var (member, _) = await SignInAsync();
        var a = await MemberAsync(u => { (u.YearLeitao, u.MonthLeitao, u.YearCaloiro, u.MonthCaloiro) = (1990, 1, 2090, 1); });
        var b = await MemberAsync(u => { (u.YearLeitao, u.MonthLeitao, u.YearCaloiro, u.MonthCaloiro) = (1990, 1, 2090, 1); });
        var oneMonth = await MemberAsync(u => { (u.YearLeitao, u.MonthLeitao, u.YearCaloiro, u.MonthCaloiro) = (2091, 1, 2091, 2); });
        var zero = await MemberAsync(u => { (u.YearLeitao, u.MonthLeitao, u.YearCaloiro, u.MonthCaloiro) = (2092, 5, 2092, 5); });
        var negative = await MemberAsync(u => { (u.YearLeitao, u.MonthLeitao, u.YearCaloiro, u.MonthCaloiro) = (2093, 6, 2093, 1); });
        var latestTuno = await MemberAsync(u => { (u.YearCaloiro, u.MonthCaloiro, u.YearTuno, u.MonthTuno) = (2098, 1, 2099, 12); });

        var mentor = await MemberAsync();
        await MemberAsync(u => u.MentorId = mentor.Id);
        await MemberAsync(u => u.MentorId = mentor.Id);
        await AssignAsync(mentor.Id, Position.Magister, 2001);
        await AssignAsync(mentor.Id, Position.Magister, 2002);
        await AssignAsync(mentor.Id, Position.Magister, 2003);
        var player = await MemberAsync();
        foreach (var type in new[] { InstrumentType.Guitarra, InstrumentType.Bandolim, InstrumentType.Cavaquinho, InstrumentType.Acordeao })
        {
            await InstrumentAsync(player.Id, type);
        }

        var r1 = await MemberAsync();
        var r2 = await MemberAsync();
        foreach (var r in new[] { r1, r2 })
        {
            for (var i = 1; i <= 3; i++)
            {
                await AttendAsync(r.Id, DateTime.UtcNow.AddDays(-i), attended: true);
            }
        }

        await AttendAsync(r1.Id, DateTime.UtcNow.AddDays(-5), attended: true, cancelled: true);
        await AttendAsync(r1.Id, DateTime.UtcNow.AddDays(5), attended: true);
        await AttendAsync(r1.Id, DateTime.UtcNow.AddDays(-6), attended: false);

        var performer = await MemberAsync();
        await EnrollAsync(performer.Id, DateTime.UtcNow.AddDays(-10), willAttend: true);
        await EnrollAsync(performer.Id, DateTime.UtcNow.AddDays(-11), willAttend: true);
        await EnrollAsync(performer.Id, DateTime.UtcNow.AddDays(-12), willAttend: true, cancelled: true);
        await EnrollAsync(performer.Id, DateTime.UtcNow.AddDays(12), willAttend: true);
        await EnrollAsync(performer.Id, DateTime.UtcNow.AddDays(-13), willAttend: false);

        var records = (await AreaHttp.Json(member, "/api/hall-of-fame")).GetProperty("categories").EnumerateArray()
            .ToDictionary(c => c.GetProperty("key").GetString()!);
        records.Keys.Should().Equal(Keys, "the twelve records, in the old page's order");

        Winners(records["mais-tempo-leitao"]).Should().BeEquivalentTo(new[] { a.Nickname, b.Nickname }, "a tie lists every winner");
        records["mais-tempo-leitao"].GetProperty("value").GetString().Should().Be("100 anos");
        Winners(records["menos-tempo-leitao"]).Should().Contain(oneMonth.Nickname).And.NotContain(new[] { zero.Nickname, negative.Nickname },
            "only a positive duration counts");
        records["menos-tempo-leitao"].GetProperty("value").GetString().Should().Be("1 mês");
        Winners(records["ultimo-tuno"]).Should().Equal(latestTuno.Nickname);
        records["ultimo-tuno"].GetProperty("value").GetString().Should().Be("dezembro 2099");
        Winners(records["mais-tempo-caloiro"]).Should().Contain(latestTuno.Nickname);
        records["mais-tempo-caloiro"].GetProperty("value").GetString().Should().Be("1 ano 11 meses");

        Winners(records["mais-afilhados"]).Should().Equal(mentor.Nickname);
        records["mais-afilhados"].GetProperty("value").GetString().Should().Be("2 afilhados");
        Winners(records["mais-magister"]).Should().Equal(mentor.Nickname);
        records["mais-magister"].GetProperty("value").GetString().Should().Be("3 mandatos");
        Winners(records["mais-cargos"]).Should().Equal(mentor.Nickname);
        Winners(records["mais-instrumentos"]).Should().Equal(player.Nickname);
        records["mais-instrumentos"].GetProperty("value").GetString().Should().Be("4 instrumentos");

        Winners(records["mais-ensaios"]).Should().BeEquivalentTo(new[] { r1.Nickname, r2.Nickname },
            "cancelled, future and not-attended rehearsals do not count, so r1 ties r2");
        records["mais-ensaios"].GetProperty("value").GetString().Should().Be("3 ensaios");
        Winners(records["mais-atuacoes"]).Should().Equal(performer.Nickname);
        records["mais-atuacoes"].GetProperty("value").GetString().Should().Be("2 atuações");
    }

    private static List<string?> Winners(JsonElement record) =>
        record.GetProperty("winners").EnumerateArray().Select(w => w.GetProperty("displayName").GetString()).ToList();

    private async Task<ApplicationUser> MemberAsync(Action<ApplicationUser>? setup = null)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var name = $"hof{Guid.NewGuid():N}"[..20];
        var user = new ApplicationUser
        {
            UserName = name,
            Email = $"{name}@test.com",
            EmailConfirmed = true,
            FirstName = "Hall",
            LastName = "Fame",
            Nickname = name,
            PhoneNumber = "912000000",
            Categories = [MemberCategory.Tuno],
        };
        setup?.Invoke(user);
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        return user;
    }

    private async Task AssignAsync(string userId, Position position, int startYear)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.RoleAssignments.Add(RoleAssignment.Create(userId, position, startYear, startYear + 1));
        await db.SaveChangesAsync();
    }

    private async Task InstrumentAsync(string userId, InstrumentType type)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.MemberInstruments.Add(MemberInstrument.Create(userId, type));
        await db.SaveChangesAsync();
    }

    private async Task AttendAsync(string userId, DateTime day, bool attended, bool cancelled = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rehearsal = Rehearsal.Create(day, "Centro Académico");
        rehearsal.IsCanceled = cancelled;
        db.Rehearsals.Add(rehearsal);
        await db.SaveChangesAsync();
        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.WillAttend = true;
        attendance.MarkAttendance(attended);
        db.RehearsalAttendances.Add(attendance);
        await db.SaveChangesAsync();
    }

    private async Task EnrollAsync(string userId, DateTime day, bool willAttend, bool cancelled = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var evt = Event.Create("Atuação do Hall of Fame", day, "Bragança", EventType.Atuacao);
        evt.IsCancelled = cancelled;
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var enrollment = Enrollment.Create(userId, evt.Id);
        enrollment.WillAttend = willAttend;
        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync();
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync()
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"hof{Guid.NewGuid():N}"[..20], $"10.49.{n / 250}.{n % 250 + 1}");
    }
}
