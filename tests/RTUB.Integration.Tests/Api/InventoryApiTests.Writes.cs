// InventoryApiTests: Writes tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

public partial class InventoryApiTests
{
    [Fact]
    public async Task Create_Validates_AsTheEntity_AndEditNeverChangesTheType()
    {
        var (mod, _) = await SignInAsync("Mod");
        await AreaHttp.WithTokenAsync(mod);

        (await AreaHttp.Errors(await mod.PostAsJsonAsync("/api/inventory", new { condition = "Nope" }))).Keys.Should().BeEquivalentTo("name", "category", "condition");
        (await AreaHttp.Errors(await mod.PostAsJsonAsync("/api/inventory", Input(new string('n', 101), "Guitarra") with
        {
            Brand = new string('b', 101), SerialNumber = new string('s', 101), Location = new string('l', 201), MaintenanceNotes = new string('m', 501),
        }))).Keys.Should().BeEquivalentTo("name", "brand", "serialNumber", "location", "maintenanceNotes");
        (await AreaHttp.Errors(await mod.PostAsJsonAsync("/api/inventory", Input("x", "Kazoo")))).Keys.Should().Equal("category");

        var name = $"{Tag()} Criado";
        var created = await mod.PostAsJsonAsync("/api/inventory", Input(name, "Cavaquinho") with { Brand = "", LastMaintenanceDate = new DateOnly(2024, 5, 6) });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var saved = (await InstrumentAsync(id))!;
        (saved.Name, saved.Category, saved.Condition, saved.Brand, saved.LastMaintenanceDate).Should().Be((name, "Cavaquinho", InstrumentCondition.Good, null, new DateTime(2024, 5, 6)));

        var edit = await mod.PutAsJsonAsync($"/api/inventory/{id}", Input("Outro nome", "Guitarra") with { Condition = "NeedsMaintenance", Location = "Sede" });
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        var edited = (await InstrumentAsync(id))!;
        (edited.Name, edited.Category, edited.Condition, edited.Location).Should().Be(("Outro nome", "Cavaquinho", InstrumentCondition.NeedsMaintenance, "Sede"),
            "the old edit never changed the type");
        (await mod.PutAsJsonAsync("/api/inventory/999999", Input("x", "Guitarra"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Image_TakesTheOriginalAndItsThumbnail_ReplacesBoth_AndDeleteRemovesTheImageAsBefore()
    {
        _factory.Storage.Invocations.Clear();
        var id = await AddAsync($"{Tag()} Imagem", InstrumentType.Guitarra);
        var (admin, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(admin);

        (await admin.PostAsync($"/api/inventory/{id}/image", Multipart(("image", Png, "image/png")))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "the thumbnail must come with the image, as the old form required");
        (await AreaHttp.Errors(await admin.PostAsync($"/api/inventory/{id}/image", Multipart(("image", "GIF89a-not-allowed"u8.ToArray(), "image/gif"), ("thumbnail", Webp, "image/webp")))))
            .Keys.Should().Equal("image");

        var first = await Json(await admin.PostAsync($"/api/inventory/{id}/image", Multipart(("image", Png, "image/png"), ("thumbnail", Webp, "image/webp"))));
        var image = first.GetProperty("imageUrl").GetString()!;
        var thumbnail = first.GetProperty("thumbnailUrl").GetString()!;
        image.Should().Contain("/instruments/");
        thumbnail.Should().Contain("/instruments/thumbnails/");

        await Json(await admin.PostAsync($"/api/inventory/{id}/image", Multipart(("image", Png, "image/png"), ("thumbnail", Webp, "image/webp"))));
        _factory.Storage.Verify(s => s.DeleteImageAsync(image), Times.Once, "a new image deletes the previous one");
        _factory.Storage.Verify(s => s.DeleteImageAsync(thumbnail), Times.Once, "and the previous thumbnail");

        var current = (await InstrumentAsync(id))!;
        (await admin.DeleteAsync($"/api/inventory/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Storage.Verify(s => s.DeleteImageAsync(current.ImageUrl!), Times.Once);
        _factory.Storage.Verify(s => s.DeleteImageAsync(current.ThumbnailUrl!), Times.Never, "the old delete left the thumbnail (recorded follow-up)");
        (await InstrumentAsync(id)).Should().BeNull("a hard delete, as before");
        (await admin.DeleteAsync($"/api/inventory/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryHeader()
    {
        var id = await AddAsync($"{Tag()} Token", InstrumentType.Guitarra);
        var (admin, _) = await SignInAsync("Admin");

        (await admin.PostAsJsonAsync("/api/inventory", Input("sem token", "Guitarra"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync($"/api/inventory/{id}", Input("sem token", "Guitarra"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.DeleteAsync($"/api/inventory/{id}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync($"/api/inventory/{id}/image", Multipart(("image", Png, "image/png"), ("thumbnail", Webp, "image/webp")))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        (await InstrumentAsync(id))!.Name.Should().EndWith("Token");
    }

    [Fact]
    public void ReactFiles_SayNothingAboutMigration_AndTheMemberMenuLinksTheInventory()
    {
        var src = Path.Combine(RepoPaths.Root(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(src, "pages/Inventory.tsx")).Should().NotContainAny("migra", "Migra");
        File.ReadAllText(Path.Combine(src, "data/content.ts")).Should().Contain("inventory: '/inventory'");
        File.ReadAllText(Path.Combine(src, "components/MemberShell.tsx")).Should().Contain("href: portal.inventory,", "the member menu links it (030)");
    }
}
