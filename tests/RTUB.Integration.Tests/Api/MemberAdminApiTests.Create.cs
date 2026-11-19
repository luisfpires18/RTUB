// MemberAdminApiTests: Create tests.
using System.Net;
using FluentAssertions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

public partial class MemberAdminApiTests
{
    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task Create_AsTheOldForm_UsernameRoleCategoriesInstruments_AndTheWelcomeEmail(string role)
    {
        var (admin, _) = await SignInAsync(role);
        await AreaHttp.WithTokenAsync(admin);
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
        await AreaHttp.WithTokenAsync(admin);
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

    [Theory]
    [InlineData("Leitao", false)]
    [InlineData("Caloiro", true)]
    [InlineData("Tuno", true)]
    public async Task Create_StartsActiveOnlyAsALeitao(string category, bool expectedRetired)
    {
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
        var email = Unique() + "@test.com";
        (await admin.PostAsJsonAsync("/api/members", Valid(category, "Init " + Unique(), email))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await UserByEmailAsync(email))!.IsRetired.Should().Be(expectedRetired, "a Leitão is new to the Tuna; anyone else is an old member being digitised");
    }

    [Fact]
    public async Task Create_ALeitaoWithoutDates_StartsTheSixMonthsInTheCreationMonth()
    {
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
        var email = Unique() + "@test.com";
        var before = DateTime.UtcNow;

        (await admin.PostAsJsonAsync("/api/members", Valid("Leitao", "Novo " + Unique(), email))).StatusCode.Should().Be(HttpStatusCode.Created);

        var created = (await UserByEmailAsync(email))!;
        var after = DateTime.UtcNow;
        new[] { (before.Year, before.Month), (after.Year, after.Month) }.Should().Contain((created.YearLeitao!.Value, created.MonthLeitao!.Value));
        created.IsRetired.Should().BeFalse();
    }

    [Fact]
    public async Task ProfileAndListReads_NeverChangeTheStatus_OnlyTheAttendanceTriggerDoes()
    {
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
        // Active with nothing since 2020: a reconciliation would reform them.
        var stale = await AddAsync(MemberCategory.Tuno, yearTuno: 2020);
        // Reformado with confirmed rehearsals three and two months ago, and a pending one last month.
        var returning = await AddAsync(MemberCategory.Tuno, yearTuno: 2015, retired: true);
        var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await AttendAsync(returning.Id, thisMonth.AddMonths(-3).AddDays(9));
        await AttendAsync(returning.Id, thisMonth.AddMonths(-2).AddDays(9));
        var (rehearsalId, pendingId) = await PendingAttendanceAsync(returning.Id, thisMonth.AddMonths(-1).AddDays(9));

        for (var n = 0; n < 2; n++)
        {
            (await admin.GetAsync($"/api/members/{stale.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.GetAsync($"/api/members/{returning.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.GetAsync($"/api/members/active?q={Uri.EscapeDataString(stale.Nickname!)}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.GetAsync("/api/members")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await UserAsync(stale.Id))!.IsRetired.Should().BeFalse("reads display, they do not reconcile");
        (await UserAsync(returning.Id))!.IsRetired.Should().BeTrue();
        (await StatusTransitionsAsync(stale.Id, returning.Id)).Should().BeEmpty();
        (await ProjectionCountAsync(stale.Id, returning.Id)).Should().Be(0);

        // Confirming the third consecutive month is a reconcile trigger.
        (await admin.PostAsync($"/api/rehearsals/{rehearsalId}/attendances/{pendingId}/approve", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await UserAsync(returning.Id))!.IsRetired.Should().BeFalse();
        (await StatusTransitionsAsync(returning.Id)).Should().ContainSingle().Which.Action.Should().Be("Activated");
    }

    [Fact]
    public async Task Create_Validates_AsTheOldForm_AndSendsNothingWhenRefused()
    {
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
        var existing = await AddAsync(MemberCategory.Tuno);
        var leitao = await AddAsync(MemberCategory.Leitao);

        var empty = await admin.PostAsJsonAsync("/api/members", new { });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AreaHttp.Errors(empty)).Keys.Should().Contain(new[] { "firstName", "lastName", "nickname", "phoneNumber", "email", "category" });

        var tooLong = Valid("Tuno", new string('a', 81), Unique() + "@test.com");
        (await AreaHttp.Errors(await admin.PostAsJsonAsync("/api/members", tooLong)))["nickname"].Should().ContainMatch("*80*");

        var sameUsername = Valid("Tuno", existing.Nickname, Unique() + "@test.com");
        (await AreaHttp.Errors(await admin.PostAsJsonAsync("/api/members", sameUsername)))["nickname"].Single().Should().StartWith("Já existe um utilizador com o nome de tuna");

        var sameEmail = Valid("Tuno", "Outro " + Unique(), existing.Email);
        (await AreaHttp.Errors(await admin.PostAsJsonAsync("/api/members", sameEmail)))["email"].Single().Should().StartWith("Já existe um utilizador com o email");

        var badInstrument = Valid("Tuno", "I " + Unique(), Unique() + "@test.com") with { Instruments = new[] { new MemberInstrumentInput("Kazoo", true) } };
        (await AreaHttp.Errors(await admin.PostAsJsonAsync("/api/members", badInstrument))).Keys.Should().Contain("instruments");

        var leitaoMentor = Valid("Caloiro", "M " + Unique(), Unique() + "@test.com") with { MentorId = leitao.Id };
        (await AreaHttp.Errors(await admin.PostAsJsonAsync("/api/members", leitaoMentor))).Keys.Should().Contain("mentorId", "only Tunos and above are padrinhos");

        var badDate = Valid("Tuno", "D " + Unique(), Unique() + "@test.com") with { YearTuno = DateTime.Now.Year + 1, MonthTuno = 13 };
        (await AreaHttp.Errors(await admin.PostAsJsonAsync("/api/members", badDate))).Keys.Should().Contain("tuno");

        _factory.Email.Verify(e => e.SendWelcomeEmailAsync(It.IsAny<string>(), It.Is<string>(m => m == existing.Email), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
