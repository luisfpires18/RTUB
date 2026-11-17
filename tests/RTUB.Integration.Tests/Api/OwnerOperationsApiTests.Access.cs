// OwnerOperationsApiTests: Access tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;
using Xunit;

namespace RTUB.Integration.Tests.Api;

public partial class OwnerOperationsApiTests
{
    // ---------- who gets in ----------

    [Fact]
    public async Task Visitors_GetNothing_AndThePagesSendThemToSignIn()
    {
        var target = await SeedUserAsync(subscribed: true);
        var log = await SeedLogAsync("visitor");
        var anonymous = AreaHttp.Anonymous(_factory, "10.71.0.1");
        await AreaHttp.WithTokenAsync(anonymous);

        AllAnswer(await ReadsAsync(anonymous), HttpStatusCode.Unauthorized);
        AllAnswer(await WritesAsync(anonymous, target.Id, log), HttpStatusCode.Unauthorized);

        foreach (var page in Pages)
        {
            var response = await anonymous.GetAsync(page);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect, page);
            response.Headers.Location!.ToString().Should().Be("/login?returnUrl=" + Uri.EscapeDataString(page));
        }

        await NothingChangedAsync(target.Id, log);
    }

    [Theory]
    [MemberData(nameof(NotOwners))]
    public async Task EveryoneButTheOwner_IsRefusedEveryCall_AndSeesTheRefusal(string who, string? role)
    {
        var member = await SignInAsync(role, who switch
        {
            "Leitão" => Who.Leitao,
            "Caloiro" => Who.Caloiro,
            "Tuno" => Who.Tuno,
            "Veterano" or "Admin Veterano" => Who.Veterano,
            _ => _ => { },
        });
        var target = await SeedUserAsync(subscribed: true);
        var log = await SeedLogAsync(who);

        var reads = await ReadsAsync(member);
        AllAnswer(reads, HttpStatusCode.Forbidden);
        AllAnswer(await WritesAsync(member, target.Id, log), HttpStatusCode.Forbidden);

        foreach (var page in Pages)
        {
            var response = await member.GetAsync(page);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} is Owner only", page);
            (await response.Content.ReadAsStringAsync()).Should().Contain("id=\"root\"", "the React page shows the refusal");
        }

        // No operational data in the refusals.
        (await member.GetStringAsyncAllowingErrors("/api/owner/emails")).Should().NotContain(target.Email!);
        (await member.GetStringAsyncAllowingErrors("/api/owner/users?q=" + target.UserName)).Should().NotContain(target.Email!);
        await NothingChangedAsync(target.Id, log);
        _factory.Email.Verify(e => e.SendAnnouncementEmailAsync(Refused, It.IsAny<string>(), It.IsAny<List<string>>(),
            It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<IProgress<EmailSendProgress>?>()), Times.Never);
        _factory.Push.Verify(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.Is<SendPushNotificationDto>(n => n.Title == Refused)),
            Times.Never);
    }

    [Fact]
    public async Task TheOwner_GetsTheReactPages_AndEveryRead_NeverCached()
    {
        var owner = await SignInAsync("Owner");

        foreach (var page in Pages)
        {
            var response = await owner.GetAsync(page);
            response.StatusCode.Should().Be(HttpStatusCode.OK, page);
            response.Headers.CacheControl!.NoCache.Should().BeTrue();
            var html = await response.Content.ReadAsStringAsync();
            html.Should().Contain("id=\"root\"").And.NotContain("blazor.web.js", "{0} is React", page);
        }

        AllAnswer(await ReadsAsync(owner), HttpStatusCode.OK);
    }

    [Fact]
    public async Task EveryWrite_NeedsTheAntiforgeryToken_EvenForTheOwner()
    {
        var owner = await SignInAsync("Owner");
        var target = await SeedUserAsync(subscribed: true);
        var log = await SeedLogAsync("csrf");
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        AllAnswer(await WritesAsync(owner, target.Id, log), HttpStatusCode.BadRequest);
        await NothingChangedAsync(target.Id, log);
    }
}
