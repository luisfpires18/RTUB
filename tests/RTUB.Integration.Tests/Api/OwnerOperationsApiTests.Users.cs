// OwnerOperationsApiTests: Users tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests.Api;

public partial class OwnerOperationsApiTests
{
    // ---------- /users ----------

    [Fact]
    public async Task Users_SearchesPagesAndShowsAnAccount()
    {
        var owner = await SignInAsync("Owner");
        var target = await SeedUserAsync(subscribed: true);

        var page = await AreaHttp.Json(owner, $"/api/owner/users?q={target.UserName}");
        var card = page.GetProperty("items").EnumerateArray().Single();
        card.GetProperty("id").GetString().Should().Be(target.Id);
        card.GetProperty("email").GetString().Should().Be(target.Email);
        card.GetProperty("role").GetString().Should().Be("Sem Função");
        page.GetProperty("pageSize").GetInt32().Should().Be(20);
        (await AreaHttp.Json(owner, "/api/owner/users?pageSize=7")).GetProperty("pageSize").GetInt32().Should().Be(20, "only the offered sizes");
        (await AreaHttp.Json(owner, $"/api/owner/users?q={target.UserName}&staff=true")).GetProperty("total").GetInt32().Should().Be(0, "no role is not staff");
        (await AreaHttp.Json(owner, "/api/owner/users?staff=true&pageSize=100")).GetProperty("items").EnumerateArray()
            .Select(c => c.GetProperty("role").GetString()).Should().NotBeEmpty().And.OnlyContain(r => r == "Owner" || r == "Admin" || r == "Mod");

        var detail = await AreaHttp.Json(owner, $"/api/owner/users/{target.Id}");
        detail.GetProperty("subscribed").GetBoolean().Should().BeTrue();
        detail.GetProperty("locked").GetBoolean().Should().BeFalse();
        detail.TryGetProperty("passwordHash", out _).Should().BeFalse("no secrets leave the server");
        (await owner.GetAsync("/api/owner/users/nao-existe")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Users_TheOwnerChangesARole_OwnerAlsoGettingAdmin()
    {
        var owner = await SignInAsync("Owner");
        var target = await SeedUserAsync(subscribed: true, role: "Member");

        (await AreaHttp.Errors(await owner.PutAsJsonAsync($"/api/owner/users/{target.Id}/role", new { role = "Rei" }))).Keys.Should().Equal("role");
        (await owner.PutAsJsonAsync("/api/owner/users/nao-existe/role", new { role = "Mod" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await AreaHttp.Json(await owner.PutAsJsonAsync($"/api/owner/users/{target.Id}/role", new { role = "Mod" }))).GetProperty("role").GetString().Should().Be("Mod");
        (await RolesAsync(target.Id)).Should().BeEquivalentTo("Mod");

        (await AreaHttp.Json(await owner.PutAsJsonAsync($"/api/owner/users/{target.Id}/role", new { role = "Owner" }))).GetProperty("role").GetString().Should().Be("Owner");
        (await RolesAsync(target.Id)).Should().BeEquivalentTo("Owner", "Admin");

        (await AreaHttp.Json(await owner.PutAsJsonAsync($"/api/owner/users/{target.Id}/role", new { role = "Member" }))).GetProperty("role").GetString().Should().Be("Member");
        (await RolesAsync(target.Id)).Should().BeEquivalentTo("Member");
    }

    [Fact]
    public async Task Users_TheOwnerUnlocksAnAccount_AndSyncsTheStatuses()
    {
        var owner = await SignInAsync("Owner");
        var target = await SeedUserAsync(subscribed: true);
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = (await users.FindByIdAsync(target.Id))!;
            await users.SetLockoutEnabledAsync(stored, true);
            await users.SetLockoutEndDateAsync(stored, DateTimeOffset.UtcNow.AddHours(1));
            await users.AccessFailedAsync(stored);
        }

        (await AreaHttp.Json(owner, $"/api/owner/users/{target.Id}")).GetProperty("locked").GetBoolean().Should().BeTrue();
        var unlocked = await AreaHttp.Json(await owner.PostAsync($"/api/owner/users/{target.Id}/unlock", null));
        unlocked.GetProperty("locked").GetBoolean().Should().BeFalse();
        var stored2 = await StoredUserAsync(target.Id);
        stored2.LockoutEnd.Should().BeNull();
        stored2.AccessFailedCount.Should().Be(0);

        (await AreaHttp.Json(await owner.PostAsync("/api/owner/users/sync-status", null))).GetProperty("updated").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }
}
