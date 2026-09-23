using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Interfaces;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Logistics authorization through the real host: real Identity roles, the real login form and
/// the prerendered page. Complements the bUnit tests, which cannot prove that role names match
/// what Identity actually issues, or that endpoint authorization still keeps Mod out of
/// Admin-only pages.
/// </summary>
public class LogisticsAuthorizationIntegrationTests : IntegrationTestBase
{
    public LogisticsAuthorizationIntegrationTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("Admin", "10.40.0.1")]
    [InlineData("Mod", "10.40.0.2")]
    public async Task Manager_GetsTheBoardManagementControls(string role, string ip)
    {
        var boardId = await SeedBoardWithOneListAsync();
        var (client, _) = await CookieTestSession.SignInAsync(Factory, $"logistics-{role.ToLowerInvariant()}", ip, role);

        var html = await GetOkAsync(client, $"/logistics/{boardId}");

        html.Should().Contain("Adicionar Lista");
        html.Should().Contain("title=\"Adicionar Cartão\"");
    }

    [Fact]
    public async Task Member_GetsNoBoardManagementControls()
    {
        var boardId = await SeedBoardWithOneListAsync();
        var (client, _) = await CookieTestSession.SignInAsync(Factory, "logistics-member", "10.40.0.3", "Member");

        var html = await GetOkAsync(client, $"/logistics/{boardId}");

        html.Should().Contain("Material", "the member still reads the board");
        html.Should().NotContain("Adicionar Lista");
        html.Should().NotContain("title=\"Adicionar Cartão\"");
    }

    /// <summary>
    /// Mod parity is Logistics-only. <c>/emails</c> is <c>[Authorize(Roles = "Admin")]</c>; Admin
    /// reaching it is the control that makes the Mod refusal meaningful.
    /// </summary>
    [Fact]
    public async Task Mod_IsStillRefusedAnAdminOnlyPage()
    {
        var (admin, _) = await CookieTestSession.SignInAsync(Factory, "emails-admin", "10.40.0.4", "Admin");
        var (mod, _) = await CookieTestSession.SignInAsync(Factory, "emails-mod", "10.40.0.5", "Mod");

        (await admin.GetAsync("/emails")).StatusCode.Should().Be(HttpStatusCode.OK);

        var refused = await mod.GetAsync("/emails");
        refused.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Forbidden);
        if (refused.StatusCode == HttpStatusCode.Redirect)
        {
            refused.Headers.Location!.ToString().Should().Contain("/login", "AccessDeniedPath is /login");
        }
    }

    private async Task<int> SeedBoardWithOneListAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var boards = scope.ServiceProvider.GetRequiredService<ILogisticsBoardService>();
        var lists = scope.ServiceProvider.GetRequiredService<ILogisticsListService>();

        var board = await boards.CreateBoardAsync($"Arraial {Guid.NewGuid():N}");
        // No card on purpose: rendering one asks R2 for its attachment count, and the test host's
        // R2 credentials are placeholders. Card-level gates are covered by the bUnit tests.
        await lists.CreateListAsync("Material", board.Id, 0);
        return board.Id;
    }

    private static async Task<string> GetOkAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"{path} should render for a signed-in member");
        // Decoded because Blazor writes non-ASCII attribute text as entities (Cart&#xE3;o), which
        // would make every NotContain below pass for the wrong reason.
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
}
