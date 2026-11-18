// ShopApiTests: Catalogue tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Application.Helpers;

namespace RTUB.Integration.Tests.Api;

public partial class ShopApiTests
{
    [Fact]
    public async Task Visitors_GetSafePublicCatalogue_AndCannotAccessMemberActions()
    {
        var tag = Tag();
        var id = await AddAsync($"{tag} Visita", "Pin");
        var soldOut = await AddAsync($"{tag} Esgotado", "Pin", stock: 0);
        var memberOnly = await AddAsync($"{tag} Privado", "Roupa", isPublic: false);
        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        var catalogue = await Json(anonymous, $"/api/shop?q={tag}");
        Names(catalogue).Should().Equal($"{tag} Esgotado", $"{tag} Visita");
        var products = catalogue.GetProperty("products").EnumerateArray().ToList();
        var card = products.Single(p => p.GetProperty("id").GetInt32() == id);
        products.Single(p => p.GetProperty("id").GetInt32() == soldOut).GetProperty("stock").GetInt32().Should().Be(0);
        (await ProductAsync(soldOut))!.IsAvailable.Should().BeFalse("public listing keeps the current sold-out state");
        card.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "id", "name", "type", "price", "stock", "imageUrl", "description", "availableAllYears");
        catalogue.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("fiscalYears", "fiscalYear", "types", "products");
        var json = catalogue.GetRawText();
        json.Should().NotContain("myReservation").And.NotContain("canReserve").And.NotContain("reservationCount")
            .And.NotContain("canCreate").And.NotContain("canManage").And.NotContain("username");
        products.Select(p => p.GetProperty("id").GetInt32()).Should().NotContain(memberOnly);
        json.Should().NotContain($"{tag} Privado");

