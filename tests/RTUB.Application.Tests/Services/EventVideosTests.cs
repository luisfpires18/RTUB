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
/// Video management behind the React event page (React track 012C), through the real EventService:
/// its storage key, sort position, push and delete cleanup are exercised against a fake storage and a
/// fake push, so nothing is ever uploaded, deleted or sent for real.
/// </summary>
public class EventVideosTests
{
    private static readonly ClaimsPrincipal Visitor = new(new ClaimsIdentity());
    private static readonly ClaimsPrincipal Member = SignedIn("Member");
    private static readonly ClaimsPrincipal Mod = SignedIn("Mod");
    private static readonly ClaimsPrincipal Admin = SignedIn("Admin");
    private static readonly ClaimsPrincipal Owner = SignedIn("Owner");

    private readonly DatabaseFixture _db = new();
    private readonly Mock<IEventVideoStorageService> _storage = new();
    private readonly Mock<IPushNotificationService> _push = new();
    private readonly EventAdminService _service;
    private readonly EventAgendaService _agenda;
    private readonly DateTime _past = DateTime.Today.AddDays(-30);
    private int _uploads;

    public EventVideosTests()
    {
        var contexts = new Contexts(_db);
        var users = new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        var events = new EventService(new EventRepository(contexts), Mock.Of<IImageStorageService>(MockBehavior.Strict), new EnrollmentRepository(contexts),
            new EventVideoRepository(contexts), _storage.Object, Mock.Of<IPushNotificationFactory>(), _push.Object, users.Object,
            Mock.Of<IHttpContextAccessor>(), contexts);
        _service = new EventAdminService(contexts, events, Mock.Of<IEmailNotificationService>(MockBehavior.Strict), _push.Object,
            Mock.Of<IPushNotificationFactory>(), Mock.Of<IAuditLogService>(), Mock.Of<ITrophyService>(MockBehavior.Strict), NullLogger<EventAdminService>.Instance);
        _agenda = new EventAgendaService(contexts, Mock.Of<IEnrollmentService>(), events, Mock.Of<IPushNotificationFactory>(),
            Mock.Of<IPushNotificationService>(), Mock.Of<IAuditLogService>(), NullLogger<EventAgendaService>.Instance);

        // The fake storage answers the key the real one would build, minus the timestamp.
        _storage.Setup(s => s.UploadVideoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((Stream _, string file, string _, int eventId) => $"https://pub-test.r2.dev/events/test/videos/{eventId}_{++_uploads}_{file}");
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
    public async Task OnlyAdminOrOwner_MayListUploadRenameReorderOrDelete(string who, EventResultStatus expected)
    {
        var user = who switch { "visitor" => Visitor, "member" => Member, _ => Mod };
        var id = AddEvent(_past);
        var video = AddVideo(id, "Abertura", 0);

        (await _service.GetVideosAsync(id, user)).Status.Should().Be(expected);
        (await _service.AddVideoAsync(id, Upload(), user)).Status.Should().Be(expected);
        (await _service.RenameVideoAsync(id, video, new EventVideoTitleInput("x"), user)).Status.Should().Be(expected);
        (await _service.ReorderVideosAsync(id, new EventVideoOrderInput(new[] { video }), user)).Status.Should().Be(expected);
        (await _service.DeleteVideoAsync(id, video, user)).Status.Should().Be(expected);

        _storage.VerifyNoOtherCalls();
        (await TitlesAsync(id)).Should().Equal("Abertura");
    }

    // ---------- upload ----------

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task Upload_StoresThroughTheExistingService_AndAppendsTheVideo(string role)
    {
        var id = AddEvent(_past);
        AddVideo(id, "Primeiro", 0);

        var result = await _service.AddVideoAsync(id, Upload(title: "  Atuação completa  "), role == "Owner" ? Owner : Admin);

        result.Status.Should().Be(EventResultStatus.Ok);
        result.Value!.Select(v => v.Title).Should().Equal("Primeiro", "Atuação completa");
        _storage.Verify(s => s.UploadVideoAsync(It.IsAny<Stream>(), "festa.mp4", "video/mp4", id), Times.Once);
        await using var db = _db.CreateContext();
        var stored = await db.EventVideos.SingleAsync(v => v.Title == "Atuação completa");
        stored.Url.Should().StartWith($"https://pub-test.r2.dev/events/test/videos/{id}_");
        stored.SortOrder.Should().Be(1, "the next position, as before");
        stored.SizeBytes.Should().Be(4);
    }

    [Theory]
    [InlineData("text/plain", "notas.txt", 4, "file")]
    [InlineData("video/mp4", "festa.mp4", 0, "file")]
    [InlineData("video/mp4", "festa.mp4", EventAdminService.MaxVideoBytes + 1, "file")]
    public async Task Upload_RefusesWhatIsNotAVideoUpTo100MB(string type, string name, long length, string field)
    {
        var id = AddEvent(_past);

        var result = await _service.AddVideoAsync(id, new EventVideoUpload(new MemoryStream(new byte[4]), name, type, length, "Título"), Admin);

        result.Errors!.Keys.Should().Contain(field);
        _storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Upload_AcceptsAKnownVideoExtension_WhenThePhoneSendsNoType()
    {
        var id = AddEvent(_past);

        (await _service.AddVideoAsync(id, new EventVideoUpload(new MemoryStream(new byte[4]), "IMG_1.MOV", "", 4, "Do telemóvel"), Admin))
            .Status.Should().Be(EventResultStatus.Ok);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Upload_NeedsATitle_AsTheOldFormDid(string? title)
    {
        var id = AddEvent(_past);

        (await _service.AddVideoAsync(id, Upload(title: title), Admin)).Errors!.Keys.Should().Contain("title");
        (await _service.AddVideoAsync(id, Upload(title: new string('t', 201)), Admin)).Errors!.Keys.Should().Contain("title");
        _storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Upload_OnlyToPastEvents_AsOnTheOldPage()
    {
        var upcoming = AddEvent(DateTime.Today.AddDays(5));

        (await _service.AddVideoAsync(upcoming, Upload(), Admin)).Status.Should().Be(EventResultStatus.Closed);
        (await _service.AddVideoAsync(999_999, Upload(), Admin)).Status.Should().Be(EventResultStatus.NotFound);
        _storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Upload_FailingInStorage_LeavesNoRow()
    {
        var id = AddEvent(_past);
        _storage.Setup(s => s.UploadVideoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("R2 down"));

        var act = () => _service.AddVideoAsync(id, Upload(), Admin);

        await act.Should().ThrowAsync<InvalidOperationException>("the endpoint answers it as a 500 problem");
        (await TitlesAsync(id)).Should().BeEmpty();
    }

    // ---------- rename, reorder, delete ----------

    [Fact]
    public async Task Rename_ChangesTheTitle_AndBlankClearsIt()
    {
        var id = AddEvent(_past);
        var video = AddVideo(id, "Antigo", 0);

        (await _service.RenameVideoAsync(id, video, new EventVideoTitleInput("  Novo  "), Owner)).Value!.Single().Title.Should().Be("Novo");
        (await _service.RenameVideoAsync(id, video, new EventVideoTitleInput(" "), Owner)).Value!.Single().Title.Should().BeNull();
        (await _service.RenameVideoAsync(id, video, new EventVideoTitleInput(new string('t', 201)), Owner)).Errors!.Keys.Should().Contain("title");
    }

    [Fact]
    public async Task Reorder_SetsTheWholeOrder_AndRefusesAStaleOne()
    {
        var id = AddEvent(_past);
        var a = AddVideo(id, "A", 0);
        var b = AddVideo(id, "B", 1);
        var c = AddVideo(id, "C", 2);

        (await _service.ReorderVideosAsync(id, new EventVideoOrderInput(new[] { c, a, b }), Admin)).Value!.Select(v => v.Title)
            .Should().Equal("C", "A", "B");
        (await PublicTitlesAsync(id)).Should().Equal(new[] { "C", "A", "B" }, "the page plays them in this order");

        (await _service.ReorderVideosAsync(id, new EventVideoOrderInput(new[] { a, b }), Admin)).Errors!.Keys.Should().Contain("videoIds");
        (await _service.ReorderVideosAsync(id, new EventVideoOrderInput(new[] { a, a, b }), Admin)).Errors!.Keys.Should().Contain("videoIds");
        (await _service.ReorderVideosAsync(id, new EventVideoOrderInput(new[] { a, b, 999 }), Admin)).Errors!.Keys.Should().Contain("videoIds");
        (await PublicTitlesAsync(id)).Should().Equal("C", "A", "B");
    }

    [Fact]
    public async Task Delete_RemovesTheStoredFileAndTheRow_AndTheLastOneEmptiesThePage()
    {
        var id = AddEvent(_past);
        var video = AddVideo(id, "Único", 0);

        var result = await _service.DeleteVideoAsync(id, video, Admin);

        result.Value!.Should().BeEmpty();
        _storage.Verify(s => s.DeleteVideoAsync($"https://pub-test.r2.dev/events/test/videos/{id}_x.mp4"), Times.Once);
        (await _agenda.GetEventAsync(id, Visitor))!.Videos.Should().BeEmpty();
        (await _agenda.GetEventAsync(id, Visitor))!.Event.VideoCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_StillRemovesTheRow_WhenStorageFails()
    {
        var id = AddEvent(_past);
        var video = AddVideo(id, "Único", 0);
        _storage.Setup(s => s.DeleteVideoAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("R2 down"));

        (await _service.DeleteVideoAsync(id, video, Admin)).Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task AVideo_IsOnlyReachedThroughItsOwnEvent()
    {
        var mine = AddEvent(_past);
        var other = AddEvent(_past);
        var theirs = AddVideo(other, "Deles", 0);

        (await _service.RenameVideoAsync(mine, theirs, new EventVideoTitleInput("x"), Admin)).Status.Should().Be(EventResultStatus.NotFound);
        (await _service.DeleteVideoAsync(mine, theirs, Admin)).Status.Should().Be(EventResultStatus.NotFound);
        (await _service.ReorderVideosAsync(mine, new EventVideoOrderInput(new[] { theirs }), Admin)).Status.Should().Be(EventResultStatus.Invalid);
        _storage.VerifyNoOtherCalls();
        (await TitlesAsync(other)).Should().Equal("Deles");
    }

    [Fact]
    public async Task ExistingVideos_StayManageable_OnAnUpcomingEvent()
    {
        var id = AddEvent(DateTime.Today.AddDays(5));
        var video = AddVideo(id, "Ensaio", 0);

        (await _service.RenameVideoAsync(id, video, new EventVideoTitleInput("Ensaio geral"), Admin)).Status.Should().Be(EventResultStatus.Ok);
        (await _service.DeleteVideoAsync(id, video, Admin)).Status.Should().Be(EventResultStatus.Ok);
    }

    [Fact]
    public void TheManagedVideo_CarriesOnlyIdAndTitle()
    {
        typeof(EventManagedVideoDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Id", "Title");
    }

    // ---------- helpers ----------

    private static ClaimsPrincipal SignedIn(string role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-" + role),
            new Claim(ClaimTypes.Name, "u-" + role),
            new Claim(ClaimTypes.Role, role),
        }, "test"));

    private static EventVideoUpload Upload(string? title = "Festa") =>
        new(new MemoryStream(new byte[] { 1, 2, 3, 4 }), "festa.mp4", "video/mp4", 4, title);

    private int AddEvent(DateTime date)
    {
        using var db = _db.CreateContext();
        var e = Event.Create("Festival", date, "Bragança", EventType.Festival, "");
        db.Events.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    private int AddVideo(int eventId, string title, int order)
    {
        using var db = _db.CreateContext();
        var v = EventVideo.CreateVideo(eventId, $"https://pub-test.r2.dev/events/test/videos/{eventId}_x.mp4", "video/mp4", 10, "someone", title, order);
        db.EventVideos.Add(v);
        db.SaveChanges();
        return v.Id;
    }

    private async Task<List<string?>> TitlesAsync(int eventId)
    {
        await using var db = _db.CreateContext();
        return await db.EventVideos.AsNoTracking().Where(v => v.EventId == eventId).OrderBy(v => v.SortOrder).Select(v => v.Title).ToListAsync();
    }

    private async Task<IEnumerable<string>> PublicTitlesAsync(int eventId) =>
        (await _agenda.GetEventAsync(eventId, Visitor))!.Videos.Select(v => v.Title);

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }
}
