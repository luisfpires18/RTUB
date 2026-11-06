using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/pending-actions and /pending-actions (tasks 071 / 072) through the real host: real login, antiforgery and SQLite
/// (so the panel queries are proven to translate), and the answers through the real EnrollmentService /
/// RehearsalAttendanceService and the real unique indexes. Other tests share this database, so these assert on their own rows,
/// never on totals that other tests could change.
/// </summary>
public class PendingActionsApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;
    private static int _ip;
    private static int _day = 4000;

    public PendingActionsApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ThePageAndTheApi_AreForSignedInMembers()
    {
        var anonymous = Anonymous();

        (await anonymous.GetAsync("/api/pending-actions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/pending-actions/panels/events")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var page = await anonymous.GetAsync("/pending-actions");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=%2Fpending-actions");

        var (member, _) = await SignInAsync([MemberCategory.Leitao], retired: false);
        var shell = await member.GetAsync("/pending-actions");
        shell.StatusCode.Should().Be(HttpStatusCode.OK);
        shell.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        var api = await member.GetAsync("/api/pending-actions");
        api.Headers.CacheControl!.NoStore.Should().BeTrue("private data is never cached");
    }

    [Fact]
    public async Task AnActiveMember_SeesTheirUnansweredActivities_InCompactPagedPanels()
    {
        var (member, me) = await SignInAsync([MemberCategory.Tuno], retired: false);
        var mine = await AddEventAsync(NextDay());
        var answered = await AddEventAsync(NextDay());
        var rehearsal = await AddRehearsalAsync(NextDay());
        await AnswerEventAsync(answered, me.Id);

        var page = await member.GetFromJsonAsync<JsonElement>("/api/pending-actions");
        var raw = await member.GetStringAsync("/api/pending-actions/panels/events?filter=upcoming&page=1");

        page.GetProperty("status").GetString().Should().Be("active");
        page.GetProperty("panels").EnumerateArray().Select(p => p.GetProperty("key").GetString()).Should().Equal("events", "rehearsals", "logistics", "questions");
        raw.Should().NotContain(me.Id).And.NotContain("@test.com");
        var keys = await AllKeysAsync(member, "events", "upcoming");
        keys.Should().Contain(mine.ToString()).And.NotContain(answered.ToString());
        (await AllKeysAsync(member, "rehearsals", "upcoming")).Should().Contain(rehearsal.ToString());
        var panel = JsonDocument.Parse(raw).RootElement;
        panel.GetProperty("pageSize").GetInt32().Should().Be(3);
        panel.GetProperty("rows").GetArrayLength().Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task AReformado_GetsThePage_ButNoParticipationPanels()
    {
        var (member, _) = await SignInAsync([MemberCategory.Tuno], retired: true);

        var page = await member.GetFromJsonAsync<JsonElement>("/api/pending-actions");

        page.GetProperty("status").GetString().Should().Be("retired");
        page.GetProperty("panels").EnumerateArray().Select(p => p.GetProperty("key").GetString()).Should().Equal(["logistics", "questions"],
            "a Reformado keeps their own work, not participation");
        (await member.GetAsync("/api/pending-actions/panels/events")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DismissingAnAnswer_IsRefused_AndNeedsTheAntiforgeryToken()
    {
        var (member, _) = await SignInAsync([MemberCategory.Tuno], retired: false);
        var id = await AddEventAsync(NextDay());
        var body = new { kind = "event.response", sourceKey = id.ToString() };

        (await member.PostAsJsonAsync("/api/pending-actions/dismissals", body)).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "no X-CSRF-TOKEN");
        await WithTokenAsync(member);
        (await member.PostAsJsonAsync("/api/pending-actions/dismissals", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "an answer is given, not dismissed, whatever the request claims");
        (await member.PostAsJsonAsync("/api/pending-actions/dismissals", new { kind = "event.response", sourceKey = "999999" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PendingActionDismissals.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Login_LandsAnActiveMemberOnTheirActions_UnlessTheyAskedForAPage()
    {
        var (active, user) = await NewUserAsync([MemberCategory.Leitao], retired: false);
        var (retired, other) = await NewUserAsync([MemberCategory.Tuno], retired: true);

        (await LoginAsync(active, user, returnUrl: null)).Should().Be("/pending-actions");
        (await LoginAsync(Client(), user, returnUrl: "/logistics/3?card=7")).Should().Be("/logistics/3?card=7", "a deep link wins");
        (await LoginAsync(Client(), user, returnUrl: "https://evil.example/")).Should().Be("/pending-actions");
        (await LoginAsync(retired, other, returnUrl: null)).Should().Be("/events");

        var again = await active.GetAsync("/login");
        again.Headers.Location!.ToString().Should().Be("/pending-actions", "a signed-in active member opening /login goes to the same place");
    }

    // ---------- answers (072) ----------

    [Fact]
    public async Task Answers_WriteTheModulesOwnRows_AndTheModulePagesAgree()
    {
        // Past prompts need a past day of this fiscal year: none exists on 1-3 September.
        var today = PortugalTime.Today(TimeProvider.System);
        Assert.SkipWhen((today - new DateTime(today.Month >= 9 ? today.Year : today.Year - 1, 9, 1)).Days < 3,
            "the first days of a fiscal year have no past day to answer yet");
        var (member, me) = await SignInAsync([MemberCategory.Leitao], retired: false);
        await WithTokenAsync(member);
        var upcoming = await AddEventAsync(NextDay());
        var pastEvent = await AddEventAsync(-1);
        var rehearsal = await AddRehearsalAsync(NextDay());
        var pastRehearsal = await AddRehearsalAsync(-2);
        var missed = await AddRehearsalAsync(-3);

        (await Answer(member, "event.response", upcoming, "yes", "Guitarra", "Levo o estojo")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Answer(member, "event.response", pastEvent, "no")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Answer(member, "rehearsal.response", rehearsal, "no")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Answer(member, "rehearsal.response", pastRehearsal, "yes")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var going = await db.Enrollments.AsNoTracking().SingleAsync(x => x.EventId == upcoming && x.UserId == me.Id);
            going.WillAttend.Should().BeTrue();
            going.Instrument.Should().Be(InstrumentType.Guitarra);
            going.Notes.Should().Be("Levo o estojo");
            going.CategoryAtEvent.Should().Be(MemberCategory.Leitao, "the service's category snapshot");
            (await db.Enrollments.AsNoTracking().SingleAsync(x => x.EventId == pastEvent && x.UserId == me.Id)).WillAttend.Should().BeFalse(
                "a Não fui is kept as an answer, not just a hidden row");
            (await db.RehearsalAttendances.AsNoTracking().SingleAsync(a => a.RehearsalId == rehearsal && a.UserId == me.Id)).WillAttend.Should().BeFalse();
            var selfReport = await db.RehearsalAttendances.AsNoTracking().SingleAsync(a => a.RehearsalId == pastRehearsal && a.UserId == me.Id);
            (selfReport.WillAttend, selfReport.Attended).Should().Be((true, false), "a Fui waits for approval");
            (await db.Rehearsals.CountAsync(r => r.Id == pastRehearsal)).Should().Be(1, "no rehearsal is created");
        }

        // The module pages show the same answers; the page no longer lists them, the unanswered one still is.
        (await member.GetFromJsonAsync<JsonElement>($"/api/events/{upcoming}/enrollment")).GetProperty("status").GetString().Should().Be("going");
        (await member.GetFromJsonAsync<JsonElement>($"/api/rehearsals/{pastRehearsal}")).GetProperty("rehearsal").GetProperty("mine")
            .GetProperty("status").GetString().Should().Be("pending");
        (await AllKeysAsync(member, "events", "upcoming")).Should().NotContain(upcoming.ToString());
        (await AllKeysAsync(member, "events", "past")).Should().NotContain(pastEvent.ToString());
        (await AllKeysAsync(member, "rehearsals", "past")).Should().Contain(missed.ToString()).And.NotContain(pastRehearsal.ToString());
    }

    [Fact]
    public async Task Answers_NeedTheToken_AndRetriesOrRacesNeverWriteTwice()
    {
        var (member, me) = await SignInAsync([MemberCategory.Tuno], retired: false);
        var id = await AddEventAsync(NextDay());

        (await Answer(member, "event.response", id, "yes")).StatusCode.Should().Be(HttpStatusCode.BadRequest, "no X-CSRF-TOKEN");
        await WithTokenAsync(member);
        var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Answer(member, "event.response", id, i % 2 == 0 ? "yes" : "no")));

        answers.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        answers.Where(r => r.StatusCode != HttpStatusCode.NoContent).Select(r => r.StatusCode).Should()
            .OnlyContain(c => c == HttpStatusCode.NotFound || c == HttpStatusCode.Conflict);
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Enrollments.CountAsync(x => x.EventId == id && x.UserId == me.Id)).Should().Be(1);
    }

    [Fact]
    public async Task AnActivityChangedSinceTheRead_IsRefusedHonestly()
    {
        var (member, me) = await SignInAsync([MemberCategory.Tuno], retired: false);
        await WithTokenAsync(member);
        var cancelled = await AddEventAsync(NextDay());
        var answeredElsewhere = await AddRehearsalAsync(NextDay());
        await CancelEventAsync(cancelled);
        await AttendAsync(answeredElsewhere, me.Id, approved: true);

        (await Answer(member, "event.response", cancelled, "yes")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Answer(member, "rehearsal.response", answeredElsewhere, "no")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Enrollments.AnyAsync(x => x.EventId == cancelled)).Should().BeFalse();
        (await db.RehearsalAttendances.AsNoTracking().SingleAsync(a => a.RehearsalId == answeredElsewhere && a.UserId == me.Id))
            .Should().Match<RehearsalAttendance>(a => a.WillAttend && a.Attended, "a confirmed presença is never overwritten");
    }

    // ---------- Logística and Questões (073) ----------

    [Fact]
    public async Task Logistics_ShowsOnlyTheMembersOwnOpenCardsOnBoardsTheySee_AndConcluirIsTheBoardsOwnMove()
    {
        var (member, me) = await SignInAsync([MemberCategory.Leitao], retired: true); // own work: Reformados too
        await WithTokenAsync(member);
        var (_, other) = await SignInAsync([MemberCategory.Tuno], retired: false);
        var board = await SeedBoardAsync(withDone: true);
        var owned = await AddCardAsync(board.Todo, "Comprar cordas", owner: me.Id);
        var helping = await AddCardAsync(board.Todo, "Montar palco", owner: other.Id, collaborator: me.Id);
        var someoneElses = await AddCardAsync(board.Todo, "Carrinha", owner: other.Id);
        var unowned = await AddCardAsync(board.Todo, "Sem dono");
        var done = await AddCardAsync(board.Done, "Já feito", owner: me.Id);
        var privateBoard = await SeedBoardAsync(withDone: true, isPrivate: true);
        var hidden = await AddCardAsync(privateBoard.Todo, "Segredo", owner: me.Id);

        var page = await member.GetFromJsonAsync<JsonElement>("/api/pending-actions");
        page.GetProperty("panels").EnumerateArray().Select(p => p.GetProperty("key").GetString()).Should().Contain(["logistics", "questions"])
            .And.NotContain("events", "a Reformado has no participation panel");
        var keys = await AllKeysAsync(member, "logistics", null);
        keys.Should().Contain([owned.ToString(), helping.ToString()])
            .And.NotContain([someoneElses.ToString(), unowned.ToString(), done.ToString(), hidden.ToString()],
                "not mine, done, or on a private board I am not on (assignment alone does not open it)");

        (await Answer(member, "logistics.card", someoneElses, "done")).StatusCode.Should().Be(HttpStatusCode.NotFound, "another member's card");
        (await Answer(member, "logistics.card", hidden, "done")).StatusCode.Should().Be(HttpStatusCode.NotFound, "a board I cannot see");
        (await Answer(member, "logistics.card", owned, "yes")).StatusCode.Should().Be(HttpStatusCode.BadRequest, "not a logistics command");
        (await Answer(member, "logistics.card", owned, "done")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Answer(member, "logistics.card", owned, "done")).StatusCode.Should().Be(HttpStatusCode.NotFound, "a retry finds it done");

        var card = await member.GetFromJsonAsync<JsonElement>($"/api/logistics/cards/{owned}");
        card.GetProperty("card").GetProperty("status").GetString().Should().Be("Done", "the board's own status change");
        card.GetProperty("listId").GetInt32().Should().Be(board.Done, "moved to the done column");
        (await member.GetFromJsonAsync<JsonElement>("/api/logistics/my-tasks")).GetProperty("tasks").EnumerateArray()
            .Single(t => t.GetProperty("cardId").GetInt32() == owned).GetProperty("status").GetString().Should().Be("Done", "As minhas tarefas agrees");
        (await AllKeysAsync(member, "logistics", null)).Should().NotContain(owned.ToString());

        // Unassigned meanwhile: the card leaves, and Concluir is refused.
        await UnassignAsync(helping, me.Id);
        (await AllKeysAsync(member, "logistics", null)).Should().NotContain(helping.ToString());
        (await Answer(member, "logistics.card", helping, "done")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Logistics_ABoardWithoutADoneColumn_RefusesHonestly_AndTheCardStays()
    {
        var (member, me) = await SignInAsync([MemberCategory.Tuno], retired: false);
        await WithTokenAsync(member);
        var board = await SeedBoardAsync(withDone: false);
        var card = await AddCardAsync(board.Todo, "Sem coluna feita", owner: me.Id);

        (await Answer(member, "logistics.card", card, "done")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AllKeysAsync(member, "logistics", null)).Should().Contain(card.ToString());
    }

    [Fact]
    public async Task Questions_ListsTheAuthorsOpenQuestions_AndOnlyTheAuthorCloses()
    {
        var (author, me) = await SignInAsync([MemberCategory.Tuno], retired: false);
        var (recipient, them) = await SignInAsync([MemberCategory.Tuno], retired: false);
        await WithTokenAsync(author);
        await WithTokenAsync(recipient);
        var mine = await AddQuestionAsync(me.Id, them.Id, "Quando é o jantar?");
        var closed = await AddQuestionAsync(me.Id, them.Id, "Antiga", closed: true);
        var theirs = await AddQuestionAsync(them.Id, me.Id, "Pergunta deles");

        (await AllKeysAsync(author, "questions", null)).Should().Contain(mine.ToString()).And.NotContain([closed.ToString(), theirs.ToString()]);
        (await AllKeysAsync(recipient, "questions", null)).Should().Contain(theirs.ToString()).And.NotContain(mine.ToString(),
            "being the recipient does not let you close it");

        (await Answer(recipient, "question.close", mine, "close")).StatusCode.Should().Be(HttpStatusCode.NotFound, "a forged close by the recipient");
        (await Answer(author, "question.close", mine, "close")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Answer(author, "question.close", mine, "close")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var detail = await author.GetFromJsonAsync<JsonElement>($"/api/questions/{mine}");
        detail.GetProperty("question").GetProperty("status").GetString().Should().Be("closed", "the module's own close");
        (await AllKeysAsync(author, "questions", null)).Should().NotContain(mine.ToString());
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Questions.AsNoTracking().SingleAsync(q => q.Id == mine))
            .Content.Should().Be("Conteúdo da pergunta", "the thread is kept");
    }

    // ---------- Tesouraria (074) ----------

    [Fact]
    public async Task Treasurers_ShareOneRequiredReport_AndParallelClicksMakeOne()
    {
        await DeleteCurrentReportAsync();
        var fy = CurrentFiscalYearStart();
        var (first, _) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Admin", positions: [Position.PrimeiroTesoureiro]);
        var (second, _) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Admin", positions: [Position.SegundoTesoureiro]);
        var (admin, _) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Admin");
        await WithTokenAsync(first);
        await WithTokenAsync(second);
        await WithTokenAsync(admin);

        var row = (await PanelAsync(first, "treasury", "open")).GetProperty("rows").EnumerateArray().Single();
        row.GetProperty("required").GetBoolean().Should().BeTrue();
        row.GetProperty("sourceKey").GetString().Should().Be($"report:{fy}");
        (await PanelAsync(second, "treasury", "open")).GetProperty("rows").EnumerateArray().Single().GetProperty("sourceKey").GetString()
            .Should().Be($"report:{fy}", "one shared action for both treasurers");
        (await admin.GetAsync("/api/pending-actions/panels/treasury")).StatusCode.Should().Be(HttpStatusCode.NotFound, "a role without the position");
        (await Command(admin, "treasury", $"report:{fy}", "create")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await first.PostAsJsonAsync("/api/pending-actions/dismissals", new { kind = "treasury", sourceKey = $"report:{fy}" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "a required action cannot be dismissed");

        var clicks = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Command(i % 2 == 0 ? first : second, "treasury", $"report:{fy}", "create")));

        clicks.Count(c => c.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        clicks.Where(c => c.StatusCode != HttpStatusCode.NoContent).Select(c => c.StatusCode).Should()
            .OnlyContain(c => c == HttpStatusCode.NotFound || c == HttpStatusCode.Conflict);
        using var scope = _factory.Services.CreateScope();
        var reports = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Reports.AsNoTracking().Where(r => r.Year == fy).ToListAsync();
        reports.Should().ContainSingle().Which.Title.Should().Be($"Relatório de Contas {fy} - {fy + 1}", "the report page's own creation");
        (await PanelAsync(second, "treasury", "open")).GetProperty("rows").EnumerateArray()
            .Should().NotContain(r => r.GetProperty("sourceKey").GetString() == $"report:{fy}", "resolved for both");
    }

    [Fact]
    public async Task Treasurers_ClassifyEachBegunEvent_WithAnActivityOrASharedSemMovimentos()
    {
        Assert.SkipWhen(PortugalTime.Today(TimeProvider.System).Month == 9 && PortugalTime.Today(TimeProvider.System).Day == 1,
            "1 September: yesterday is in the previous fiscal year");
        var reportId = await EnsureCurrentReportAsync();
        var (first, firstUser) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Admin", positions: [Position.PrimeiroTesoureiro]);
        var (second, _) = await SignInAsync([MemberCategory.Tuno], retired: false, role: "Admin", positions: [Position.SegundoTesoureiro]);
        await WithTokenAsync(first);
        await WithTokenAsync(second);
        var begun = await AddEventAsync(-1);
        var another = await AddEventAsync(-1);
        var future = await AddEventAsync(NextDay());
        var cancelled = await AddEventAsync(-1);
        await CancelEventAsync(cancelled);

        var open = await AllKeysAsync(first, "treasury", "open");
        open.Should().Contain([$"event:{begun}", $"event:{another}"]).And.NotContain([$"event:{future}", $"event:{cancelled}"]);
        var row = await RowAsync(first, "treasury", "open", $"event:{begun}");
        row.GetProperty("actions")[0].GetProperty("href").GetString().Should().Be($"/treasury/reports/{reportId}?activityForEvent={begun}");

        // Não: shared, for both treasurers, and listed under "Sem movimentos" where it can be reopened.
        (await Command(first, "treasury", $"event:{begun}", "none")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AllKeysAsync(second, "treasury", "open")).Should().NotContain($"event:{begun}");
        (await AllKeysAsync(second, "treasury", "none")).Should().Contain($"event:{begun}");
        (await Command(second, "treasury", $"event:{begun}", "none")).StatusCode.Should().Be(HttpStatusCode.NotFound, "already decided");
        (await Command(second, "treasury", $"event:{begun}", "reopen")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AllKeysAsync(first, "treasury", "open")).Should().Contain($"event:{begun}");

        // Sim: an activity of this year's report linked to the event, on the report page; deleting it reopens the question.
        var created = await first.PostAsJsonAsync($"/api/treasury/reports/{reportId}/activities",
            new { name = "Serenata", startDate = DateTime.Today.AddDays(-1), endDate = (DateTime?)null, description = "", eventId = another });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var activity = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activities").EnumerateArray()
            .Single(a => a.ValueKind == JsonValueKind.Object && a.GetProperty("eventId").ValueKind == JsonValueKind.Number && a.GetProperty("eventId").GetInt32() == another);
        (await AllKeysAsync(second, "treasury", "open")).Should().NotContain($"event:{another}", "an activity accounts for it");
        (await first.DeleteAsync($"/api/treasury/activities/{activity.GetProperty("id").GetInt32()}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AllKeysAsync(second, "treasury", "open")).Should().Contain($"event:{another}", "deleting the activity re-evaluates it");
        (await first.PostAsJsonAsync($"/api/treasury/reports/{reportId}/activities",
                new { name = "Fora do ano", startDate = DateTime.Today, endDate = (DateTime?)null, description = "", eventId = cancelled }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a cancelled event cannot be linked");

        // A treasurer whose mandate ended no longer has the panel or its commands.
        await SetPositionsAsync(firstUser.Id, []);
        (await first.GetAsync("/api/pending-actions/panels/treasury")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Command(first, "treasury", $"event:{another}", "none")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ATreasurerWithoutTheModulesRole_IsRefusedByTheModule()
    {
        Assert.SkipWhen(PortugalTime.Today(TimeProvider.System).Month == 9 && PortugalTime.Today(TimeProvider.System).Day == 1,
            "1 September: yesterday is in the previous fiscal year");
        await EnsureCurrentReportAsync();
        var (treasurer, _) = await SignInAsync([MemberCategory.Tuno], retired: false, positions: [Position.PrimeiroTesoureiro]);
        await WithTokenAsync(treasurer);
        var e = await AddEventAsync(-1);

        (await AllKeysAsync(treasurer, "treasury", "open")).Should().Contain($"event:{e}", "the mandate shows the work");
        (await Command(treasurer, "treasury", $"event:{e}", "none")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "the treasury's report rule decides the write");
        (await AllKeysAsync(treasurer, "treasury", "open")).Should().Contain($"event:{e}");
    }

    // ---------- helpers ----------

    private async Task<(int Board, int Todo, int Done)> SeedBoardAsync(bool withDone, bool isPrivate = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var board = LogisticsBoard.Create($"PA {Guid.NewGuid():N}"[..12]);
        if (isPrivate)
        {
            board.Visibility = BoardVisibility.Private;
        }

        db.LogisticsBoards.Add(board);
        await db.SaveChangesAsync();
        board.LegacyFolder = "";
        var todo = LogisticsList.Create("Por fazer", board.Id, 0);
        todo.State = CardStatus.Todo;
        db.LogisticsLists.Add(todo);
        LogisticsList? done = null;
        if (withDone)
        {
            done = LogisticsList.Create("Feito", board.Id, 1);
            done.State = CardStatus.Done;
            db.LogisticsLists.Add(done);
        }

        await db.SaveChangesAsync();
        return (board.Id, todo.Id, done?.Id ?? 0);
    }

    private async Task<int> AddCardAsync(int listId, string title, string? owner = null, string? collaborator = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var list = await db.LogisticsLists.SingleAsync(l => l.Id == listId);
        var card = LogisticsCard.Create(title, listId, 0);
        card.Status = list.State ?? CardStatus.Todo;
        card.AssignedToUserId = owner;
        db.LogisticsCards.Add(card);
        await db.SaveChangesAsync();
        if (collaborator is not null)
        {
            db.LogisticsCardAssignments.Add(LogisticsCardAssignment.Create(card.Id, collaborator));
            await db.SaveChangesAsync();
        }

        return card.Id;
    }

    private async Task UnassignAsync(int cardId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.LogisticsCardAssignments.RemoveRange(db.LogisticsCardAssignments.Where(a => a.CardId == cardId && a.UserId == userId));
        await db.SaveChangesAsync();
    }

    private async Task<int> AddQuestionAsync(string authorId, string recipientId, string title, bool closed = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var q = Question.Create(title, "Conteúdo da pergunta", authorId, Position.Magister, recipientId);
        if (closed)
        {
            q.Close();
        }

        db.Questions.Add(q);
        await db.SaveChangesAsync();
        return q.Id;
    }

    private static Task<HttpResponseMessage> Answer(HttpClient client, string kind, int id, string answer, string? instrument = null, string? notes = null) =>
        client.PostAsJsonAsync("/api/pending-actions/responses", new { kind, sourceKey = id.ToString(), answer, instrument, notes });

    private async Task CancelEventAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Events.SingleAsync(e => e.Id == id)).IsCancelled = true;
        await db.SaveChangesAsync();
    }

    private async Task AttendAsync(int rehearsalId, string userId, bool approved)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var a = RehearsalAttendance.Create(rehearsalId, userId);
        a.MarkAttendance(approved);
        db.RehearsalAttendances.Add(a);
        await db.SaveChangesAsync();
    }

    private static int NextDay() => Interlocked.Increment(ref _day);

    private static async Task<List<string>> AllKeysAsync(HttpClient client, string panel, string? filter)
    {
        var keys = new List<string>();
        for (var page = 1; ; page++)
        {
            var body = await client.GetFromJsonAsync<JsonElement>($"/api/pending-actions/panels/{panel}?filter={filter ?? ""}&page={page}");
            keys.AddRange(body.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("sourceKey").GetString()!));
            var total = body.GetProperty("total").GetInt32();
            if (body.GetProperty("page").GetInt32() * 3 >= total)
            {
                return keys;
            }
        }
    }

    private async Task<int> AddEventAsync(int days)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var e = Event.Create($"Ação {days}", PortugalTime.Today(TimeProvider.System).AddDays(days).AddHours(21), "Braga", Enum.GetValues<EventType>()[0]);
        db.Events.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    private async Task<int> AddRehearsalAsync(int days)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var r = Rehearsal.Create(PortugalTime.Today(TimeProvider.System).AddDays(days), "Centro Académico");
        db.Rehearsals.Add(r);
        await db.SaveChangesAsync();
        return r.Id;
    }

    private async Task AnswerEventAsync(int eventId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Enrollments.Add(Enrollment.Create(userId, eventId));
        await db.SaveChangesAsync();
    }

    private async Task SetMembershipAsync(string userId, List<MemberCategory> categories, bool retired)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.Categories = categories;
        user.IsRetired = retired;
        await db.SaveChangesAsync();
    }

    private async Task<(HttpClient Client, ApplicationUser User)> SignInAsync(List<MemberCategory> categories, bool retired,
        string? role = null, List<Position>? positions = null)
    {
        var n = Interlocked.Increment(ref _ip);
        var (client, user) = await CookieTestSession.SignInAsync(_factory, $"pa{Guid.NewGuid():N}"[..20], $"10.81.{n / 250}.{n % 250 + 1}", role);
        await SetMembershipAsync(user.Id, categories, retired);
        if (positions is not null)
        {
            await SetPositionsAsync(user.Id, positions);
        }

        return (client, user);
    }

    private async Task SetPositionsAsync(string userId, List<Position> positions)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Users.SingleAsync(u => u.Id == userId)).Positions = positions;
        await db.SaveChangesAsync();
    }

    private static int CurrentFiscalYearStart()
    {
        var today = PortugalTime.Today(TimeProvider.System);
        return today.Month >= 9 ? today.Year : today.Year - 1;
    }

    private async Task DeleteCurrentReportAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fy = CurrentFiscalYearStart();
        db.Activities.RemoveRange(db.Activities.Where(a => a.Report!.Year == fy));
        db.Reports.RemoveRange(db.Reports.Where(r => r.Year == fy));
        await db.SaveChangesAsync();
    }

    private async Task<int> EnsureCurrentReportAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fy = CurrentFiscalYearStart();
        if (await db.Reports.FirstOrDefaultAsync(r => r.Year == fy) is { } existing)
        {
            return existing.Id;
        }

        var report = Report.Create($"Relatório de Contas {fy} - {fy + 1}", fy);
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        return report.Id;
    }

    private static Task<HttpResponseMessage> Command(HttpClient client, string kind, string sourceKey, string answer) =>
        client.PostAsJsonAsync("/api/pending-actions/responses", new { kind, sourceKey, answer });

    private static Task<JsonElement> PanelAsync(HttpClient client, string panel, string? filter) =>
        client.GetFromJsonAsync<JsonElement>($"/api/pending-actions/panels/{panel}?filter={filter ?? ""}&page=1");

    private static async Task<JsonElement> RowAsync(HttpClient client, string panel, string? filter, string sourceKey)
    {
        for (var page = 1; ; page++)
        {
            var body = await client.GetFromJsonAsync<JsonElement>($"/api/pending-actions/panels/{panel}?filter={filter ?? ""}&page={page}");
            foreach (var row in body.GetProperty("rows").EnumerateArray())
            {
                if (row.GetProperty("sourceKey").GetString() == sourceKey)
                {
                    return row;
                }
            }

            if (body.GetProperty("page").GetInt32() * 3 >= body.GetProperty("total").GetInt32())
            {
                throw new InvalidOperationException($"{sourceKey} not in {panel}");
            }
        }
    }

    /// <summary>A confirmed account (not signed in yet) with the given membership, and a client of its own.</summary>
    private async Task<(HttpClient Client, (string UserName, string Password) Login)> NewUserAsync(List<MemberCategory> categories, bool retired)
    {
        var name = $"pl{Guid.NewGuid():N}"[..20];
        var password = TestSecret.NewPassword();
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = name, Email = $"{name}@test.com", EmailConfirmed = true, FirstName = "Ana", LastName = "Silva", Nickname = name,
                Categories = categories, IsRetired = retired,
            };
            (await users.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        }

        return (Client(), (name, password));
    }

    private static async Task<string> LoginAsync(HttpClient client, (string UserName, string Password) login, string? returnUrl)
    {
        var token = await AntiforgeryFormToken.FetchAsync(client);
        var form = new Dictionary<string, string>
        {
            [AntiforgeryFormToken.FieldName] = token!,
            ["Username"] = login.UserName,
            ["Password"] = login.Password,
            ["RememberMe"] = "false",
        };
        if (returnUrl is not null)
        {
            form["ReturnUrl"] = returnUrl;
        }

        var response = await client.PostAsync("/auth/login", new FormUrlEncodedContent(form));
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        return response.Headers.Location!.ToString();
    }

    private HttpClient Client()
    {
        var n = Interlocked.Increment(ref _ip);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, $"10.82.{n / 250}.{n % 250 + 1}");
        return client;
    }

    private HttpClient Anonymous() => Client();

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
