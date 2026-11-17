// GalleryManagementApiTests: EditAndDelete tests.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

public partial class GalleryManagementApiTests
{
    [Fact]
    public async Task OnlyTheUploaderAdminOrOwner_EditAndDelete_AndModsAndOtherMembersCannot()
    {
        var (uploader, uploaderUser) = await SignInAsync();
        var (other, _) = await SignInAsync();
        var (mod, _) = await SignInAsync("Mod");
        var (owner, _) = await SignInAsync("Owner");
        foreach (var c in new[] { uploader, other, mod, owner })
        {
            await WithTokenAsync(c);
        }

        var id = await AddMediaAsync(uploaderUser.Id, isPrivate: true);
        var edit = new { title = "Novo título", date = "2022-11-05", membersOnly = false, personIds = Array.Empty<string>() };

        foreach (var refused in new[] { other, mod })
        {
            (await refused.PutAsJsonAsync($"/api/gallery/items/{id}", edit)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await refused.DeleteAsync($"/api/gallery/items/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await refused.GetAsync($"/api/gallery/items/{id}/edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var listed = await other.GetFromJsonAsync<JsonElement>($"/api/gallery/items/{id}");
        listed.GetProperty("canEdit").GetBoolean().Should().BeFalse();

        (await uploader.PutAsJsonAsync($"/api/gallery/items/{id}", edit)).StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await MediaAsync(id);
        saved!.Title.Should().Be("Novo título");
        saved.IsPrivate.Should().BeFalse("the members-only flag follows the edit");

        (await owner.PutAsJsonAsync($"/api/gallery/items/{id}", edit)).StatusCode.Should().Be(HttpStatusCode.OK,
            "the Owner inherits Admin (was Admin only)");
        (await owner.DeleteAsync($"/api/gallery/items/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await MediaAsync(id)).Should().BeNull();
        _factory.GalleryStorage.Verify(s => s.DeleteMediaAsync(saved.MediaUrl), Times.Once, "the stored file goes first, through the fake");
        (await owner.DeleteAsync($"/api/gallery/items/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenStorageFailsToDelete_TheItemAndItsTagsStay()
    {
        var (uploader, uploaderUser) = await SignInAsync();
        var (_, tagged) = await SignInAsync();
        await WithTokenAsync(uploader);
        var id = await AddMediaAsync(uploaderUser.Id, isPrivate: true, tagged.Id);
        var url = (await MediaAsync(id))!.MediaUrl;
        _factory.GalleryStorage.Setup(s => s.DeleteMediaAsync(url)).ThrowsAsync(new IOException("R2 unavailable"));

        var response = await uploader.DeleteAsync($"/api/gallery/items/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await MediaAsync(id)).Should().NotBeNull("a failed storage delete must not detach the row from its file");
        (await TagsAsync(id)).Should().ContainSingle().Which.Should().Be(tagged.Id);
        (await uploader.GetAsync($"/api/gallery/items/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_AddsAndRemovesTags_WithoutNotifying()
    {
        var (uploader, uploaderUser) = await SignInAsync();
        var (_, kept) = await SignInAsync();
        var (_, removed) = await SignInAsync();
        var (_, added) = await SignInAsync();
        await WithTokenAsync(uploader);
        var id = await AddMediaAsync(uploaderUser.Id, isPrivate: true, kept.Id, removed.Id);

        var form = await uploader.GetFromJsonAsync<JsonElement>($"/api/gallery/items/{id}/edit");
        form.GetProperty("people").GetArrayLength().Should().Be(2);

        var found = await uploader.GetFromJsonAsync<JsonElement>($"/api/gallery/people?q={added.UserName}");
        found.EnumerateArray().Should().ContainSingle(p => p.GetProperty("id").GetString() == added.Id);
        found.GetRawText().Should().NotContain("@test.com").And.NotContain("phone");

        var response = await uploader.PutAsJsonAsync($"/api/gallery/items/{id}",
            new { title = "Foto", date = "2021-03-04", membersOnly = true, personIds = new[] { kept.Id, added.Id } });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TagsAsync(id)).Should().BeEquivalentTo(new[] { kept.Id, added.Id });
        _factory.Push.Verify(p => p.SendToSelectedUsersAsync(It.Is<IEnumerable<string>>(ids => ids.Contains(added.Id)), It.IsAny<SendPushNotificationDto>()),
            Times.Never, "only tags made on upload notify, as before");
    }
}
