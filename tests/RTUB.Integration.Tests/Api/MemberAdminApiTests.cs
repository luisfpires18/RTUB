using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The member admin tools on /api/members (React track 018, were the Blazor /members/manage) through the real host:
/// real login, antiforgery, Identity and SQLite. Email and push are the recording fakes of <see cref="EventsApiFactory"/>:
/// nothing is ever sent. Every test scopes its own members, since the class shares one database.
/// </summary>
public class MemberAdminApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public MemberAdminApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_MembersAndMods_CannotUseTheTools()
    {
        var tuno = await AddAsync(MemberCategory.Tuno);
        var leitao = await AddAsync(MemberCategory.Leitao);
        var calls = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Get, $"/api/members/{tuno.Id}/edit", null),
            (HttpMethod.Get, "/api/members/mentors?q=a", null),
            (HttpMethod.Post, "/api/members", Valid("Leitao", Unique())),
            (HttpMethod.Put, $"/api/members/{tuno.Id}", Valid("Tuno", Unique())),
            (HttpMethod.Delete, $"/api/members/{leitao.Id}", null),
            (HttpMethod.Post, $"/api/members/{tuno.Id}/instruments", new { instrument = "Guitarra" }),
            (HttpMethod.Delete, $"/api/members/{tuno.Id}/instruments/1", null),
            (HttpMethod.Put, $"/api/members/{tuno.Id}/instruments/1/primary", null),
            (HttpMethod.Put, $"/api/members/{leitao.Id}/nickname", new { nickname = "Novo" }),
            (HttpMethod.Post, $"/api/members/{leitao.Id}/expel", null),
            (HttpMethod.Post, $"/api/members/{leitao.Id}/reactivate", null),
            (HttpMethod.Post, $"/api/members/{tuno.Id}/activate", null),
            (HttpMethod.Post, $"/api/members/{tuno.Id}/reminder", null),
        };

        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        foreach (var (method, path, body) in calls)
        {
            (await SendAsync(anonymous, method, path, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {path}");
        }

        foreach (var role in new[] { "Member", "Mod" })
        {
            var (client, _) = await SignInAsync(role);
            await WithTokenAsync(client);
            foreach (var (method, path, body) in calls)
            {
                (await SendAsync(client, method, path, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{role}: {method} {path}");
            }
        }

        (await UserAsync(leitao.Id)).Should().NotBeNull();
        (await UserAsync(leitao.Id))!.IsExpelled.Should().BeFalse();
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryHeader()
    {
        var leitao = await AddAsync(MemberCategory.Leitao);
        var (admin, _) = await SignInAsync("Admin");
        var email = Unique() + "@test.com";

        (await admin.PostAsJsonAsync("/api/members", Valid("Leitao", Unique(), email))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync($"/api/members/{leitao.Id}/expel", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.DeleteAsync($"/api/members/{leitao.Id}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await UserByEmailAsync(email)).Should().BeNull();
        (await UserAsync(leitao.Id))!.IsExpelled.Should().BeFalse();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task Create_AsTheOldForm_UsernameRoleCategoriesInstruments_AndTheWelcomeEmail(string role)
    {
        var (admin, _) = await SignInAsync(role);
        await WithTokenAsync(admin);
        var nickname = "Zé " + Unique();
        var email = Unique() + "@test.com";
        var input = Valid("Caloiro", nickname, email) with
        {
            YearLeitao = 2023, MonthLeitao = 10, YearCaloiro = 2024, MonthCaloiro = 3,
            Instruments = new[] { new MemberInstrumentInput("Bandolim", false), new MemberInstrumentInput("Guitarra", true), new MemberInstrumentInput("Bandolim", false) },
        };

        var response = await admin.PostAsJsonAsync("/api/members", input);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await UserByEmailAsync(email))!;
        created.UserName.Should().Be(UsernameHelper.NormalizeUsername(nickname), "the username comes from the nickname, as before");
        created.Nickname.Should().Be(nickname);
        created.Categories.Should().Equal(MemberCategory.Caloiro);
        created.EmailConfirmed.Should().BeTrue();
        created.RequirePasswordChange.Should().BeTrue();
        (created.YearCaloiro, created.MonthCaloiro).Should().Be((2024, 3));
        (await RolesAsync(created.Id)).Should().Equal("Member");
        var instruments = await InstrumentsAsync(created.Id);
        instruments.Select(i => (i.InstrumentType, i.IsPrimary)).Should().BeEquivalentTo(new[] { (InstrumentType.Bandolim, false), (InstrumentType.Guitarra, true) });
        _factory.Email.Verify(e => e.SendWelcomeEmailAsync(created.UserName!, email, "Novo Membro", nickname, It.Is<string>(p => p.Length >= 6)), Times.Once);
    }

    [Fact]
    public async Task Create_LeitaoWithoutNickname_TakesTheEmail_AndFundadorIsTunoSince1991WithoutPadrinho()
    {
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var local = "novo." + Unique();
        var leitao = Valid("Leitao", null, local + "@test.com") with { NoNickname = true };

        (await admin.PostAsJsonAsync("/api/members", leitao)).StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await UserByEmailAsync(local + "@test.com"))!;
        created.Nickname.Should().Be(local);
        created.UserName.Should().Be(UsernameHelper.NormalizeUsername(local));
        created.Categories.Should().Equal(MemberCategory.Leitao);

        var mentor = await AddAsync(MemberCategory.Tuno, yearTuno: 2000);
        var fundadorEmail = Unique() + "@test.com";
        var fundador = Valid("Tuno", "Fund " + Unique(), fundadorEmail) with
        {
            Fundador = true, YearLeitao = 1990, MonthLeitao = 1, YearCaloiro = 1991, MonthCaloiro = 1, MentorId = mentor.Id,
        };
        (await admin.PostAsJsonAsync("/api/members", fundador)).StatusCode.Should().Be(HttpStatusCode.Created);
        var f = (await UserByEmailAsync(fundadorEmail))!;
        f.Categories.Should().Equal(MemberCategory.Tuno, MemberCategory.Fundador);
        (f.YearTuno, f.MonthTuno, f.YearLeitao, f.YearCaloiro, f.MentorId).Should().Be((1991, 12, null, null, null));
    }

    [Fact]
    public async Task Create_Validates_AsTheOldForm_AndSendsNothingWhenRefused()
    {
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var existing = await AddAsync(MemberCategory.Tuno);
        var leitao = await AddAsync(MemberCategory.Leitao);

        var empty = await admin.PostAsJsonAsync("/api/members", new { });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Errors(empty)).Keys.Should().Contain(new[] { "firstName", "lastName", "nickname", "phoneNumber", "email", "category" });

        var tooLong = Valid("Tuno", new string('a', 81), Unique() + "@test.com");
        (await Errors(await admin.PostAsJsonAsync("/api/members", tooLong)))["nickname"].Should().ContainMatch("*80*");

        var sameUsername = Valid("Tuno", existing.Nickname, Unique() + "@test.com");
        (await Errors(await admin.PostAsJsonAsync("/api/members", sameUsername)))["nickname"].Single().Should().StartWith("Já existe um utilizador com o nome de tuna");

        var sameEmail = Valid("Tuno", "Outro " + Unique(), existing.Email);
        (await Errors(await admin.PostAsJsonAsync("/api/members", sameEmail)))["email"].Single().Should().StartWith("Já existe um utilizador com o email");

        var badInstrument = Valid("Tuno", "I " + Unique(), Unique() + "@test.com") with { Instruments = new[] { new MemberInstrumentInput("Kazoo", true) } };
        (await Errors(await admin.PostAsJsonAsync("/api/members", badInstrument))).Keys.Should().Contain("instruments");

        var leitaoMentor = Valid("Caloiro", "M " + Unique(), Unique() + "@test.com") with { MentorId = leitao.Id };
        (await Errors(await admin.PostAsJsonAsync("/api/members", leitaoMentor))).Keys.Should().Contain("mentorId", "only Tunos and above are padrinhos");

        var badDate = Valid("Tuno", "D " + Unique(), Unique() + "@test.com") with { YearTuno = DateTime.Now.Year + 1, MonthTuno = 13 };
        (await Errors(await admin.PostAsJsonAsync("/api/members", badDate))).Keys.Should().Contain("tuno");

        _factory.Email.Verify(e => e.SendWelcomeEmailAsync(It.IsAny<string>(), It.Is<string>(m => m == existing.Email), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Edit_KeepsTheUsername_RefusesOwnPadrinho_AndReturnsOnlyTheFormFields()
    {
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var mentor = await AddAsync(MemberCategory.Tuno, yearTuno: 2001);
        var member = await AddAsync(MemberCategory.Caloiro);

        var edit = await admin.GetFromJsonAsync<JsonElement>($"/api/members/{member.Id}/edit");
        edit.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "firstName", "lastName", "nickname", "phoneNumber", "email", "city",
            "degree", "dateOfBirth", "category", "fundador", "honorario", "yearLeitao", "monthLeitao", "yearCaloiro", "monthCaloiro", "yearTuno",
            "monthTuno", "mentorId", "mentorName", "instruments", "lockedDates");
        edit.GetProperty("category").GetString().Should().Be("Caloiro");

        var update = Valid("Tuno", "Renomeado " + Unique(), member.Email) with { City = "Porto", YearTuno = 2024, MonthTuno = 5, MentorId = mentor.Id };
        var response = await admin.PutAsJsonAsync($"/api/members/{member.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = (await UserAsync(member.Id))!;
        saved.UserName.Should().Be(member.UserName, "the username is set only on creation");
        (saved.Nickname, saved.City, saved.MentorId, saved.YearTuno).Should().Be((update.Nickname, "Porto", mentor.Id, 2024));
        saved.Categories.Should().Equal(MemberCategory.Tuno);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mentorName").GetString().Should().Contain(mentor.Nickname);

        var own = await admin.PutAsJsonAsync($"/api/members/{member.Id}", update with { MentorId = member.Id });
        (await Errors(own))["mentorId"].Single().Should().Be("Um utilizador não pode ser o seu próprio padrinho.");
        (await admin.PutAsJsonAsync("/api/members/no-such-member", update)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CompleteDates_ChangeOnlyForOwnerOrTheCurrentMagister_IncompleteOnesForAnyAdmin()
    {
        var member = await AddAsync(MemberCategory.Tuno, yearTuno: 2015);
        await SetDatesAsync(member.Id, leitao: (2013, 10), caloiro: (2014, null));
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var change = Valid("Tuno", member.Nickname, member.Email) with
        {
            YearLeitao = 2012, MonthLeitao = 1, YearCaloiro = 2014, MonthCaloiro = 4, YearTuno = 2016, MonthTuno = 2,
        };

        var locked = await admin.GetFromJsonAsync<JsonElement>($"/api/members/{member.Id}/edit");
        locked.GetProperty("lockedDates").GetProperty("leitao").GetBoolean().Should().BeTrue();
        locked.GetProperty("lockedDates").GetProperty("caloiro").GetBoolean().Should().BeFalse("an incomplete pair can be completed");
        (await admin.PutAsJsonAsync($"/api/members/{member.Id}", change)).StatusCode.Should().Be(HttpStatusCode.OK);
        var afterAdmin = (await UserAsync(member.Id))!;
        (afterAdmin.YearLeitao, afterAdmin.MonthLeitao).Should().Be((2013, 10), "an Admin's change to a complete pair is ignored, as before");
        (afterAdmin.YearCaloiro, afterAdmin.MonthCaloiro).Should().Be((2014, 4));
        (afterAdmin.YearTuno, afterAdmin.MonthTuno).Should().Be((2015, 1));

        var (magister, magisterUser) = await SignInAsync("Admin");
        await WithTokenAsync(magister);
        await AddMagisterAsync(magisterUser.Id);
        (await magister.PutAsJsonAsync($"/api/members/{member.Id}", change)).StatusCode.Should().Be(HttpStatusCode.OK);
        var afterMagister = (await UserAsync(member.Id))!;
        (afterMagister.YearLeitao, afterMagister.YearTuno).Should().Be((2012, 2016), "the current Magister may correct dates");

        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);
        (await owner.GetFromJsonAsync<JsonElement>($"/api/members/{member.Id}/edit")).GetProperty("lockedDates").GetProperty("leitao").GetBoolean().Should().BeFalse();
        (await owner.PutAsJsonAsync($"/api/members/{member.Id}", change with { YearLeitao = 2011 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await UserAsync(member.Id))!.YearLeitao.Should().Be(2011);
    }

    [Fact]
    public async Task Instruments_FirstIsPrimary_NoDuplicates_RemovingThePrimaryPromotesTheNext()
    {
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var member = await AddAsync(MemberCategory.Tuno);
        var other = await AddAsync(MemberCategory.Tuno);

        var first = await Json(await admin.PostAsJsonAsync($"/api/members/{member.Id}/instruments", new { instrument = "Guitarra" }));
        first.EnumerateArray().Single().GetProperty("primary").GetBoolean().Should().BeTrue();
        (await admin.PostAsJsonAsync($"/api/members/{member.Id}/instruments", new { instrument = "Guitarra" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var two = await Json(await admin.PostAsJsonAsync($"/api/members/{member.Id}/instruments", new { instrument = "Bandolim" }));
        var bandolim = two.EnumerateArray().Single(i => i.GetProperty("instrument").GetString() == "Bandolim");
        bandolim.GetProperty("primary").GetBoolean().Should().BeFalse();

        var guitarraId = (await InstrumentsAsync(member.Id)).Single(i => i.InstrumentType == InstrumentType.Guitarra).Id;
        (await admin.DeleteAsync($"/api/members/{other.Id}/instruments/{guitarraId}")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "an instrument is only changed through its own member");
        (await admin.DeleteAsync($"/api/members/{member.Id}/instruments/{guitarraId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await InstrumentsAsync(member.Id)).Single().Should().Match<MemberInstrument>(i => i.InstrumentType == InstrumentType.Bandolim && i.IsPrimary);

        await admin.PostAsJsonAsync($"/api/members/{member.Id}/instruments", new { instrument = "Pandeireta" });
        var pandeireta = (await InstrumentsAsync(member.Id)).Single(i => i.InstrumentType == InstrumentType.Pandeireta);
        (await admin.PutAsync($"/api/members/{member.Id}/instruments/{pandeireta.Id}/primary", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await InstrumentsAsync(member.Id)).Single(i => i.IsPrimary).InstrumentType.Should().Be(InstrumentType.Pandeireta);
        (await admin.PostAsJsonAsync($"/api/members/{member.Id}/instruments", new { instrument = "Kazoo" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Nickname_IsForLeitoes_ChangesTheUsername_AndEmailsTheMember()
    {
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var leitao = await AddAsync(MemberCategory.Leitao);
        var tuno = await AddAsync(MemberCategory.Tuno);
        var nickname = "Bacorinho " + Unique();

        (await admin.PutAsJsonAsync($"/api/members/{tuno.Id}/nickname", new { nickname })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PutAsJsonAsync($"/api/members/{leitao.Id}/nickname", new { nickname = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Errors(await admin.PutAsJsonAsync($"/api/members/{leitao.Id}/nickname", new { nickname = tuno.Nickname })))["nickname"]
            .Single().Should().StartWith("Já existe um utilizador com o username");

        (await admin.PutAsJsonAsync($"/api/members/{leitao.Id}/nickname", new { nickname })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var saved = (await UserAsync(leitao.Id))!;
        (saved.Nickname, saved.UserName).Should().Be((nickname, UsernameHelper.NormalizeUsername(nickname)));
        _factory.Email.Verify(e => e.SendUsernameChangedEmailAsync(leitao.Email!, "Mem Bro", nickname, leitao.UserName!, saved.UserName!), Times.Once);
    }

    [Fact]
    public async Task ExpelAndReactivate_AreForLeitoes()
    {
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var leitao = await AddAsync(MemberCategory.Leitao);
        var tuno = await AddAsync(MemberCategory.Tuno);

        (await admin.PostAsync($"/api/members/{tuno.Id}/expel", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsync($"/api/members/{leitao.Id}/expel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UserAsync(leitao.Id))!.IsExpelled.Should().BeTrue();
        (await admin.GetFromJsonAsync<JsonElement>($"/api/members/{leitao.Id}")).GetProperty("expelled").GetBoolean().Should().BeTrue();
        (await admin.PostAsync($"/api/members/{leitao.Id}/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UserAsync(leitao.Id))!.IsExpelled.Should().BeFalse();
        (await UserAsync(tuno.Id))!.IsExpelled.Should().BeFalse();
    }

    [Fact]
    public async Task MakeActive_AndTheReminder_FollowTheOldListsConditions()
    {
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var activeTuno = await AddAsync(MemberCategory.Tuno, yearTuno: 2020);
        var leitao = await AddAsync(MemberCategory.Leitao);
        var almostBack = await AddAsync(MemberCategory.Tuno, yearTuno: 2015, retired: true);
        // Two completed months with a rehearsal, none this month: 2/3 on the way back.
        var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await AttendAsync(almostBack.Id, thisMonth.AddMonths(-1).AddDays(9));
        await AttendAsync(almostBack.Id, thisMonth.AddMonths(-2).AddDays(10));

        var row = (await admin.GetFromJsonAsync<JsonElement>($"/api/members/active?q={Uri.EscapeDataString(almostBack.Nickname!)}")).EnumerateArray().Single();
        row.GetProperty("encourage").GetBoolean().Should().BeTrue();
        row.GetProperty("canMakeActive").GetBoolean().Should().BeTrue();

        (await admin.PostAsync($"/api/members/{activeTuno.Id}/reminder", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsync($"/api/members/{leitao.Id}/activate", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsync($"/api/members/{activeTuno.Id}/activate", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "active with no way back to make: the old list offered no button");
        _factory.Push.Verify(p => p.SendToUserAsync(activeTuno.Id, It.IsAny<SendPushNotificationDto>()), Times.Never);

        (await admin.PostAsync($"/api/members/{almostBack.Id}/reminder", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Push.Verify(p => p.SendToUserAsync(almostBack.Id, It.IsAny<SendPushNotificationDto>()), Times.Once);

        (await admin.PostAsync($"/api/members/{almostBack.Id}/activate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UserAsync(almostBack.Id))!.IsRetired.Should().BeFalse();
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MemberStatuses.AsNoTracking().SingleAsync(s => s.UserId == almostBack.Id))
            .OverrideRetired.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_IsTheOldHardDelete_OwnerAnyMember_AdminLeitoesOnly()
    {
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);
        var leitao = await AddAsync(MemberCategory.Leitao);
        var tuno = await AddAsync(MemberCategory.Tuno);

        (await admin.GetFromJsonAsync<JsonElement>("/api/members?q=zzz-none")).GetProperty("canDeleteMembers").GetBoolean().Should().BeFalse();
        (await owner.GetFromJsonAsync<JsonElement>("/api/members?q=zzz-none")).GetProperty("canDeleteMembers").GetBoolean().Should().BeTrue();

        (await admin.DeleteAsync($"/api/members/{tuno.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await UserAsync(tuno.Id)).Should().NotBeNull();
        (await admin.DeleteAsync($"/api/members/{leitao.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UserAsync(leitao.Id)).Should().BeNull();
        (await owner.DeleteAsync($"/api/members/{tuno.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UserAsync(tuno.Id)).Should().BeNull();
        (await owner.DeleteAsync($"/api/members/{tuno.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MentorSearch_OffersTunosAndAbove_WithoutPrivateFields_AndLeavesOutTheMemberEdited()
    {
        var (admin, _) = await SignInAsync("Admin");
        var tag = Unique();
        var tuno = await AddAsync(MemberCategory.Tuno, yearTuno: 2010, nickname: $"Padrinho {tag}");
        await AddAsync(MemberCategory.Caloiro, nickname: $"Caloiro {tag}");

        var found = await admin.GetFromJsonAsync<JsonElement>($"/api/members/mentors?q={tag}");
        found.EnumerateArray().Select(m => m.GetProperty("id").GetString()).Should().Equal(tuno.Id);
        found[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "displayName", "fullName", "avatarUrl");
        (await admin.GetFromJsonAsync<JsonElement>($"/api/members/mentors?q={tag}&exclude={tuno.Id}")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task TheBlazorBridgeIsGone_AndTheReactFilesSayNothingAboutMigration()
    {
        var (admin, _) = await SignInAsync("Admin");
        foreach (var client in new[] { admin, Anonymous() })
        {
            var response = await client.GetAsync("/members/manage");
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Be("/members");
        }

        var web = typeof(RTUB.App).Assembly;
        web.GetType("RTUB.Pages.Members.Members").Should().BeNull("the Blazor /members/manage page was retired");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/members/manage", "/member/events", "/member/gallery", "/member/roles", "/hierarchy" });

        var src = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        foreach (var file in new[] { "Members.tsx", "MemberDialogs.tsx", "MemberManage.tsx", "membersApi.ts" })
        {
            var text = File.ReadAllText(Path.Combine(src, file));
            text.Should().NotContainAny(new[] { "migra", "Migra" }, file).And.NotContain("href=\"/members/manage\"", file);
        }
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

    private static async Task<Dictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray());
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

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.84.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "RTUB.Web")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}
