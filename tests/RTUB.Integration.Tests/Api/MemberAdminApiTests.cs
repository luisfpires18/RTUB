using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The member admin tools on /api/members (React track 018, were the Blazor /members/manage) through the real host:
/// real login, antiforgery, Identity and SQLite. Email and push are the recording fakes of <see cref="EventsApiFactory"/>:
/// nothing is ever sent. Every test scopes its own members, since the class shares one database.
/// </summary>
public partial class MemberAdminApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public MemberAdminApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    // ---------- helpers ----------

    private static string Unique() => "m" + Guid.NewGuid().ToString("N")[..10];

    private static MemberInput Valid(string category, string? nickname, string? email = null) => new(
        "Novo", "Membro", nickname, "912345678", email ?? Unique() + "@test.com", null, null, null, category,
        false, false, false, null, null, null, null, null, null, null, null);

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<ApplicationUser> AddAsync(MemberCategory category, int? yearTuno = null, bool retired = false, string? nickname = null)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userName = "adm" + Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName + "@test.com",
            PhoneNumber = "919191919",
            Nickname = nickname ?? userName,
            FirstName = "Mem",
            LastName = "Bro",
            Categories = new List<MemberCategory> { category },
            YearTuno = yearTuno,
            MonthTuno = yearTuno is null ? null : 1,
            IsRetired = retired,
        };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        return user;
    }

    private async Task SetDatesAsync(string id, (int, int?) leitao, (int, int?) caloiro)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(id))!;
        (user.YearLeitao, user.MonthLeitao, user.YearCaloiro, user.MonthCaloiro) = (leitao.Item1, leitao.Item2, caloiro.Item1, caloiro.Item2);
        (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
    }

    private async Task AddMagisterAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var start = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        db.RoleAssignments.Add(RoleAssignment.Create(userId, Position.Magister, start, start + 1));
        await db.SaveChangesAsync();
    }

    private async Task AttendAsync(string userId, DateTime day)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rehearsal = Rehearsal.Create(day, "Centro Académico");
        db.Rehearsals.Add(rehearsal);
        await db.SaveChangesAsync();
        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.WillAttend = true;
        attendance.MarkAttendance(true);
        db.RehearsalAttendances.Add(attendance);
        await db.SaveChangesAsync();
    }

    private async Task<(int RehearsalId, int AttendanceId)> PendingAttendanceAsync(string userId, DateTime day)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rehearsal = Rehearsal.Create(day, "Centro Académico");
        db.Rehearsals.Add(rehearsal);
        await db.SaveChangesAsync();
        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.WillAttend = true;
        db.RehearsalAttendances.Add(attendance);
        await db.SaveChangesAsync();
        return (rehearsal.Id, attendance.Id);
    }

    private async Task<List<AuditLog>> StatusTransitionsAsync(params string[] userIds)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == RTUB.Application.Services.MemberStatusService.AuditEntityType && userIds.Contains(a.UserId!))
            .ToListAsync();
    }

    private async Task<int> ProjectionCountAsync(params string[] userIds)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MemberStatuses.CountAsync(s => userIds.Contains(s.UserId));
    }

    private async Task<ApplicationUser?> UserAsync(string id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
    }

    private async Task<ApplicationUser?> UserByEmailAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
    }

    private async Task<List<MemberInstrument>> InstrumentsAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MemberInstruments.AsNoTracking()
            .Where(i => i.MemberId == userId).ToListAsync();
    }

    private async Task<IList<string>> RolesAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await users.GetRolesAsync((await users.FindByIdAsync(userId))!);
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"adm{Guid.NewGuid():N}"[..20], $"10.83.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous() => AreaHttp.Anonymous(_factory, "10.84.0.1");
}
