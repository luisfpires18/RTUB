// MemberAdminApiTests: EditAndInstruments tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

public partial class MemberAdminApiTests
{
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
}