        (await anonymous.GetAsync($"/api/shop/products/{id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"/api/shop/products/{id}/reservations")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await anonymous.PostAsJsonAsync($"/api/shop/products/{id}/reservations", new { hasSizes = false })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/shop/products", Input("x", "Pin"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PutAsJsonAsync($"/api/shop/products/{id}", Input("x", "Pin"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync($"/api/shop/products/{id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync($"/api/shop/products/{id}/image", Multipart(Webp, "image/webp"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync("/api/shop/reservations/1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var page = await anonymous.GetAsync("/shop");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        (await page.Content.ReadAsStringAsync()).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");

        var (member, _) = await SignInAsync("Member");
        (await member.GetStringAsync("/shop")).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");

        var web = typeof(RTUB.Program).Assembly;
        web.GetType("RTUB.Pages.Inventory.Shop").Should().BeNull("the Blazor shop page was retired");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/shop", "/inventory", "/leaderboard", "/members/manage", "/member/events", "/member/gallery", "/member/roles", "/hierarchy" });
    }

    [Fact]
    public async Task Products_AreByTypeThenName_ThisYearByDefault_WithSearchAndType()
    {
        var tag = Tag();
        await AddAsync($"{tag} Zeta", "Album", stock: 3);
        await AddAsync($"{tag} Alfa", "Roupa", stock: 0, isPublic: false);
        await AddAsync($"{tag} Beta", "Album", stock: 2, isPublic: false);
        var old = await AddAsync($"{tag} Antigo", "Album", stock: 1);
        await SetCreatedAsync(old, DateTime.UtcNow.AddYears(-2));
        var (member, _) = await SignInAsync("Member");

        var shop = await Json(member, $"/api/shop?q={tag}");
        shop.GetProperty("fiscalYear").GetString().Should().Be(FiscalYearHelper.GetCurrentFiscalYearString(), "the old page opened on the current year");
        Names(shop).Should().Equal($"{tag} Beta", $"{tag} Zeta", $"{tag} Alfa");
        shop.GetProperty("canCreate").GetBoolean().Should().BeFalse();
        shop.GetProperty("canManage").GetBoolean().Should().BeFalse();
        shop.GetProperty("sizes").EnumerateArray().Select(s => s.GetString()).Should().Equal("XS", "S", "M", "L", "XL", "XXL", "XXXL");

        var products = shop.GetProperty("products").EnumerateArray().ToList();
        products[0].GetProperty("canReserve").GetBoolean().Should().BeTrue("members-only and in stock");
        products[1].GetProperty("canReserve").GetBoolean().Should().BeFalse("public products are not reserved here");
        products[2].GetProperty("canReserve").GetBoolean().Should().BeFalse("out of stock");
        products[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "name", "type", "price", "stock", "isPublic", "imageUrl",
            "myReservation", "canReserve", "availableAllYears", "reservationCount");

        Names(await Json(member, $"/api/shop?fiscalYear=&q={tag}")).Should().Contain($"{tag} Antigo", "Todos os anos");
        Names(await Json(member, $"/api/shop?fiscalYear=&q={tag}&type=Roupa")).Should().Equal($"{tag} Alfa");
        Names(await Json(member, $"/api/shop?q={tag.ToUpperInvariant()}%20b")).Should().Equal($"{tag} Beta");
        (await Json(member, "/api/shop?fiscalYear=")).GetProperty("types").EnumerateArray().Select(t => t.GetString()).Should().Contain(new[] { "Album", "Roupa" });
        (await member.GetAsync("/api/shop?fiscalYear=1999-2000")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// #221: a product available in all years shows in every fiscal year; a year-specific one only in the year it was
    /// created in, as before. Stock and reservation rules are the same for both.
    /// </summary>
    [Fact]
    public async Task AllYearsProducts_ShowInEveryYear_YearSpecificOnlyInTheirOwn()
    {
        var tag = Tag();
        var current = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        var previous = $"{current - 1}-{current}";
        await EnsureFiscalYearAsync(current - 1);
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);

        var yearly = await CreateAsync(admin, Input($"{tag} Sweat 2025", "Roupa"));
        var publicCd = await CreateAsync(admin, Input($"{tag} CD público", "Album") with { AvailableAllYears = true, IsPublic = true, Stock = 4 });
        var cd = await CreateAsync(admin, Input($"{tag} CD", "Album") with { AvailableAllYears = true, IsPublic = false, Stock = 4 });
        var soldOut = await CreateAsync(admin, Input($"{tag} CD antigo", "Album") with { AvailableAllYears = true, IsPublic = false, Stock = 0 });
        foreach (var id in new[] { yearly, publicCd, cd, soldOut })
        {
            await SetCreatedAsync(id, new DateTime(current - 1, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        }

        (await ProductAsync(yearly))!.AvailableAllYears.Should().BeFalse("year-specific unless chosen");
        (await ProductAsync(cd))!.AvailableAllYears.Should().BeTrue();

        var (member, _) = await SignInAsync("Member");
        Names(await Json(member, $"/api/shop?q={tag}")).Should().Equal($"{tag} CD", $"{tag} CD antigo", $"{tag} CD público");
        Names(await Json(member, $"/api/shop?fiscalYear={previous}&q={tag}")).Should().Equal($"{tag} CD", $"{tag} CD antigo", $"{tag} CD público", $"{tag} Sweat 2025");
        Names(await Json(member, $"/api/shop?fiscalYear=&q={tag}")).Should().Equal($"{tag} CD", $"{tag} CD antigo", $"{tag} CD público", $"{tag} Sweat 2025");

        var anonymous = Anonymous();
        Names(await Json(anonymous, $"/api/shop?q={tag}")).Should().Equal($"{tag} CD público");
        Names(await Json(anonymous, $"/api/shop?fiscalYear={previous}&q={tag}")).Should().Equal($"{tag} CD público", $"{tag} Sweat 2025");
        Names(await Json(anonymous, $"/api/shop?fiscalYear=&q={tag}")).Should().Equal($"{tag} CD público", $"{tag} Sweat 2025");
        (await Json(anonymous, $"/api/shop?q={tag}")).GetProperty("products").EnumerateArray().Single()
            .GetProperty("availableAllYears").GetBoolean().Should().BeTrue();

        var cards = (await Json(member, $"/api/shop?q={tag}")).GetProperty("products").EnumerateArray().ToList();
        cards.Select(c => c.GetProperty("availableAllYears").GetBoolean()).Should().AllBeEquivalentTo(true);
        (cards[0].GetProperty("canReserve").GetBoolean(), cards[1].GetProperty("canReserve").GetBoolean())
            .Should().Be((true, false), "a sold-out product is still listed and not reservable, all years or not");
        (await ProductAsync(soldOut))!.IsAvailable.Should().BeFalse("availability still follows the stock");

        var detail = await Json(member, $"/api/shop/products/{cd}");
        (detail.GetProperty("availableAllYears").GetBoolean(), detail.GetProperty("fiscalYear").GetString()).Should().Be((true, previous));
        (await Json(member, $"/api/shop/products/{yearly}")).GetProperty("availableAllYears").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// #221: edit turns a product into all years and back; an edit without the field keeps it. A year-specific product's
    /// year is the year it was created in (immutable), so turning the option off always lands on a real year.
    /// </summary>
    [Fact]
    public async Task AllYears_EditsBothWays_AbsentKeeps_AndOffGoesBackToTheCreationYear()
    {
        var tag = Tag();
        var current = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        var previous = $"{current - 1}-{current}";
        await EnsureFiscalYearAsync(current - 1);
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var id = await CreateAsync(admin, Input($"{tag} CD", "Album"));
        (await Json(admin, $"/api/shop/products/{id}")).GetProperty("fiscalYear").GetString()
            .Should().Be(FiscalYearHelper.GetCurrentFiscalYearString(), "a new product belongs to the year it is created in");
        await SetCreatedAsync(id, new DateTime(current - 1, 10, 1, 12, 0, 0, DateTimeKind.Utc));

        async Task<JsonElement> PutAsync(InputBody body) => await Json(await admin.PutAsJsonAsync($"/api/shop/products/{id}", body));
        bool Listed(JsonElement shop) => Names(shop).Contains($"{tag} CD");

        (await PutAsync(Input($"{tag} CD", "Album") with { AvailableAllYears = true })).GetProperty("availableAllYears").GetBoolean().Should().BeTrue();
        Listed(await Json(admin, $"/api/shop?q={tag}")).Should().BeTrue();

        (await PutAsync(Input($"{tag} CD", "Album") with { Price = 12m })).GetProperty("availableAllYears").GetBoolean()
            .Should().BeTrue("an edit without the field keeps it");

        var off = await PutAsync(Input($"{tag} CD", "Album") with { AvailableAllYears = false, Price = 12m });
        (off.GetProperty("availableAllYears").GetBoolean(), off.GetProperty("fiscalYear").GetString()).Should().Be((false, previous));
        Listed(await Json(admin, $"/api/shop?q={tag}")).Should().BeFalse("back to the year it was created in only");
        Listed(await Json(admin, $"/api/shop?fiscalYear={previous}&q={tag}")).Should().BeTrue();
        (await ProductAsync(id))!.Price.Should().Be(12m);
    }

    /// <summary>
    /// #221: a product available in all years is reserved like any other, and switching the option never touches a
    /// reservation (member, size, date) nor the stock. Year-specific products keep reserving as before.
    /// </summary>
    [Fact]
    public async Task AllYears_ReservesNormally_AndTheToggleNeverChangesAReservation()
    {
        var tag = Tag();
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var cd = await CreateAsync(admin, Input($"{tag} CD", "Album") with { AvailableAllYears = true, IsPublic = false, Stock = 2 });
        var tee = await CreateAsync(admin, Input($"{tag} T-shirt", "Roupa") with { IsPublic = false, Stock = 2 });
        await SetCreatedAsync(cd, DateTime.UtcNow.AddYears(-2));
        var (member, me) = await SignInAsync("Member");
        await WithTokenAsync(member);

        (await member.PostAsJsonAsync($"/api/shop/products/{cd}/reservations", new { hasSizes = true, size = "L" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var reservation = (await Json(admin, $"/api/shop/products/{cd}/reservations")).EnumerateArray().Single();
        (reservation.GetProperty("username").GetString(), reservation.GetProperty("size").GetString()).Should().Be((me.UserName, "L"));
        (await Json(await member.PostAsJsonAsync($"/api/shop/products/{tee}/reservations", new { hasSizes = false })))
            .GetProperty("username").GetString().Should().Be(me.UserName, "year-specific products reserve as before");

        foreach (var allYears in new[] { false, true })
        {
            (await admin.PutAsJsonAsync($"/api/shop/products/{cd}", Input($"{tag} CD", "Album") with { AvailableAllYears = allYears, IsPublic = false, Stock = 2 }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            (await Json(admin, $"/api/shop/products/{cd}/reservations")).EnumerateArray().Single().GetRawText()
                .Should().Be(reservation.GetRawText(), "the reservation is the same whatever the option");
            (await Json(member, $"/api/shop?fiscalYear=&q={tag}")).GetProperty("products").EnumerateArray()
                .Single(p => p.GetProperty("id").GetInt32() == cd).GetProperty("myReservation").GetRawText().Should().Be(reservation.GetRawText());
        }

        (await ProductAsync(cd))!.Stock.Should().Be(2, "reserving never changed the stock");
    }
}
