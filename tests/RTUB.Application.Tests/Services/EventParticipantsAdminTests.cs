using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Admin/Owner management of other members' answers behind the React event page (React track 012E),
/// through the real EnrollmentService on the test database. Push is a recording fake: adding someone by
/// hand must not notify anyone, as on the old page.
/// </summary>
public class EventParticipantsAdminTests
{
    private static readonly ClaimsPrincipal Visitor = new(new ClaimsIdentity());
    private static readonly ClaimsPrincipal Member = SignedIn("Member");
    private static readonly ClaimsPrincipal Mod = SignedIn("Mod");
    private static readonly ClaimsPrincipal Admin = SignedIn("Admin");
    private static readonly ClaimsPrincipal Owner = SignedIn("Owner");

    private readonly DatabaseFixture _db = new();
    private readonly Mock<IPushNotificationService> _push = new();
    private readonly Mock<IMemberInstrumentService> _instruments = new();
    private readonly EventParticipantsAdminService _service;
    private readonly EventAgendaService _agenda;

    public EventParticipantsAdminTests()
    {
        var contexts = new Contexts(_db);
        var users = new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        var enrollments = new EnrollmentService(new EnrollmentRepository(contexts), Mock.Of<IRetirementStatusService>(), Mock.Of<IPushNotificationFactory>(),
            _push.Object, Mock.Of<IHttpContextAccessor>(), users.Object);
        _service = new EventParticipantsAdminService(contexts, enrollments, _instruments.Object);
        _agenda = new EventAgendaService(contexts, enrollments, Mock.Of<IEventService>(), Mock.Of<IPushNotificationFactory>(),
            Mock.Of<IPushNotificationService>(), Mock.Of<IAuditLogService>(), NullLogger<EventAgendaService>.Instance);
    }

    // ---------- who may ----------

