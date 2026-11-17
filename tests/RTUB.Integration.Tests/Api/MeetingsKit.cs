// MeetingsKit: test class moved here from MeetingsApiTests.cs.
using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using static RTUB.Integration.Tests.Api.MeetingsKit;

namespace RTUB.Integration.Tests.Api;
/// <summary>Personas, seeded rows and the calls the /api/meetings tests share.</summary>
internal sealed partial class MeetingsKit
{
    public const string Ago = nameof(MeetingType.AssembleiaGeralOrdinaria);

    public const string Age = nameof(MeetingType.AssembleiaGeralExtraordinaria);

    public const string Cv = nameof(MeetingType.ConselhoVeteranos);

    public const string Direcao = nameof(MeetingType.ReuniaoDirecao);

    private static int _ip;

    private readonly MeetingsApiFactory _factory;

    public MeetingsKit(MeetingsApiFactory factory)
    {
        _factory = factory;
    }

    // ---------- personas ----------

    /// <summary>A new member signed in through the real login (own name and IP), then given categories, years and positions.</summary>
    public Task<Persona> MemberAsync(params Action<ApplicationUser>[] setup) => SignInAsync(null, setup);

    /// <summary>The same, with a role ("Owner", "Admin", ...) on the session.</summary>
    public Task<Persona> MemberAsync(string role, params Action<ApplicationUser>[] setup) => SignInAsync(role, setup);

    private async Task<Persona> SignInAsync(string? role, Action<ApplicationUser>[] setup)
    {
        var n = Interlocked.Increment(ref _ip);
        var (client, user) = await CookieTestSession.SignInAsync(_factory, $"mtg{Guid.NewGuid():N}"[..20], $"10.64.{n / 250}.{n % 250 + 1}", role);
        if (setup.Length > 0)
        {
            // The service reloads the member on every request, so changes after sign-in apply at once.
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await users.FindByIdAsync(user.Id) ?? throw new InvalidOperationException($"Member {user.Id} was not created.");
            foreach (var change in setup)
            {
                change(stored);
                change(user);
            }

            (await users.UpdateAsync(stored)).Succeeded.Should().BeTrue();
        }

        await AreaHttp.WithTokenAsync(client);
        return new Persona(client, user);
    }

    // ---------- seeded rows ----------

    /// <summary>A meeting at 21:00, <paramref name="days"/> from today (negative = past, 0 = today).</summary>
    public Task<int> MeetingAsync(MeetingType type, int days, string? title = null, string? statement = null,
        string? representativeId = null, bool cancelled = false) =>
        MeetingOnAsync(type, DateTime.Today.AddDays(days).AddHours(21), title, statement, representativeId, cancelled);

    public async Task<int> MeetingOnAsync(MeetingType type, DateTime date, string? title = null, string? statement = null,
        string? representativeId = null, bool cancelled = false)
    {
        var meeting = new Meeting
        {
            Type = type,
            Title = title ?? $"Reunião {Tag()}",
            Date = date,
            Location = "Sede da RTUB",
            Statement = statement ?? "Ordem de trabalhos",
            TunoRepresentativeUserId = representativeId,
            IsCancelled = cancelled,
            CancellationReason = cancelled ? "Motivo interno" : null,
        };
        await SaveAsync(meeting);
        return meeting.Id;
    }

    public async Task<int> AtaAsync(int meetingId, string presidentId, MeetingAtaStatus status = MeetingAtaStatus.Draft,
        string? firstSecretaryId = null, string? secondSecretaryId = null)
    {
        var ata = new MeetingAta
        {
            MeetingId = meetingId,
            AtaNumber = "1/2026",
            ActualStartTime = DateTime.Today.AddDays(-1).AddHours(21),
            Location = "Sede da RTUB",
            PresidentUserId = presidentId,
            FirstSecretaryUserId = firstSecretaryId,
            SecondSecretaryUserId = secondSecretaryId,
            QuorumBasis = "HoraAgendada",
            AttendeesPresent = "[]",
            AttendeesAbsent = "[]",
            Status = status,
            ClosingText = "Nada mais havendo a tratar, encerrou-se a sessão.",
            AgendaPoints = [new MeetingAtaAgendaPoint { PointNumber = 1, Title = "Aprovação das contas", VoteResult = "Aprovado" }],
        };
        await SaveAsync(ata);
        return ata.Id;
    }

