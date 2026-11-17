// ShopApiTests: ProductsAndPromotions tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;

namespace RTUB.Integration.Tests.Api;

public partial class ShopApiTests
{
    [Theory]
    [InlineData("Member", false, false)]
    [InlineData("Mod", true, false)]
    [InlineData("Admin", true, true)]
    [InlineData("Owner", true, true)]
    public async Task Products_ModAddsAndEdits_AdminAndOwnerAlsoDelete(string role, bool creates, bool manages)
    {
        var id = await AddAsync($"{Tag()} Papel", "Pin", stock: 1);
        var (client, _) = await SignInAsync(role);
        await WithTokenAsync(client);

        var shop = await Json(client, "/api/shop?q=zzz-none");
        (shop.GetProperty("canCreate").GetBoolean(), shop.GetProperty("canManage").GetBoolean()).Should().Be((creates, manages));
        shop.GetProperty("canManageReservations").GetBoolean().Should().Be(creates, "reservations follow Mod and above");
        (await client.PostAsJsonAsync("/api/shop/products", Input($"{Tag()} Novo", "Pin"))).StatusCode
            .Should().Be(creates ? HttpStatusCode.Created : HttpStatusCode.Forbidden, role);
        (await client.PutAsJsonAsync($"/api/shop/products/{id}", Input("Renomeado", "Pin"))).StatusCode
            .Should().Be(creates ? HttpStatusCode.OK : HttpStatusCode.Forbidden, role);
        (await client.DeleteAsync($"/api/shop/products/{id}")).StatusCode.Should().Be(manages ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden, role);
    }

