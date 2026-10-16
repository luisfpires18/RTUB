using System.Data.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Data;

/// <summary>
/// #220: audit rows are named after the person or object, on real SQLite. Every "act" runs in a fresh context, as the
/// services do (one context per operation), so nothing the name needs is tracked: it must come from the one batched
/// lookup made before the save - which must not run when no name needs it.
/// </summary>
public sealed class AuditEntityNamesTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Commands _commands = new();

    public AuditEntityNamesTests()
    {
        _connection.Open();
        using var db = Context();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task NewsPost_IsNamedByItsTitle_OnCreateEditAndDelete_AndByItsBodyWithoutOne()
    {
        int id;
        await using (var db = Context())
        {
            var post = new NewsPost { Title = "  Arraial 2026  ", Body = "Texto" };
            db.NewsPosts.Add(post);
            await db.SaveChangesAsync();
            id = post.Id;
        }

        await using (var db = Context())
        {
            var post = await db.NewsPosts.SingleAsync(p => p.Id == id);
            post.Title = "Arraial 2027";
            await db.SaveChangesAsync();
            db.NewsPosts.Remove(post);
            await db.SaveChangesAsync();

            var untitled = new NewsPost { Body = "Hoje   ensaiamos\n no Centro Académico, a partir das 21h, com toda a tuna presente." };
            db.NewsPosts.Add(untitled);
            await db.SaveChangesAsync();
        }

        (await Names("NewsPost")).Should().Equal(
            "Created:Arraial 2026", "Modified:Arraial 2027", "Deleted:Arraial 2027",
            "Created:Hoje ensaiamos no Centro Académico, a partir das 21h, com…");
    }

    [Fact]
    public async Task PushSubscription_IsTheMembersNicknameAndDevice_AndItsEndpointAndKeysAreNeverLogged()
    {
        var zé = await MemberAsync("ze", "Zé");
        await using (var db = Context())
        {
            db.PushSubscriptions.Add(new PushSubscription
            {
                UserId = zé, Endpoint = "https://fcm.example/send/secret-endpoint", P256dh = "p256dh-secret-key", Auth = "auth-secret",
                UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X)",
            });
            await db.SaveChangesAsync();
        }

        await using var read = Context();
        var log = await read.AuditLogs.SingleAsync(a => a.EntityType == "PushSubscription");
        log.EntityDisplayName.Should().Be("Zé · iPhone");
        log.EntityDisplayName.Should().NotContain(zé);
        log.Changes.Should().NotContain("secret-endpoint").And.NotContain("p256dh-secret-key").And.NotContain("auth-secret")
            .And.Contain("[redacted]");
    }

    [Fact]
    public async Task EnrollmentAttendanceAndParticipationDeletes_KeepTheMemberAndWhatTheyWereIn()
    {
        var zé = await MemberAsync("ze", "Zé");
        int enrollment, attendance, participation;
        await using (var db = Context())
        {
            var evt = Event.Create("Serenata", new DateTime(2026, 11, 1), "Bragança", EventType.Atuacao);
            var rehearsal = Rehearsal.Create(new DateTime(2026, 11, 3), "Centro Académico");
            var meeting = new Meeting { Type = default, Title = "AG de novembro", Date = new DateTime(2026, 11, 5), Statement = "-" };
            db.AddRange(evt, rehearsal, meeting);
            await db.SaveChangesAsync();
            var e = Enrollment.Create(zé, evt.Id);
            var a = RehearsalAttendance.Create(rehearsal.Id, zé);
            var p = MeetingParticipation.Create(meeting.Id, zé);
            db.AddRange(e, a, p);
            await db.SaveChangesAsync();
            (enrollment, attendance, participation) = (e.Id, a.Id, p.Id);
        }

        await using (var db = Context())
        {
            db.Enrollments.Remove(await db.Enrollments.SingleAsync(x => x.Id == enrollment));
            db.RehearsalAttendances.Remove(await db.RehearsalAttendances.SingleAsync(x => x.Id == attendance));
            db.MeetingParticipations.Remove(await db.MeetingParticipations.SingleAsync(x => x.Id == participation));
            await db.SaveChangesAsync();
        }

        (await Names("Enrollment", "Deleted")).Should().Equal("Deleted:Zé - Serenata - Vai");
        (await Names("RehearsalAttendance", "Deleted")).Should().Equal("Deleted:Zé - 2026-11-03 - Vai");
        (await Names("MeetingParticipation", "Deleted")).Should().Equal("Deleted:Zé - AG de novembro - Vai");
    }

    [Fact]
    public async Task NamesTheTrackerLacks_AreLoadedInOneQueryPerTable_NotOnePerRow()
    {
        var members = new[] { await MemberAsync("a1", "Ana"), await MemberAsync("b2", "Bia"), await MemberAsync("c3", "Cris") };
        var events = new List<int>();
        await using (var db = Context())
        {
            foreach (var name in new[] { "Serenata", "Arraial", "Festival" })
            {
                var evt = Event.Create(name, new DateTime(2026, 12, 1), "Bragança", EventType.Atuacao);
                db.Events.Add(evt);
                await db.SaveChangesAsync();
                events.Add(evt.Id);
            }
        }

        _commands.Sql.Clear();
        await using (var db = Context())
        {
            db.Enrollments.AddRange(members.Select((m, i) => Enrollment.Create(m, events[i])));
            await db.SaveChangesAsync();
        }

        _commands.Selects("AspNetUsers").Should().Be(1, "the three members come in one batched lookup");
        _commands.Selects("Events").Should().Be(1, "and the three events in another");
        (await Names("Enrollment")).Should().BeEquivalentTo("Created:Ana - Serenata - Vai", "Created:Bia - Arraial - Vai", "Created:Cris - Festival - Vai");
    }

    [Fact]
    public async Task NoLookup_WhenNoPendingRowNeedsOne()
    {
        var zé = await MemberAsync("ze", "Zé");

        _commands.Sql.Clear();
        await using (var db = Context())
        {
            db.Albums.Add(Album.Create("Fado ao Luar", 2026));
            await db.SaveChangesAsync();
        }

        _commands.Sql.Should().NotContain(s => s.TrimStart().StartsWith("SELECT"), "an album names itself");

        await using (var db = Context())
        {
            var user = await db.Users.SingleAsync(u => u.Id == zé);
            var evt = Event.Create("Serenata", new DateTime(2026, 11, 1), "Bragança", EventType.Atuacao);
            db.Events.Add(evt);
            await db.SaveChangesAsync();

            _commands.Sql.Clear();
            db.Enrollments.Add(Enrollment.Create(zé, evt.Id));
            await db.SaveChangesAsync();
        }

        _commands.Selects("AspNetUsers").Should().Be(0, "the member and the event are already tracked");
        _commands.Selects("Events").Should().Be(0);
    }

    [Fact]
    public async Task ApplicationUser_IsTheNicknameWithTheUsername()
    {
        var zé = await MemberAsync("ze.pires", "Zé");
        await using (var db = Context())
        {
            var user = await db.Users.SingleAsync(u => u.Id == zé);
            user.City = "Bragança";
            await db.SaveChangesAsync();
        }

        (await Names("ApplicationUser")).Should().Equal("Modified:Zé (ze.pires)");
    }

    [Fact]
    public async Task MemberIdsInNames_BecomeNicknames_AndUnknownOnesStayUnresolved()
    {
        var zé = await MemberAsync("ze", "Zé");
        await using (var db = Context())
        {
            db.RoleAssignments.Add(RoleAssignment.Create(zé, Position.Magister, 2026, 2027));
            db.MemberInstruments.Add(MemberInstrument.Create(zé, InstrumentType.Guitarra));
            await db.SaveChangesAsync();
        }

        var names = await Names("RoleAssignment", "Created");
        names.Should().Equal("Created:Zé - Magister");
        (await Names("MemberInstrument")).Single().Should().StartWith("Created:Zé - ");
    }

    // ---------- helpers ----------

    private ApplicationDbContext Context()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).AddInterceptors(_commands).Options;
        var audit = new AuditContext();
        audit.SetUser("owner", "owner-id");
        return new ApplicationDbContext(options, Mock.Of<IHttpContextAccessor>(), audit, new AuditLogAppender());
    }

    private async Task<string> MemberAsync(string userName, string nickname)
    {
        await using var db = Context();
        var user = new ApplicationUser { UserName = userName, Email = $"{userName}@rtub.test", FirstName = "Nome", LastName = "Apelido", Nickname = nickname };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<List<string>> Names(string entityType, string? action = null)
    {
        await using var db = Context();
        return await db.AuditLogs.Where(a => a.EntityType == entityType && (action == null || a.Action == action))
            .OrderBy(a => a.Id).Select(a => a.Action + ":" + a.EntityDisplayName).ToListAsync();
    }

    /// <summary>Every command EF sends, to count the lookups.</summary>
    private sealed class Commands : DbCommandInterceptor
    {
        public List<string> Sql { get; } = new();

        public int Selects(string table) =>
            Sql.Count(s => s.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && s.Contains($"FROM \"{table}\""));

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    public void Dispose() => _connection.Dispose();
}
