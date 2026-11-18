// ShopApiTests: Reservations tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;

namespace RTUB.Integration.Tests.Api;

public partial class ShopApiTests
{
    [Fact]
    public async Task Reservations_FollowTheOldRules_AndNeverChangeTheStock()
    {
        var tag = Tag();
        var tee = await AddAsync($"{tag} T-shirt", "Roupa", stock: 2, isPublic: false);
        var pin = await AddAsync($"{tag} Pin", "Pin", stock: 5);
        var gone = await AddAsync($"{tag} Esgotado", "Pin", stock: 0, isPublic: false);
        var (member, me) = await SignInAsync("Member");
        await AreaHttp.WithTokenAsync(member);

        (await AreaHttp.Errors(await member.PostAsJsonAsync($"/api/shop/products/{pin}/reservations", new { hasSizes = false }))).Keys.Should().Equal("product");
        (await AreaHttp.Errors(await member.PostAsJsonAsync($"/api/shop/products/{gone}/reservations", new { hasSizes = false }))).Keys.Should().Equal("product");
        (await AreaHttp.Errors(await member.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = true }))).Keys.Should().Equal("size");
        (await AreaHttp.Errors(await member.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = true, size = "XXXXL" }))).Keys.Should().Equal("size");
        (await AreaHttp.Errors(await member.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = false, displayName = new string('d', 201) })))
            .Keys.Should().Equal("displayName");
        (await member.PostAsJsonAsync("/api/shop/products/999999/reservations", new { hasSizes = false })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var created = await Json(await member.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = true, size = "M", displayName = " Zé " }));
        (created.GetProperty("username").GetString(), created.GetProperty("size").GetString(), created.GetProperty("displayName").GetString())
            .Should().Be((me.UserName, "M", "Zé"));
        created.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "username", "displayName", "size", "createdAt");
        (await AreaHttp.Errors(await member.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = false })))["product"]
            .Single().Should().Be("Já existe uma reserva para este produto");
        (await ProductAsync(tee))!.Stock.Should().Be(2, "reserving never changed the stock");

        var card = (await AreaHttp.Json(member, $"/api/shop?q={tag}")).GetProperty("products").EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == tee);
        card.GetProperty("myReservation").GetProperty("id").GetInt32().Should().Be(created.GetProperty("id").GetInt32());
        card.GetProperty("canReserve").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_IsTheMembersOwn_AndModAdminAndOwnerSeeAndDeleteEveryReservation()
    {
        var tee = await AddAsync($"{Tag()} Casaco", "Roupa", stock: 4, isPublic: false);
        var (alice, _) = await SignInAsync("Member");
        await AreaHttp.WithTokenAsync(alice);
        var (bob, _) = await SignInAsync("Mod");
        await AreaHttp.WithTokenAsync(bob);
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
        var (owner, _) = await SignInAsync("Owner");
        await AreaHttp.WithTokenAsync(owner);

        var aliceReservation = (await Json(await alice.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = false }))).GetProperty("id").GetInt32();
        var bobReservation = (await Json(await bob.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = false }))).GetProperty("id").GetInt32();

        (await alice.GetAsync($"/api/shop/products/{tee}/reservations")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "a plain member never sees the list");
        (await alice.DeleteAsync($"/api/shop/reservations/{bobReservation}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AreaHttp.Json(bob, $"/api/shop/products/{tee}/reservations")).GetArrayLength().Should().Be(2, "a Mod sees every reservation");
        (await AreaHttp.Json(admin, $"/api/shop/products/{tee}/reservations")).GetArrayLength().Should().Be(2);
        (await AreaHttp.Json(owner, $"/api/shop/products/{tee}/reservations")).GetRawText().Should().NotContain("userId").And.NotContain("@test.com");

        (await bob.DeleteAsync($"/api/shop/reservations/{aliceReservation}")).StatusCode.Should().Be(HttpStatusCode.NoContent, "a Mod deletes anyone's");
        (await alice.DeleteAsync($"/api/shop/reservations/{aliceReservation}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.DeleteAsync($"/api/shop/reservations/{bobReservation}")).StatusCode.Should().Be(HttpStatusCode.NoContent, "the Owner inherits Admin");
        (await AreaHttp.Json(admin, $"/api/shop/products/{tee}/reservations")).GetArrayLength().Should().Be(0);
    }

    /// <summary>
    /// #226 F20: Admin / Owner see how many reservations a delete would remove (0, 1, many) in the list they already
    /// load; anyone else gets null. Only a number - never who reserved. The delete still takes them (cascade).
    /// </summary>
    [Fact]
    public async Task ReservationCount_IsForManagersOnly_AndCountsWhatADeleteRemoves()
    {
        var tag = Tag();
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);
        var created = await admin.PostAsJsonAsync("/api/shop/products", Input($"{tag} Caneca", "Loiça") with { Stock = 5, IsPublic = false });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        async Task<JsonElement> Card(HttpClient client) =>
            (await AreaHttp.Json(client, $"/api/shop?fiscalYear=&q={Uri.EscapeDataString(tag)}")).GetProperty("products").EnumerateArray().Single();

        (await Card(admin)).GetProperty("reservationCount").GetInt32().Should().Be(0);

        var members = new List<HttpClient>();
        for (var i = 0; i < 3; i++)
        {
            var (member, _) = await SignInAsync("Member");
            await AreaHttp.WithTokenAsync(member);
            (await member.PostAsJsonAsync($"/api/shop/products/{id}/reservations", new { hasSizes = false })).StatusCode.Should().Be(HttpStatusCode.OK);
            members.Add(member);
            (await Card(admin)).GetProperty("reservationCount").GetInt32().Should().Be(i + 1);
        }

        (await Card(members[0])).GetProperty("reservationCount").ValueKind.Should().Be(JsonValueKind.Null, "a member never sees the count");
        var (mod, _) = await SignInAsync("Mod");
        (await Card(mod)).GetProperty("reservationCount").ValueKind.Should().Be(JsonValueKind.Null, "a Mod cannot delete, so no count");
        var (owner, _) = await SignInAsync("Owner");
        (await Card(owner)).GetProperty("reservationCount").GetInt32().Should().Be(3);

        (await admin.DeleteAsync($"/api/shop/products/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ProductReservations.CountAsync(r => r.ProductId == id))
            .Should().Be(0, "the delete still takes the reservations (cascade)");

        var shop = File.ReadAllText(Path.Combine(PortalSrc(), "pages/Shop.tsx")).Replace("\r\n", "\n");
        shop.Should().Contain("(open.product.reservationCount ?? 0) > 0 &&", "no warning line at zero")
            .And.Contain("'Este produto tem 1 reserva, que também é apagada.'")
            .And.Contain("`Este produto tem ${open.product.reservationCount} reservas, que também são apagadas.`");
    }
}
