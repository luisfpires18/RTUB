// EventsApiTests tests: prizes and videos.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
public partial class EventsApiTests
{
    // ---------- prizes (Admin/Owner, 012B) ----------

    [Fact]
    public async Task PrizeWrites_AreRefused_ForVisitorsMembersAndMods_AndWithoutTheAntiforgeryHeader()
    {
        var id = await AddEventAsync("Festival protegido", DateTime.Today.AddDays(-200), type: EventType.Festival);
        var prize = await AddTrophyAsync(id, "Intocável");
        var visitor = Anonymous();
        await WithTokenAsync(visitor);
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        var (adminWithoutToken, _) = await SignInAsync("Admin");

        foreach (var (client, expected) in new[] { (visitor, HttpStatusCode.Unauthorized), (member, HttpStatusCode.Forbidden), (mod, HttpStatusCode.Forbidden) })
        {
            (await client.GetAsync($"/api/events/{id}/prizes")).StatusCode.Should().Be(expected);
            (await client.PostAsJsonAsync($"/api/events/{id}/prizes", new { name = "Novo" })).StatusCode.Should().Be(expected);
            (await client.PutAsJsonAsync($"/api/events/{id}/prizes/{prize}", new { name = "Mudado" })).StatusCode.Should().Be(expected);
            (await client.DeleteAsync($"/api/events/{id}/prizes/{prize}")).StatusCode.Should().Be(expected);
        }

        (await adminWithoutToken.PostAsJsonAsync($"/api/events/{id}/prizes", new { name = "Sem token" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.PutAsJsonAsync($"/api/events/{id}/prizes/{prize}", new { name = "Sem token" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.DeleteAsync($"/api/events/{id}/prizes/{prize}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await TrophyNamesAsync(id)).Should().Equal("Intocável");
        var page = await member.GetFromJsonAsync<JsonElement>($"/api/events/{id}");
        page.GetProperty("canManagePrizes").GetBoolean().Should().BeFalse();
        page.GetProperty("event").GetProperty("trophies").EnumerateArray().Select(t => t.GetString()).Should().Equal("Intocável");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminOrOwner_ManagePrizes_AndThePublicViewFollows(string role)
    {
        var id = await AddEventAsync($"Festival com prémios ({role})", DateTime.Today.AddDays(-201), type: EventType.Festival);
        var (client, _) = await SignInAsync(role);
        await WithTokenAsync(client);
        var visitor = Anonymous();

        (await client.GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("canManagePrizes").GetBoolean().Should().BeTrue();
        (await client.GetFromJsonAsync<JsonElement>($"/api/events/{id}/prizes")).GetArrayLength().Should().Be(0);

        var empty = await client.PostAsJsonAsync($"/api/events/{id}/prizes", new { name = " " });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await empty.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("name", out _).Should().BeTrue();

        var added = await client.PostAsJsonAsync($"/api/events/{id}/prizes", new { name = "Melhor Tuna" });
        added.StatusCode.Should().Be(HttpStatusCode.OK);
        var prizeId = (await added.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetInt32();
        (await visitor.GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("event").GetProperty("trophies")[0].GetString()
            .Should().Be("Melhor Tuna", "visitors see a festival's prizes, as before");

        var renamed = await client.PutAsJsonAsync($"/api/events/{id}/prizes/{prizeId}", new { name = "Melhor Tuna 2026" });
        renamed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TrophyNamesAsync(id)).Should().Equal("Melhor Tuna 2026");

        var deleted = await client.DeleteAsync($"/api/events/{id}/prizes/{prizeId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deleted.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
        (await visitor.GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("event").GetProperty("trophies").GetArrayLength()
            .Should().Be(0, "the last prize gone, nothing is left to show");
        (await client.DeleteAsync($"/api/events/{id}/prizes/{prizeId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NewPrizes_AreRefused_ForAnUpcomingEvent()
    {
        var id = await AddEventAsync("Festival por vir", DateTime.Today.AddDays(304), type: EventType.Festival);
        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);

        (await owner.PostAsJsonAsync($"/api/events/{id}/prizes", new { name = "Cedo demais" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("canManagePrizes").GetBoolean().Should().BeFalse();
    }

    // ---------- videos (Admin/Owner, 012C) ----------

    [Fact]
    public async Task VideoWrites_AreRefused_ForVisitorsMembersAndMods_AndWithoutTheAntiforgeryHeader()
    {
        var id = await AddEventAsync("Arraial com vídeo protegido", DateTime.Today.AddDays(-210));
        var video = await AddEventVideoAsync(id, "Intocável", 0);
        var visitor = Anonymous();
        await WithTokenAsync(visitor);
        var (member, _) = await SignInAsync();
        await WithTokenAsync(member);
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        var (adminWithoutToken, _) = await SignInAsync("Admin");
        _factory.VideoStorage.Invocations.Clear();

        foreach (var (client, expected) in new[] { (visitor, HttpStatusCode.Unauthorized), (member, HttpStatusCode.Forbidden), (mod, HttpStatusCode.Forbidden) })
        {
            (await client.GetAsync($"/api/events/{id}/videos")).StatusCode.Should().Be(expected);
            (await client.PostAsync($"/api/events/{id}/videos", VideoForm())).StatusCode.Should().Be(expected);
            (await client.PutAsJsonAsync($"/api/events/{id}/videos/{video}", new { title = "Mudado" })).StatusCode.Should().Be(expected);
            (await client.PostAsJsonAsync($"/api/events/{id}/videos/reorder", new { videoIds = new[] { video } })).StatusCode.Should().Be(expected);
            (await client.DeleteAsync($"/api/events/{id}/videos/{video}")).StatusCode.Should().Be(expected);
        }

        (await adminWithoutToken.PostAsync($"/api/events/{id}/videos", VideoForm())).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.PutAsJsonAsync($"/api/events/{id}/videos/{video}", new { title = "x" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.PostAsJsonAsync($"/api/events/{id}/videos/reorder", new { videoIds = new[] { video } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await adminWithoutToken.DeleteAsync($"/api/events/{id}/videos/{video}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _factory.VideoStorage.Invocations.Should().BeEmpty("nothing is uploaded or deleted for them");
        (await member.GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("videos")[0].GetProperty("title").GetString()
            .Should().Be("Intocável", "members still watch it");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminOrOwner_UploadRenameReorderAndDelete_ThroughTheStorageFake(string role)
    {
        var id = await AddEventAsync($"Arraial filmado ({role})", DateTime.Today.AddDays(-211));
        var first = await AddEventVideoAsync(id, "Primeiro", 0);
        var (client, _) = await SignInAsync(role);
        await WithTokenAsync(client);
        var stored = $"https://pub-test.r2.dev/events/test/videos/{id}_new.mp4";
        _factory.VideoStorage.Setup(s => s.UploadVideoAsync(It.IsAny<Stream>(), "festa.mp4", "video/mp4", id)).ReturnsAsync(stored);

        var refused = await client.PostAsync($"/api/events/{id}/videos", VideoForm(contentType: "text/plain", name: "notas.txt"));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("file", out _).Should().BeTrue();
        (await client.PostAsync($"/api/events/{id}/videos", VideoForm(title: ""))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var uploaded = await client.PostAsync($"/api/events/{id}/videos", VideoForm(title: "A atuação toda"));
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await uploaded.Content.ReadFromJsonAsync<JsonElement>();
        list.EnumerateArray().Select(v => v.GetProperty("title").GetString()).Should().Equal("Primeiro", "A atuação toda");
        var second = list[1].GetProperty("id").GetInt32();
        list.GetRawText().Should().NotContain("pub-test", "the management list carries no URL");

        var page = await Anonymous().GetFromJsonAsync<JsonElement>($"/api/events/{id}");
        page.GetProperty("videos")[1].GetProperty("url").GetString().Should().Be(stored, "visitors watch it straight away");

        (await client.PutAsJsonAsync($"/api/events/{id}/videos/{second}", new { title = "Atuação completa" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var reordered = await client.PostAsJsonAsync($"/api/events/{id}/videos/reorder", new { videoIds = new[] { second, first } });
        reordered.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Anonymous().GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("videos").EnumerateArray()
            .Select(v => v.GetProperty("title").GetString()).Should().Equal("Atuação completa", "Primeiro");

        (await client.PostAsync($"/api/events/videos/{second}/plays", null)).StatusCode.Should().Be(HttpStatusCode.NoContent, "plays are still audited");

        (await client.DeleteAsync($"/api/events/{id}/videos/{second}")).StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.VideoStorage.Verify(s => s.DeleteVideoAsync(stored), Times.Once);
        var last = await client.DeleteAsync($"/api/events/{id}/videos/{first}");
        (await last.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
        (await Anonymous().GetFromJsonAsync<JsonElement>($"/api/events/{id}")).GetProperty("videos").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AVideoUpload_ToAnUpcomingEvent_OrFailingInStorage_LeavesNoRow()
    {
        var upcoming = await AddEventAsync("Arraial por filmar", DateTime.Today.AddDays(305));
        var past = await AddEventAsync("Arraial sem sorte", DateTime.Today.AddDays(-212));
        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);
        _factory.VideoStorage.Setup(s => s.UploadVideoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), past))
            .ThrowsAsync(new InvalidOperationException("R2 down"));

        (await owner.PostAsync($"/api/events/{upcoming}/videos", VideoForm())).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var failed = await owner.PostAsync($"/api/events/{past}/videos", VideoForm());
        failed.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await failed.Content.ReadAsStringAsync()).Should().NotContain("R2 down", "no internal detail reaches the browser");

        (await owner.GetFromJsonAsync<JsonElement>($"/api/events/{past}/videos")).GetArrayLength().Should().Be(0);
    }

    private static MultipartFormDataContent VideoForm(string contentType = "video/mp4", string name = "festa.mp4", string title = "Festa")
    {
        var file = new ByteArrayContent(new byte[] { 0, 0, 0, 24, (byte)'f', (byte)'t', (byte)'y', (byte)'p' });
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", name }, { new StringContent(title), "title" } };
    }

    private async Task<int> AddTrophyAsync(int eventId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var t = Trophy.Create(name, eventId);
        db.Trophies.Add(t);
        await db.SaveChangesAsync();
        return t.Id;
    }

    private async Task<List<string>> TrophyNamesAsync(int eventId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Trophies.AsNoTracking()
            .Where(t => t.EventId == eventId).OrderBy(t => t.Name).Select(t => t.Name).ToListAsync();
    }
}
