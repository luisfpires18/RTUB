// NaipesApiTests: CommentsPlaysSettings tests.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Interfaces;
using Xunit;

namespace RTUB.Integration.Tests.Api;

public partial class NaipesApiTests
{
    // ---------- comments and plays ----------

    [Fact]
    public async Task Comments_AnyMemberWrites_TheAuthorOrAManagerDeletes()
    {
        var (writer, _) = await SignInAsync();
        var (other, _) = await SignInAsync();
        var (admin, _) = await SignInAsync("Admin");
        foreach (var client in new[] { writer, other, admin })
        {
            await AreaHttp.WithTokenAsync(client);
        }

        var id = await CreateAsync(other, "Acordeao");

        (await AreaHttp.Errors(await writer.PostAsJsonAsync($"/api/naipes/{id}/comments", new { text = "   " }))).Should().ContainKey("text");
        (await AreaHttp.Errors(await writer.PostAsJsonAsync($"/api/naipes/{id}/comments", new { text = new string('c', 1001) }))).Should().ContainKey("text");
        (await writer.PostAsJsonAsync("/api/naipes/999999/comments", new { text = "olá" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var comments = await AreaHttp.Json(await writer.PostAsJsonAsync($"/api/naipes/{id}/comments", new { text = "  Muito útil  " }));
        var comment = comments.EnumerateArray().Single();
        comment.GetProperty("text").GetString().Should().Be("Muito útil");
        comment.GetProperty("authorName").GetString().Should().NotBeNullOrWhiteSpace();
        comment.GetProperty("canDelete").GetBoolean().Should().BeTrue();
        comments.EnumerateArray().Select(c => c.EnumerateObject().Select(p => p.Name)).SelectMany(n => n)
            .Should().NotContain(new[] { "authorId", "email" }, "no account id or email reaches the page");
        var commentId = comment.GetProperty("id").GetInt32();

        var seenByOther = (await AreaHttp.Json(other, $"/api/naipes/{id}/comments")).EnumerateArray().Single();
        seenByOther.GetProperty("canDelete").GetBoolean().Should().BeFalse("the content's author is not the comment's");
        (await other.DeleteAsync($"/api/naipes/{id}/comments/{commentId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AreaHttp.Json(other, $"/api/naipes?instrument=Acordeao")).GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetInt32() == id).GetProperty("commentCount").GetInt32().Should().Be(1);

        var other2 = await CreateAsync(other, "Acordeao");
        (await writer.DeleteAsync($"/api/naipes/{other2}/comments/{commentId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "the comment belongs to another item");

        (await AreaHttp.Json(await writer.DeleteAsync($"/api/naipes/{id}/comments/{commentId}"))).GetArrayLength().Should().Be(0);

        var second = (await AreaHttp.Json(await writer.PostAsJsonAsync($"/api/naipes/{id}/comments", new { text = "Outra" }))).EnumerateArray().Single();
        (await AreaHttp.Json(admin, $"/api/naipes/{id}/comments")).EnumerateArray().Single().GetProperty("canDelete").GetBoolean().Should().BeTrue();
        (await AreaHttp.Json(await admin.DeleteAsync($"/api/naipes/{id}/comments/{second.GetProperty("id").GetInt32()}"))).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Plays_CountForVideosOnly()
    {
        var (member, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);
        var video = await CreateAsync(member, "Fagote");
        var image = await CreateAsync(member, "Fagote", video: false);

        (await member.PostAsync($"/api/naipes/{video}/plays", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await member.PostAsync($"/api/naipes/{video}/plays", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AreaHttp.Errors(await member.PostAsync($"/api/naipes/{image}/plays", null))).Should().ContainKey("id");
        (await member.PostAsync("/api/naipes/999999/plays", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var items = (await AreaHttp.Json(member, "/api/naipes?instrument=Fagote")).GetProperty("items").EnumerateArray().ToList();
        items.Single(i => i.GetProperty("id").GetInt32() == video).GetProperty("playCount").GetInt32().Should().Be(2);
        items.Single(i => i.GetProperty("id").GetInt32() == image).GetProperty("playCount").GetInt32().Should().Be(0);
    }

    // ---------- settings (/naipes/config) ----------

    [Fact]
    public async Task Settings_AreForAdminAndOwner_AndMembersAreRefused()
    {
        var (member, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);

        (await member.GetAsync("/api/naipes/config")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PutAsJsonAsync("/api/naipes/config/1", new { isVisible = false, sortOrder = 1 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PostAsync("/api/naipes/config/1/picture", Picture(Png, "image/png"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.DeleteAsync("/api/naipes/config/1/picture")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (admin, _) = await SignInAsync("Admin");
        var (owner, _) = await SignInAsync("Owner");
        foreach (var client in new[] { admin, owner })
        {
            var settings = (await AreaHttp.Json(client, "/api/naipes/config")).EnumerateArray().ToList();
            settings.Should().HaveCount(Enum.GetValues<RTUB.Core.Enums.InstrumentType>().Length, "every instrument has its setting");
            settings.Select(s => s.GetProperty("sortOrder").GetInt32()).Should().BeInAscendingOrder();
        }
    }

    [Fact]
    public async Task AnAdmin_HidesReordersAndPicturesAnInstrument_AndTheBoardFollows()
    {
        var (admin, _) = await SignInAsync("Admin");
        var (owner, _) = await SignInAsync("Owner");
        var (member, _) = await SignInAsync();
        await AreaHttp.WithTokenAsync(admin);
        await AreaHttp.WithTokenAsync(owner);
        await AreaHttp.WithTokenAsync(member);
        var saxofone = await SettingAsync(admin, "Saxofone");
        var id = saxofone.GetProperty("id").GetInt32();
        var order = saxofone.GetProperty("sortOrder").GetInt32();
        await CreateAsync(member, "Saxofone", title: "Embocadura");

        try
        {
            foreach (var bad in new int?[] { -1, 1000, null })
            {
                (await AreaHttp.Errors(await admin.PutAsJsonAsync($"/api/naipes/config/{id}", new { isVisible = true, sortOrder = bad })))
                    .Should().ContainKey("sortOrder");
            }

            (await admin.PutAsJsonAsync("/api/naipes/config/999999", new { isVisible = true, sortOrder = 1 })).StatusCode.Should().Be(HttpStatusCode.NotFound);

            var hidden = (await AreaHttp.Json(await admin.PutAsJsonAsync($"/api/naipes/config/{id}", new { isVisible = false, sortOrder = 0 })))
                .EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == id);
            hidden.GetProperty("isVisible").GetBoolean().Should().BeFalse();
            hidden.GetProperty("sortOrder").GetInt32().Should().Be(0);

            var board = await AreaHttp.Json(member, "/api/naipes?instrument=Saxofone");
            board.GetProperty("instruments").EnumerateArray().Select(i => i.GetProperty("value").GetString()).Should().NotContain("Saxofone");
            board.GetProperty("instrument").ValueKind.Should().Be(JsonValueKind.Null, "a hidden instrument is off the board");
            board.GetProperty("items").GetArrayLength().Should().Be(0);

            await AreaHttp.Json(await owner.PutAsJsonAsync($"/api/naipes/config/{id}", new { isVisible = true, sortOrder = 0 }));
            var instruments = (await AreaHttp.Json(member, "/api/naipes")).GetProperty("instruments").EnumerateArray()
                .Select(i => i.GetProperty("value").GetString()).ToList();
            instruments.IndexOf("Saxofone").Should().BeLessThan(instruments.IndexOf("Bandolim"), "order 0 comes before order 1");
            (await AreaHttp.Json(member, "/api/naipes?instrument=Saxofone")).GetProperty("items").GetArrayLength().Should().Be(1, "its content was kept");

            // Picture: JPEG, PNG or WebP by type and bytes, at most 5 MB; the old one is deleted.
            (await AreaHttp.Errors(await admin.PostAsync($"/api/naipes/config/{id}/picture", Picture(Svg, "image/svg+xml")))).Should().ContainKey("picture");
            (await AreaHttp.Errors(await admin.PostAsync($"/api/naipes/config/{id}/picture", Picture(Html, "image/png")))).Should().ContainKey("picture");
            var tooBig = new byte[5 * 1024 * 1024 + 1];
            Png.CopyTo(tooBig, 0);
            (await AreaHttp.Errors(await admin.PostAsync($"/api/naipes/config/{id}/picture", Picture(tooBig, "image/png"))))["picture"].Single()
                .Should().Contain("5 MB");

            var first = (await AreaHttp.Json(await admin.PostAsync($"/api/naipes/config/{id}/picture", Picture(Png, "image/png"))))
                .EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == id).GetProperty("pictureUrl").GetString();
            first.Should().StartWith("https://pub-test.r2.dev/naipes/");
            _factory.Media.Verify(m => m.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), "image/png", "typeconfig_Saxofone"), Times.Once);
            (await AreaHttp.Json(member, "/api/naipes")).GetProperty("instruments").EnumerateArray()
                .Single(i => i.GetProperty("value").GetString() == "Saxofone").GetProperty("pictureUrl").GetString().Should().Be(first);

            await AreaHttp.Json(await owner.PostAsync($"/api/naipes/config/{id}/picture", Picture(Jpeg, "image/jpeg")));
            _factory.Media.Verify(m => m.DeleteMediaAsync(first!), Times.Once, "the replaced picture is deleted, as before");

            var removed = (await AreaHttp.Json(await admin.DeleteAsync($"/api/naipes/config/{id}/picture")))
                .EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == id);
            removed.GetProperty("pictureUrl").ValueKind.Should().Be(JsonValueKind.Null);
        }
        finally
        {
            await admin.PutAsJsonAsync($"/api/naipes/config/{id}", new { isVisible = true, sortOrder = order });
        }
    }
}