    public async Task<int> ParticipationAsync(int meetingId, string userId, bool willAttend, string? notes = null)
    {
        var participation = new MeetingParticipation { MeetingId = meetingId, UserId = userId, WillAttend = willAttend, Notes = notes };
        await SaveAsync(participation);
        return participation.Id;
    }

    public async Task<int> RequestAsync(MeetingType type, string authorId, string? title = null, RequestStatus status = RequestStatus.Pending)
    {
        var request = new MeetingRequest
        {
            RequestedMeetingType = type,
            Title = title ?? $"Pedido {Tag()}",
            ProposedDateTime = DateTime.Today.AddDays(15).AddHours(21),
            Location = "Sede da RTUB",
            Description = "Assuntos a tratar",
            AuthorUserId = authorId,
            Status = status,
        };
        await SaveAsync(request);
        return request.Id;
    }

    public Task<Meeting?> StoredMeetingAsync(int id) =>
        WithDbAsync<Meeting?>(db => db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id));

    public Task<MeetingAta?> StoredAtaAsync(int meetingId) =>
        WithDbAsync<MeetingAta?>(db => db.MeetingAtas.AsNoTracking().FirstOrDefaultAsync(a => a.MeetingId == meetingId));

    public Task<MeetingParticipation?> StoredParticipationAsync(int id) =>
        WithDbAsync<MeetingParticipation?>(db => db.MeetingParticipations.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id));

    public Task<MeetingRequest?> StoredRequestAsync(int id) =>
        WithDbAsync<MeetingRequest?>(db => db.MeetingRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id));

    private Task<int> SaveAsync(object entity) => WithDbAsync(db =>
    {
        db.Add(entity);
        return db.SaveChangesAsync();
    });

    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        return await work(db);
    }

    // ---------- reading answers ----------

    public static Task<JsonElement> BoardAsync(HttpClient client, string query = "fy=all") => AreaHttp.Json(client, $"/api/meetings?{query}");

    public static Task<JsonElement> CardAsync(HttpClient client, int id) => AreaHttp.Json(client, $"/api/meetings/{id}");

    /// <summary>The board's cards, upcoming then past.</summary>
    public static List<JsonElement> Cards(JsonElement board) =>
        board.GetProperty("upcoming").EnumerateArray().Concat(board.GetProperty("past").EnumerateArray()).ToList();

    public static List<int> BoardIds(JsonElement board) => Cards(board).Select(c => c.GetProperty("id").GetInt32()).ToList();

    public static List<int> Ids(JsonElement cards) => cards.EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();

    public static List<string?> Names(JsonElement people) => people.EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();

    /// <summary>One of the card's buttons, as the server decided it.</summary>
    public static bool Can(JsonElement card, string action) => card.GetProperty("can").GetProperty(action).GetBoolean();

    public static bool IsNull(JsonElement element, string property) =>
        !element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null;

    public static async Task<int> CreatedIdAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    /// <summary>Every page of the requests section (all statuses, every year).</summary>
    public static async Task<List<JsonElement>> AllRequestsAsync(HttpClient client)
    {
        var all = new List<JsonElement>();
        for (var page = 1; page <= 100; page++)
        {
            var result = await AreaHttp.Json(client, $"/api/meetings/requests?fy=all&pageSize=20&page={page}");
            var items = result.GetProperty("items").EnumerateArray().ToList();
            all.AddRange(items);
            if (items.Count == 0 || all.Count >= result.GetProperty("total").GetInt32())
            {
                break;
            }
        }

        return all;
    }

    public static JsonElement RequestIn(List<JsonElement> requests, int id) => requests.Single(r => r.GetProperty("id").GetInt32() == id);
}