    public static TheoryData<string, EventResultStatus> Refused => new()
    {
        { "visitor", EventResultStatus.SignInRequired },
        { "member", EventResultStatus.Forbidden },
        { "mod", EventResultStatus.Forbidden },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task OnlyAdminOrOwner_MayListSearchAddOrRemoveOthersAnswers(string who, EventResultStatus expected)
    {
        var user = who switch { "visitor" => Visitor, "member" => Member, _ => Mod };
        var id = AddEvent(DateTime.Today.AddDays(10));
        var someone = AddUser("Zé");
        var answer = AddEnrollment(id, someone, going: true);
        var other = AddUser("Rui");

        (await _service.GetAsync(id, user)).Status.Should().Be(expected);
        (await _service.SearchMembersAsync(id, "rui", user)).Status.Should().Be(expected);
        (await _service.AddAsync(id, new EventEnrollmentAddInput(other), user)).Status.Should().Be(expected);
        (await _service.RemoveAsync(id, answer, user)).Status.Should().Be(expected);

        (await AnswersAsync(id)).Should().ContainSingle();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminOrOwner_AddAMemberAsGoing_WithTheirPrimaryInstrument_AndNoNotification(string role)
    {
        var id = AddEvent(DateTime.Today.AddDays(-5));
        var rui = AddUser("Rui", category: MemberCategory.Tuno);
        _instruments.Setup(i => i.GetPrimaryInstrumentAsync(rui)).ReturnsAsync(MemberInstrument.Create(rui, InstrumentType.Bandolim, isPrimary: true));

        var result = await _service.AddAsync(id, new EventEnrollmentAddInput(rui), role == "Owner" ? Owner : Admin);

        result.Status.Should().Be(EventResultStatus.Ok);
        var added = result.Value!.Going.Single();
        added.Participant.Name.Should().Be("Rui");
        added.Participant.Instrument.Should().Be(StatusHelperDisplay(InstrumentType.Bandolim));
        var stored = (await AnswersAsync(id)).Single();
        stored.WillAttend.Should().BeTrue();
        stored.Instrument.Should().Be(InstrumentType.Bandolim);
        _push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AMemberWithoutInstruments_IsAddedWithNone()
    {
        var id = AddEvent(DateTime.Today.AddDays(-5));
        var rui = AddUser("Rui");

        (await _service.AddAsync(id, new EventEnrollmentAddInput(rui), Admin)).Status.Should().Be(EventResultStatus.Ok);
        (await AnswersAsync(id)).Single().Instrument.Should().BeNull();
    }

    [Fact]
    public async Task Remove_TakesAnyAnswerOut_AndMembersSeeItGone()
    {
        var id = AddEvent(DateTime.Today.AddDays(10));
        var going = AddEnrollment(id, AddUser("Zé"), going: true);
        AddEnrollment(id, AddUser("Ana"), going: false);

        var result = (await _service.RemoveAsync(id, going, Admin)).Value!;

        result.Going.Should().BeEmpty();
        result.NotGoing.Should().ContainSingle();
        (await _agenda.GetEventAsync(id, Member))!.Member!.Participants.Going.Should().BeEmpty("\"Quem vai\" follows");
    }

    // ---------- rules ----------

    [Fact]
    public async Task TheList_GroupsAsQuemVai_GoingLeitoesAndNotGoing()
    {
        var id = AddEvent(DateTime.Today.AddDays(10));
        AddEnrollment(id, AddUser("Tuno"), going: true);
        AddEnrollment(id, AddUser("Leitão", category: MemberCategory.Leitao), going: true);
        AddEnrollment(id, AddUser("Ausente"), going: false);

        var list = (await _service.GetAsync(id, Admin)).Value!;

        list.Going.Select(g => g.Participant.Name).Should().Equal("Tuno");
        list.Leitoes.Select(g => g.Participant.Name).Should().Equal("Leitão");
        list.NotGoing.Select(g => g.Participant.Name).Should().Equal("Ausente");
        list.CanAdd.Should().BeTrue();
    }

    [Fact]
    public async Task ADuplicate_IsRefused_NotOverwritten()
    {
        var id = AddEvent(DateTime.Today.AddDays(10));
        var ana = AddUser("Ana");
        AddEnrollment(id, ana, going: false);

        (await _service.AddAsync(id, new EventEnrollmentAddInput(ana), Admin)).Errors!.Keys.Should().Contain("userId");
        (await AnswersAsync(id)).Single().WillAttend.Should().BeFalse("her own answer stays");
    }

    [Fact]
    public async Task UnknownOrExpelledMembers_AreRefused_AndNotOffered()
    {
        var id = AddEvent(DateTime.Today.AddDays(10));
        var expelled = AddUser("Expulso", expelled: true);
        AddUser("Expedito");

        (await _service.AddAsync(id, new EventEnrollmentAddInput(expelled), Admin)).Errors!.Keys.Should().Contain("userId");
        (await _service.AddAsync(id, new EventEnrollmentAddInput("nobody"), Admin)).Errors!.Keys.Should().Contain("userId");
        (await _service.SearchMembersAsync(id, "exp", Admin)).Value!.Select(m => m.Name).Should().Equal("Expedito");
    }

    [Fact]
    public async Task ACancelledEvent_TakesNoNewAnswers()
    {
        var id = AddEvent(DateTime.Today.AddDays(10), cancelled: true);

        (await _service.AddAsync(id, new EventEnrollmentAddInput(AddUser("Rui")), Admin)).Status.Should().Be(EventResultStatus.Closed);
        (await _service.GetAsync(id, Admin)).Value!.CanAdd.Should().BeFalse();
    }

    [Fact]
    public async Task AnAnswer_IsOnlyReachedThroughItsOwnEvent()
    {
        var mine = AddEvent(DateTime.Today.AddDays(10));
        var other = AddEvent(DateTime.Today.AddDays(11));
        var theirs = AddEnrollment(other, AddUser("Zé"), going: true);

        (await _service.RemoveAsync(mine, theirs, Admin)).Status.Should().Be(EventResultStatus.NotFound);
        (await _service.GetAsync(999_999, Admin)).Status.Should().Be(EventResultStatus.NotFound);
        (await AnswersAsync(other)).Should().ContainSingle();
    }

    // ---------- the member picker ----------

    [Fact]
    public async Task Search_MatchesNicknameOrFullName_IgnoringAccents_AndSkipsWhoAnswered()
    {
        var id = AddEvent(DateTime.Today.AddDays(10));
        AddUser("Joãozinho", first: "João", last: "Silva");
        AddUser("Gaita", first: "Joana", last: "Pires");
        AddEnrollment(id, AddUser("Já foi", first: "Joaquim", last: "Já"), going: true);

        var found = (await _service.SearchMembersAsync(id, "JOA", Admin)).Value!;

        found.Select(m => m.Name).Should().Equal("Gaita", "Joãozinho");
        found.Should().OnlyContain(m => m.AvatarUrl.Length > 0);
        (await _service.SearchMembersAsync(id, "  ", Admin)).Value!.Should().BeEmpty("nothing is listed before a name is typed");
    }

    [Fact]
    public void ThePickerAndListContracts_CarryNoEmailOrPhone()
    {
        typeof(EventMemberOptionDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Id", "Name", "FullName", "AvatarUrl");
        typeof(EventManagedEnrollmentDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Id", "Participant");
        typeof(EventParticipantDto).GetProperties().Select(p => p.Name).Should()
            .NotContain(new[] { "Email", "PhoneNumber", "UserId" });
    }

    // ---------- helpers ----------

    private static string StatusHelperDisplay(InstrumentType i) => RTUB.Application.Helpers.StatusHelper.GetInstrumentDisplay(i);

    private static ClaimsPrincipal SignedIn(string role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-" + role),
            new Claim(ClaimTypes.Name, "u-" + role),
            new Claim(ClaimTypes.Role, role),
        }, "test"));

    private int AddEvent(DateTime date, bool cancelled = false)
    {
        using var db = _db.CreateContext();
        var e = Event.Create("Serenata", date, "Bragança", EventType.Serenata, "");
        if (cancelled)
        {
            e.Cancel("Chuva");
        }

        db.Events.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    private string AddUser(string nickname, string first = "Nome", string last = "Apelido", MemberCategory category = MemberCategory.Tuno, bool expelled = false)
    {
        using var db = _db.CreateContext();
        var u = new ApplicationUser
        {
            UserName = Guid.NewGuid().ToString("N"),
            Email = $"{Guid.NewGuid():N}@rtub.test",
            Nickname = nickname,
            FirstName = first,
            LastName = last,
            IsExpelled = expelled,
            Categories = new List<MemberCategory> { category },
        };
        db.Users.Add(u);
        db.SaveChanges();
        return u.Id;
    }

    private int AddEnrollment(int eventId, string userId, bool going)
    {
        using var db = _db.CreateContext();
        var e = Enrollment.Create(userId, eventId);
        e.WillAttend = going;
        db.Enrollments.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    private async Task<List<Enrollment>> AnswersAsync(int eventId)
    {
        await using var db = _db.CreateContext();
        return await db.Enrollments.AsNoTracking().Where(e => e.EventId == eventId).ToListAsync();
    }

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }
}
