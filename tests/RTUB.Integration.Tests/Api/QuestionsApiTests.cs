using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// "Perguntas aos Órgãos Sociais" on /api/questions (task 031, was the Blazor /questions) through the real host: real
/// login, antiforgery, SQLite and the old QuestionService. Push is the recording fake of <see cref="EventsApiFactory"/>:
/// nothing is ever sent, but the calls the old service makes are seen. Every test scopes its own members and questions.
/// </summary>
public class QuestionsApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public QuestionsApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_GetNothing()
    {
        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        foreach (var path in new[] { "/api/questions", "/api/questions?closed=true", "/api/questions/recipients", "/api/questions/1" })
        {
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }

        (await anonymous.PostAsJsonAsync("/api/questions", new { title = "t", content = "conteúdo longo", recipientId = "x", position = "Magister" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/questions/1/replies", new { content = "olá" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/questions/1/close", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/questions/1/remind", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync("/api/questions/1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recipients_AreEveryOrgaosSociaisPosition_WithTheOldRoleText()
    {
        var (_, holder) = await SignInAsync();
        await SetPositionsAsync(holder.Id, Position.Magister, Position.PresidenteConselhoFiscal);
        var (_, nobody) = await SignInAsync();
        var (member, _) = await SignInAsync();

        var list = (await Json(member, "/api/questions/recipients")).EnumerateArray().ToList();
        var mine = list.Where(r => r.GetProperty("id").GetString() == holder.Id).ToList();
        mine.Select(r => r.GetProperty("position").GetString()).Should().BeEquivalentTo("Magister", "PresidenteConselhoFiscal");
        mine.Single(r => r.GetProperty("position").GetString() == "Magister").GetProperty("role").GetString().Should().Be("Direção - Magister");
        mine.Single(r => r.GetProperty("position").GetString() == "PresidenteConselhoFiscal").GetProperty("role").GetString()
            .Should().Be("Presidente do Conselho Fiscal", "only the Direção shows its group");
        list.Should().NotContain(r => r.GetProperty("id").GetString() == nobody.Id, "a member with no position cannot be asked");
        list.SelectMany(r => r.EnumerateObject().Select(p => p.Name)).Distinct()
            .Should().BeEquivalentTo("id", "displayName", "fullName", "avatarUrl", "position", "role");
    }

    [Fact]
    public async Task Asking_IsValidated_GoesOnlyToAHolderOfThatPosition_AndNotifiesThem()
    {
        var (_, recipient) = await SignInAsync();
        await SetPositionsAsync(recipient.Id, Position.Magister);
        var (leitao, leitaoUser) = await SignInAsync();
        await SetCategoryAsync(leitaoUser.Id, MemberCategory.Leitao);
        await WithTokenAsync(leitao);

        var errors = await Errors(await leitao.PostAsJsonAsync("/api/questions", new { title = "", content = "curta", recipientId = recipient.Id, position = "Secretario" }));
        errors.Keys.Should().BeEquivalentTo("title", "content", "recipientId");
        (await Errors(await leitao.PostAsJsonAsync("/api/questions", Ask(recipient.Id, title: new string('t', 101))))).Keys.Should().Equal("title");
        (await Errors(await leitao.PostAsJsonAsync("/api/questions", Ask(leitaoUser.Id)))).Keys.Should().Equal("recipientId");

        _factory.Push.Invocations.Clear();
        var created = await leitao.PostAsJsonAsync("/api/questions", Ask(recipient.Id));
        created.StatusCode.Should().Be(HttpStatusCode.Created, "any signed-in member asks, Leitões included, as before");
        var detail = await created.Content.ReadFromJsonAsync<JsonElement>();
        var question = detail.GetProperty("question");
        question.GetProperty("status").GetString().Should().Be("unanswered");
        question.GetProperty("isMine").GetBoolean().Should().BeTrue();
        question.GetProperty("canReply").GetBoolean().Should().BeFalse("the recipient answers first");
        question.GetProperty("canRemind").GetBoolean().Should().BeTrue();
        question.GetProperty("recipient").GetProperty("role").GetString().Should().Be("Direção - Magister");
        _factory.Push.Verify(p => p.SendToUserAsync(recipient.Id, It.IsAny<SendPushNotificationDto>()), Times.Once);
    }

    [Fact]
    public async Task Replies_TakeTurns_AndNobodyElseReplies()
    {
        var (author, recipient, stranger, id) = await QuestionAsync();

        (await author.PostAsJsonAsync($"/api/questions/{id}/replies", new { content = "Então?" })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "it is the recipient's turn");
        (await stranger.PostAsJsonAsync($"/api/questions/{id}/replies", new { content = "Eu sei!" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Errors(await recipient.PostAsJsonAsync($"/api/questions/{id}/replies", new { content = "   " }))).Keys.Should().Equal("content");

        var answered = await Json(await recipient.PostAsJsonAsync($"/api/questions/{id}/replies", new { content = "Às 21h." }));
        answered.GetProperty("question").GetProperty("status").GetString().Should().Be("answered");
        answered.GetProperty("question").GetProperty("canReply").GetBoolean().Should().BeFalse("now it is the author's turn");
        var reply = answered.GetProperty("replies").EnumerateArray().Single();
        reply.GetProperty("content").GetString().Should().Be("Às 21h.");
        reply.GetProperty("fromRecipient").GetBoolean().Should().BeTrue();

        (await recipient.PostAsJsonAsync($"/api/questions/{id}/replies", new { content = "Mais uma." })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var discussed = await Json(await author.PostAsJsonAsync($"/api/questions/{id}/replies", new { content = "E no sábado?" }));
        discussed.GetProperty("question").GetProperty("status").GetString().Should().Be("inDiscussion");
        discussed.GetProperty("replies").GetArrayLength().Should().Be(2);

        var seen = await Json(stranger, $"/api/questions/{id}");
        seen.GetProperty("replies").GetArrayLength().Should().Be(2, "every member reads every question, as before");
        seen.GetProperty("question").GetProperty("canReply").GetBoolean().Should().BeFalse();
        seen.GetProperty("question").GetProperty("isMine").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CloseRemindAndDelete_AreTheAuthors_AndAClosedQuestionTakesNoReplies()
    {
        var (author, recipient, stranger, id) = await QuestionAsync();

        foreach (var other in new[] { recipient, stranger })
        {
            (await other.PostAsync($"/api/questions/{id}/close", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await other.PostAsync($"/api/questions/{id}/remind", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await other.DeleteAsync($"/api/questions/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await author.PostAsync($"/api/questions/{id}/remind", null)).StatusCode.Should().Be(HttpStatusCode.NoContent, "the recipient owes an answer");
        await Json(await recipient.PostAsJsonAsync($"/api/questions/{id}/replies", new { content = "Respondido." }));
        (await author.PostAsync($"/api/questions/{id}/remind", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "now the author owes the reply");

        var closed = await Json(await author.PostAsync($"/api/questions/{id}/close", null));
        closed.GetProperty("question").GetProperty("status").GetString().Should().Be("closed");
        (await Errors(await author.PostAsJsonAsync($"/api/questions/{id}/replies", new { content = "Afinal…" }))).Keys.Should().Equal("content");
        (await author.PostAsync($"/api/questions/{id}/remind", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Json(await author.PostAsync($"/api/questions/{id}/close", null))).GetProperty("question").GetProperty("status").GetString()
            .Should().Be("closed", "closing twice is harmless");

        (await author.DeleteAsync($"/api/questions/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await author.GetAsync($"/api/questions/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "deleted questions are gone for everyone");
        (await author.DeleteAsync($"/api/questions/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Lists_SplitOpenAndClosed_ByLatestActivity_WithSearchRecipientFilterAndPages()
    {
        var tag = "qst" + Guid.NewGuid().ToString("N")[..8];
        var (author, _, _, first) = await QuestionAsync($"{tag} primeira");
        var recipientId = await RecipientIdAsync(first);
        var second = (await Json(await author.PostAsJsonAsync("/api/questions", Ask(recipientId, title: $"{tag} segunda")))).GetProperty("question").GetProperty("id").GetInt32();
        var third = (await Json(await author.PostAsJsonAsync("/api/questions", Ask(recipientId, title: $"{tag} terceira")))).GetProperty("question").GetProperty("id").GetInt32();
        await Json(await author.PostAsync($"/api/questions/{second}/close", null));

        var open = await Json(author, $"/api/questions?q={tag}");
        Ids(open).Should().Equal(third, first);
        open.GetProperty("total").GetInt32().Should().Be(2);
        Ids(await Json(author, $"/api/questions?closed=true&q={tag}")).Should().Equal(second);

        var paged = await Json(author, $"/api/questions?q={tag}&pageSize=1&page=2");
        Ids(paged).Should().Equal(first);
        paged.GetProperty("total").GetInt32().Should().Be(2);
        paged.GetProperty("pageSize").GetInt32().Should().Be(1);
        (await Json(author, $"/api/questions?q={tag}&pageSize=500")).GetProperty("pageSize").GetInt32().Should().Be(50, "at most 50 a page");

        var byRecipient = await Json(author, $"/api/questions?q={tag}&recipient={Uri.EscapeDataString(recipientId)}");
        byRecipient.GetProperty("total").GetInt32().Should().Be(2);
        (await Json(author, $"/api/questions?q={tag}&recipient=nobody")).GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryHeader()
    {
        var (_, recipient) = await SignInAsync();
        await SetPositionsAsync(recipient.Id, Position.Magister);
        var (member, _) = await SignInAsync();

        (await member.PostAsJsonAsync("/api/questions", Ask(recipient.Id))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.PostAsync("/api/questions/1/close", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.DeleteAsync("/api/questions/1")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- helpers ----------

    private static object Ask(string recipientId, string title = "Quando é o ensaio?", string position = "Magister") =>
        new { title, content = "Gostava de saber a hora do próximo ensaio.", recipientId, position };

    /// <summary>An author with a question to a Magister (the recipient), and a third member; all with tokens.</summary>
    private async Task<(HttpClient Author, HttpClient Recipient, HttpClient Stranger, int Id)> QuestionAsync(string title = "Quando é o ensaio?")
    {
        var (recipient, recipientUser) = await SignInAsync();
        await SetPositionsAsync(recipientUser.Id, Position.Magister);
        var (author, _) = await SignInAsync();
        var (stranger, _) = await SignInAsync();
        foreach (var client in new[] { recipient, author, stranger })
        {
            await WithTokenAsync(client);
        }

        var created = await author.PostAsJsonAsync("/api/questions", Ask(recipientUser.Id, title));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("question").GetProperty("id").GetInt32();
        return (author, recipient, stranger, id);
    }

    private async Task<string> RecipientIdAsync(int questionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RTUB.Application.Data.ApplicationDbContext>();
        return (await db.Questions.FindAsync(questionId))!.AssignedMemberId;
    }

    private static IEnumerable<int> Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(q => q.GetProperty("id").GetInt32());

    private async Task SetPositionsAsync(string userId, params Position[] positions)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        user!.Positions = positions.ToList();
        (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
    }

    private async Task SetCategoryAsync(string userId, MemberCategory category)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        user!.Categories = [category];
        (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
    }

    private static async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Dictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray());
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"qst{Guid.NewGuid():N}"[..20], $"10.43.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.44.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
