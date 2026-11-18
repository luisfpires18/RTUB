// MemberAdminApiTests: Access tests.
using System.Net;
using FluentAssertions;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

public partial class MemberAdminApiTests
{
    [Fact]
    public async Task Visitors_MembersAndMods_CannotUseTheTools_ExceptTheModsLimitedEdit()
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
            (HttpMethod.Post, $"/api/members/{tuno.Id}/reminder", null),
        };

        var anonymous = Anonymous();
        await AreaHttp.WithTokenAsync(anonymous);
        foreach (var (method, path, body) in calls)
        {
            (await SendAsync(anonymous, method, path, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {path}");
        }

        // Task 076: the limited member-information edit (mentors, instruments, /info) is Mod's too; every other tool is not.
        var info = new[] { "/api/members/mentors", "/instruments", "/info" };
        bool IsInfo(string path) => info.Any(path.Contains);
        var member = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Get, $"/api/members/{tuno.Id}/info", null),
            (HttpMethod.Put, $"/api/members/{tuno.Id}/info", new { city = "Braga", phoneNumber = "912", stamp = "x" }),
        };
        foreach (var role in new[] { "Member", "Mod" })
        {
            var (client, _) = await SignInAsync(role);
            await AreaHttp.WithTokenAsync(client);
            foreach (var (method, path, body) in calls.Concat(member))
            {
                var status = (await SendAsync(client, method, path, body)).StatusCode;
                // Hotfix 098: Mod creates members (POST /api/members) as well.
                if (role == "Mod" && (IsInfo(path) || (method == HttpMethod.Post && path == "/api/members")))
                {
                    status.Should().NotBe(HttpStatusCode.Forbidden, $"{role}: {method} {path} (076)");
                }
                else
                {
                    status.Should().Be(HttpStatusCode.Forbidden, $"{role}: {method} {path}");
                }
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
}