    [Theory]
    [InlineData("Member", false)]
    [InlineData("Mod", true)]
    [InlineData("Admin", true)]
    [InlineData("Owner", true)]
    public async Task Promotions_ModAdminAndOwnerWrite_AMemberOnlyReads(string role, bool writes)
    {
        var cd = await AddAsync($"{Tag()} CD", "Álbum", stock: 5);
        var (client, _) = await SignInAsync(role);
        await WithTokenAsync(client);
        var body = new { name = "3 CDs", price = 20m, items = new[] { new { productId = cd, quantity = 3 } } };

        (await Json(client, "/api/shop/promotions")).GetProperty("canManage").GetBoolean().Should().Be(writes);
        var created = await client.PostAsJsonAsync("/api/shop/promotions", body);
        created.StatusCode.Should().Be(writes ? HttpStatusCode.Created : HttpStatusCode.Forbidden, role);
        if (!writes)
        {
            return;
        }

        var promo = await created.Content.ReadFromJsonAsync<JsonElement>();
        (promo.GetProperty("normalPrice").GetDecimal(), promo.GetProperty("price").GetDecimal(), promo.GetProperty("inStock").GetBoolean())
            .Should().Be((30m, 20m, true));
        var id = promo.GetProperty("id").GetInt32();
        (await client.PutAsJsonAsync($"/api/shop/promotions/{id}", new { name = "Pack", price = 25m, items = body.items })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/shop/promotions/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/shop/promotions/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Promotions_AreValidated_AndVisitorsOnlySeeThePublicOnes()
    {
        var pub = await AddAsync($"{Tag()} Pub", "Álbum", stock: 3);
        var members = await AddAsync($"{Tag()} Membros", "Álbum", stock: 1, isPublic: false);
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        var items = new[] { new { productId = pub, quantity = 3 } };

        foreach (var bad in new object[]
        {
            new { name = "", price = 20m, items },
            new { name = "x", price = 0m, items },
            new { name = "x", price = 30m, items },
            new { name = "x", price = 20m, items = new object[0] },
            new { name = "x", price = 20m, items = new[] { new { productId = pub, quantity = 0 } } },
            new { name = "x", price = 20m, items = new[] { new { productId = 0, quantity = 1 } } },
            new { name = "x", price = 20m, items = new[] { new { productId = pub, quantity = 3 }, new { productId = pub, quantity = 1 } } },
        })
        {
            (await mod.PostAsJsonAsync("/api/shop/promotions", bad)).StatusCode.Should().Be(HttpStatusCode.BadRequest, JsonSerializer.Serialize(bad));
        }

        var publicName = $"{Tag()} Publica";
        var memberName = $"{Tag()} Interna";
        (await mod.PostAsJsonAsync("/api/shop/promotions", new { name = publicName, price = 20m, items })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await mod.PostAsJsonAsync("/api/shop/promotions", new { name = memberName, price = 5m, items = new[] { new { productId = members, quantity = 1 } } }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        string[] Names(JsonElement j) => j.GetProperty("promotions").EnumerateArray().Select(p => p.GetProperty("name").GetString()!).ToArray();
        Names(await Json(Anonymous(), "/api/shop/promotions")).Should().Contain(publicName).And.NotContain(memberName);
        Names(await Json(mod, "/api/shop/promotions")).Should().Contain(new[] { publicName, memberName });
        (await Json(Anonymous(), "/api/shop/promotions")).GetProperty("canManage").GetBoolean().Should().BeFalse();
        (await Anonymous().PostAsJsonAsync("/api/shop/promotions", new { name = "x", price = 1m, items })).StatusCode.Should().NotBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Promotions_ShowSoldOut_AndLeaveWithTheirProduct()
    {
        var cd = await AddAsync($"{Tag()} CD", "Álbum", stock: 2);
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var name = $"{Tag()} Pack";
        (await admin.PostAsJsonAsync("/api/shop/promotions", new { name, price = 20m, items = new[] { new { productId = cd, quantity = 3 } } }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement Mine(JsonElement j) => j.GetProperty("promotions").EnumerateArray().Single(p => p.GetProperty("name").GetString() == name);
        Mine(await Json(admin, "/api/shop/promotions")).GetProperty("inStock").GetBoolean().Should().BeFalse("it needs 3 and only 2 are left");

        (await admin.DeleteAsync($"/api/shop/products/{cd}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Json(admin, "/api/shop/promotions")).GetProperty("promotions").EnumerateArray()
            .Should().NotContain(p => p.GetProperty("name").GetString() == name, "a bundle with no product left is not shown");
    }

    [Fact]
    public async Task ProductForm_Validates_AvailabilityFollowsStock_AndDeleteTakesImageAndReservations()
    {
        _factory.Storage.Invocations.Clear();
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);

        (await Errors(await admin.PostAsJsonAsync("/api/shop/products", new { price = 0, stock = -1 }))).Keys
            .Should().BeEquivalentTo("name", "type", "price", "stock");
        (await Errors(await admin.PostAsJsonAsync("/api/shop/products", Input(new string('n', 201), new string('t', 51)) with { Description = new string('d', 1001) })))
            .Keys.Should().BeEquivalentTo("name", "type", "description");

        var created = await admin.PostAsJsonAsync("/api/shop/products", Input($"{Tag()} Sweat", "Roupa") with { Stock = 0, IsPublic = false, Price = 12.5m });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var saved = (await ProductAsync(id))!;
        (saved.Price, saved.Stock, saved.IsAvailable, saved.IsPublic).Should().Be((12.5m, 0, false, false), "availability follows the stock, as the old save");

        (await admin.PutAsJsonAsync($"/api/shop/products/{id}", Input(saved.Name, "Roupa") with { Stock = 3, IsPublic = false, Price = 15m })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ProductAsync(id))!.IsAvailable.Should().BeTrue();

        var image = (await Json(await admin.PostAsync($"/api/shop/products/{id}/image", Multipart(Webp, "image/webp")))).GetProperty("imageUrl").GetString()!;
        (await Errors(await admin.PostAsync($"/api/shop/products/{id}/image", Multipart("GIF89a-not-allowed"u8.ToArray(), "image/gif")))).Keys.Should().Equal("image");
        var replaced = (await Json(await admin.PostAsync($"/api/shop/products/{id}/image", Multipart(Webp, "image/webp")))).GetProperty("imageUrl").GetString()!;
        _factory.Storage.Verify(s => s.DeleteImageAsync(image), Times.Once, "a new image deletes the previous one");

        var (member, _) = await SignInAsync("Member");
        await WithTokenAsync(member);
        await member.PostAsJsonAsync($"/api/shop/products/{id}/reservations", new { hasSizes = false });
        (await admin.DeleteAsync($"/api/shop/products/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Storage.Verify(s => s.DeleteImageAsync(replaced), Times.Once);
        (await ProductAsync(id)).Should().BeNull();
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ProductReservations.CountAsync(r => r.ProductId == id))
            .Should().Be(0, "reservations go with the product (cascade), as before");
    }

    [Fact]
    public async Task AMod_SetsAndReplacesProductImages()
    {
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        var id = (await (await mod.PostAsJsonAsync("/api/shop/products", Input($"{Tag()} Pin Mod", "Pin"))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        (await mod.PostAsync($"/api/shop/products/{id}/image", Multipart(Webp, "image/webp"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await mod.PostAsync($"/api/shop/products/{id}/image", Multipart(Webp, "image/webp"))).StatusCode.Should().Be(HttpStatusCode.OK, "a Mod changes images too (059)");
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryHeader()
    {
        var id = await AddAsync($"{Tag()} Token", "Roupa", stock: 2, isPublic: false);
        var (admin, _) = await SignInAsync("Admin");

        (await admin.PostAsJsonAsync("/api/shop/products", Input("sem token", "Pin"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync($"/api/shop/products/{id}", Input("sem token", "Pin"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.DeleteAsync($"/api/shop/products/{id}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync($"/api/shop/products/{id}/reservations", new { hasSizes = false })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.DeleteAsync("/api/shop/reservations/1")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await ProductAsync(id))!.Name.Should().EndWith("Token");
    }

    [Fact]
    public void ReactFiles_SayNothingAboutMigration_AndTheMemberMenuLinksTheShop()
    {
        var src = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(src, "pages/Shop.tsx")).Should().NotContainAny("migra", "Migra");
        File.ReadAllText(Path.Combine(src, "data/content.ts")).Should().Contain("shop: '/shop'");
        File.ReadAllText(Path.Combine(src, "components/MemberShell.tsx")).Should().Contain("href: portal.shop,", "the member menu links it (030)");
    }

    /// <summary>
    /// #221 form wiring: one checkbox carries availableAllYears on create and edit (off for a new product), loads the
    /// saved value, and its hint names the creation year a year-specific product falls back to.
    /// </summary>
    [Fact]
    public void ReactProductForm_WiresTheAllYearsOption()
    {
        var shop = File.ReadAllText(Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src", "components/ShopProductForm.tsx"));
        shop.Should().Contain("Disponível em todos os anos letivos")
            .And.Contain("availableAllYears: false })", "a new product is year-specific")
            .And.Contain("availableAllYears: d.availableAllYears,", "edit loads the saved value")
            .And.Contain("onChange={(e) => set({ availableAllYears: e.target.checked })}")
            .And.Contain("setCreatedYear(d.fiscalYear);")
            .And.Contain("`Aparece só no ano letivo ${createdYear}, em que foi criado.`");
    }
}
