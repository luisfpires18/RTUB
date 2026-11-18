// NewsApiTests: Manage tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace RTUB.Integration.Tests.Api;

public partial class NewsApiTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Member")]
    public async Task VisitorsAndMembers_CannotManage(string? role)
    {
        var id = await AddAsync($"{Tag()} intocável", "Fica igual.", DateTime.UtcNow, null);
        var client = role is null ? Anonymous() : (await SignInAsync(role)).Client;
        await AreaHttp.WithTokenAsync(client);
        var expected = role is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;

        (await client.PostAsJsonAsync("/api/news", Input("Novo", "Texto", publish: true))).StatusCode.Should().Be(expected);
        (await client.PutAsJsonAsync($"/api/news/{id}", Input("Mudado", "Mudado"))).StatusCode.Should().Be(expected);
        (await client.PostAsync($"/api/news/{id}/unpublish", null)).StatusCode.Should().Be(expected);
        (await client.PostAsync($"/api/news/{id}/publish", null)).StatusCode.Should().Be(expected);
        (await client.DeleteAsync($"/api/news/{id}")).StatusCode.Should().Be(expected);

        var post = await PostAsync(id);
        post!.Body.Should().Be("Fica igual.");
        post.Title.Should().EndWith("intocável");
        post.PublishedAt.Should().NotBeNull();

        var feed = await AreaHttp.Json(client, "/api/news");
        feed.GetProperty("canManage").GetBoolean().Should().BeFalse();
        feed.GetProperty("drafts").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AMod_WritesEditsPublishesAndUnpublishes_ButCannotDelete()
    {
        var tag = Tag();
        var (mod, _) = await SignInAsync("Mod");
        await AreaHttp.WithTokenAsync(mod);
        var published = await AddAsync($"{tag} no ar", "Fica igual.", DateTime.UtcNow, null);
        var existingDraft = await AddAsync($"{tag} rascunho do admin", "Rascunho.", null, null);

        var created = await mod.PostAsJsonAsync("/api/news", Input($"{tag} do mod", "Texto do mod.", publish: false));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var feed = await AreaHttp.Json(mod, "/api/news");
        (feed.GetProperty("canWrite").GetBoolean(), feed.GetProperty("canManage").GetBoolean()).Should().Be((true, false));
        Tagged(feed.GetProperty("drafts"), tag).Should().HaveCount(2, "a Mod sees every draft");

        (await mod.PutAsJsonAsync($"/api/news/{id}", Input($"{tag} do mod", "Texto corrigido."))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await mod.PutAsJsonAsync($"/api/news/{published}", Input($"{tag} no ar", "Corrigido."))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PublishAsync(mod, id)).Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        (await mod.PostAsync($"/api/news/{published}/unpublish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PostAsync(published))!.PublishedAt.Should().BeNull();

        var live = await mod.PostAsJsonAsync("/api/news", Input($"{tag} direta", "Publicada pelo Mod.", publish: true));
        (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("publishedAt").ValueKind.Should().Be(JsonValueKind.String);

        (await mod.DeleteAsync($"/api/news/{existingDraft}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await PostAsync(existingDraft)).Should().NotBeNull();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminAndOwner_WriteDrafts_Publish_Unpublish_Republish_Edit_AndDelete(string role)
    {
        var tag = Tag();
        var (manager, me) = await SignInAsync(role);
        await AreaHttp.WithTokenAsync(manager);
        var anonymous = Anonymous();

        // A draft: only managers see it, with the author's display name.
        var created = await manager.PostAsJsonAsync("/api/news", Input($"  {tag} anúncio  ", "  Ensaio aberto na sexta.\nTragam capa.  "));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetInt32();
        created.Headers.Location!.ToString().Should().Be($"/api/news/{id}");
        draft.GetProperty("title").GetString().Should().Be($"{tag} anúncio", "title and text are trimmed");
        draft.GetProperty("body").GetString().Should().Be("Ensaio aberto na sexta.\nTragam capa.");
        draft.GetProperty("publishedAt").ValueKind.Should().Be(JsonValueKind.Null);
        draft.GetProperty("authorName").GetString().Should().Contain(me.FirstName);
        (await PostAsync(id))!.AuthorId.Should().Be(me.Id);

        var mine = await AreaHttp.Json(manager, "/api/news");
        mine.GetProperty("canManage").GetBoolean().Should().BeTrue();
        Tagged(mine.GetProperty("drafts"), tag).Should().ContainSingle();
        Tagged(mine.GetProperty("posts"), tag).Should().BeEmpty();
        Tagged((await AreaHttp.Json(anonymous, "/api/news")).GetProperty("posts"), tag).Should().BeEmpty();

        // Publish: on top of the public feed. Publishing again leaves the date alone.
        var first = await PublishAsync(manager, id);
        Tagged((await AreaHttp.Json(anonymous, "/api/news")).GetProperty("posts"), tag).Should().ContainSingle();
        Tagged((await AreaHttp.Json(anonymous, "/api/news")).GetProperty("posts"), tag)[0].GetProperty("id").GetInt32().Should().Be(id);
        (await PublishAsync(manager, id)).Should().Be(first);

        // Another post goes above it; unpublishing takes it off the feed; re-publishing puts it back on top with a new date.
        await AddAsync($"{tag} outra", "Outra", DateTime.UtcNow, null);
        var unpublished = await Json(await manager.PostAsync($"/api/news/{id}/unpublish", null));
        unpublished.GetProperty("publishedAt").ValueKind.Should().Be(JsonValueKind.Null);
        Tagged((await AreaHttp.Json(anonymous, "/api/news")).GetProperty("posts"), tag).Select(p => p.GetProperty("id").GetInt32()).Should().NotContain(id);
        var again = await PublishAsync(manager, id);
        again.Should().BeAfter(first);
        Tagged((await AreaHttp.Json(anonymous, "/api/news")).GetProperty("posts"), tag)[0].GetProperty("id").GetInt32().Should().Be(id);

        // Edit keeps the state; a blank title means none.
        var edited = await Json(await manager.PutAsJsonAsync($"/api/news/{id}", Input("   ", "Texto corrigido", publish: false)));
        edited.GetProperty("title").ValueKind.Should().Be(JsonValueKind.Null);
        edited.GetProperty("body").GetString().Should().Be("Texto corrigido");
        edited.GetProperty("publishedAt").GetDateTime().Should().Be(again, "editing never unpublishes");

        // Delete is permanent.
        (await manager.DeleteAsync($"/api/news/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostAsync(id)).Should().BeNull();
        (await manager.DeleteAsync($"/api/news/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await manager.PutAsJsonAsync($"/api/news/{id}", Input("x", "y"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await manager.PostAsync($"/api/news/{id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Created as published straight away ("Publicar" in the form).
        var live = await manager.PostAsJsonAsync("/api/news", Input($"{tag} direta", "Já publicada", publish: true));
        live.StatusCode.Should().Be(HttpStatusCode.Created);
        (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("publishedAt").ValueKind.Should().Be(JsonValueKind.String);

        _factory.Push.Invocations.Should().BeEmpty("publishing sends nothing");
        _factory.Email.Invocations.Should().BeEmpty("publishing sends nothing");
    }

    [Fact]
    public async Task EveryWrite_NeedsTheAntiforgeryToken()
    {
        var id = await AddAsync($"{Tag()} protegida", "Sem token não muda.", DateTime.UtcNow, null);
        var (admin, _) = await SignInAsync("Admin");

        (await admin.PostAsJsonAsync("/api/news", Input("x", "y"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync($"/api/news/{id}", Input("x", "y"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync($"/api/news/{id}/unpublish", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync($"/api/news/{id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.DeleteAsync($"/api/news/{id}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var post = await PostAsync(id);
        (post!.Body, post.PublishedAt.HasValue).Should().Be(("Sem token não muda.", true));
    }

    [Fact]
    public async Task Input_IsValidated_TextRequired_AndBothLengthsCapped()
    {
        var (admin, _) = await SignInAsync("Owner");
        await AreaHttp.WithTokenAsync(admin);
        var before = await CountAsync();

        (await AreaHttp.Errors(await admin.PostAsJsonAsync("/api/news", Input("Título", "   ")))).Should().ContainKey("body");
        (await AreaHttp.Errors(await admin.PostAsJsonAsync("/api/news", new { title = "Só título" }))).Should().ContainKey("body");
        (await AreaHttp.Errors(await admin.PostAsJsonAsync("/api/news", Input("Título", new string('a', 5001))))).Should().ContainKey("body");
        (await AreaHttp.Errors(await admin.PostAsJsonAsync("/api/news", Input(new string('t', 151), "Texto")))).Should().ContainKey("title");
        (await CountAsync()).Should().Be(before, "nothing invalid is stored");

        var longest = await admin.PostAsJsonAsync("/api/news", Input(new string('t', 150), new string('a', 5000)));
        longest.StatusCode.Should().Be(HttpStatusCode.Created, "150 and 5000 characters are the limits, inclusive");

        (await Anonymous().GetAsync("/api/news?page=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
