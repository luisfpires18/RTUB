using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// The React /gallery timeline (React track 009): who sees what, in which order, and what an item
/// carries. Each test gets its own database; nothing touches storage.
/// </summary>
public class GalleryTimelineServiceTests
{
    private static readonly ClaimsPrincipal Visitor = new(new ClaimsIdentity());
    private static readonly ClaimsPrincipal Member = new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "m") }, "test"));

    private readonly DatabaseFixture _db = new();
    private readonly GalleryTimelineService _service;
    private readonly string _uploader;

    public GalleryTimelineServiceTests()
    {
        _service = new GalleryTimelineService(new Contexts(_db));
        _uploader = AddUser("Uploader");
    }

    [Fact]
    public async Task NoMedia_IsAnEmptyTimeline()
    {
        var result = await _service.GetTimelineAsync(Visitor, new GalleryQuery());

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.Years.Should().BeEmpty();
        result.IsMember.Should().BeFalse();
    }

    [Fact]
    public async Task AVisitor_SeesPublicItemsOnly_EvenInTheYearsAndCount()
    {
        Add("public", 2024, isPrivate: false);
        Add("members", 2019, isPrivate: true);

        var result = await _service.GetTimelineAsync(Visitor, new GalleryQuery());

        result.Items.Select(i => i.Title).Should().Equal("public");
        result.Total.Should().Be(1);
        result.Years.Should().Equal(new[] { 2024 }, "a members-only year must not hint at hidden photos");
    }

    [Fact]
    public async Task AMember_SeesPublicAndMembersOnlyItems_Badged()
    {
        Add("public", 2024, isPrivate: false);
        Add("members", 2024, isPrivate: true);

        var result = await _service.GetTimelineAsync(Member, new GalleryQuery());

        result.IsMember.Should().BeTrue();
        result.Items.Should().HaveCount(2);
        result.Items.Single(i => i.Title == "members").MembersOnly.Should().BeTrue();
        result.Items.Single(i => i.Title == "public").MembersOnly.Should().BeFalse();
    }

    [Fact]
    public async Task PublicOnly_NarrowsAMembersView_ToWhatVisitorsSee()
    {
        // The home preview (React track 010) shows visitors' photos to everyone.
        Add("public", 2024, isPrivate: false);
        Add("members", 2025, isPrivate: true);

        var result = await _service.GetTimelineAsync(Member, new GalleryQuery(PublicOnly: true));

        result.Items.Select(i => i.Title).Should().Equal("public");
        result.Years.Should().Equal(new[] { 2024 });
        result.IsMember.Should().BeFalse("nothing member-only, tags included, comes back on this view");
    }

    [Fact]
    public async Task Items_AreNewestFirst_ByYearMonthDay_ThenNewestRecordFirst()
    {
        Add("2023", 2023, month: 12, day: 31);
        Add("2024 no day", 2024, month: 5);
        Add("2024 may 20", 2024, month: 5, day: 20);
        Add("2024 june", 2024, month: 6, day: 1);
        Add("same day, older record", 2022, month: 1, day: 1);
        Add("same day, newer record", 2022, month: 1, day: 1);

        var result = await _service.GetTimelineAsync(Visitor, new GalleryQuery());

        result.Items.Select(i => i.Title).Should().Equal(
            "2024 june", "2024 may 20", "2024 no day", "2023", "same day, newer record", "same day, older record");
    }

    [Fact]
    public async Task Pages_NeverOverlap_AndAddUpToTheTotal()
    {
        for (var i = 0; i < 7; i++)
        {
            Add($"photo {i}", 2024, month: 3, day: 3);
        }

        var first = await _service.GetTimelineAsync(Visitor, new GalleryQuery(Page: 1, PageSize: 3));
        var second = await _service.GetTimelineAsync(Visitor, new GalleryQuery(Page: 2, PageSize: 3));
        var third = await _service.GetTimelineAsync(Visitor, new GalleryQuery(Page: 3, PageSize: 3));

        var ids = first.Items.Concat(second.Items).Concat(third.Items).Select(i => i.Id).ToList();
        ids.Should().OnlyHaveUniqueItems().And.HaveCount(7);
        first.Total.Should().Be(7);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1000, 60)]
    public async Task PageSize_IsClamped(int requested, int expected)
    {
        Add("photo", 2024);

        var result = await _service.GetTimelineAsync(Visitor, new GalleryQuery(PageSize: requested));

        result.PageSize.Should().Be(expected);
    }

    [Fact]
    public async Task YearAndSearch_Filter_ButYearsStayComplete()
    {
        Add("Serenata em Bragança", 2024);
        Add("Viagem", 2024);
        Add("Serenata antiga", 2016);

        var result = await _service.GetTimelineAsync(Visitor, new GalleryQuery(Year: 2024, Search: "  serenata "));

        result.Items.Select(i => i.Title).Should().Equal("Serenata em Bragança");
        result.Years.Should().Equal(2024, 2016);
    }

    [Fact]
    public async Task PersonTags_AreForMembersOnly_AndTheirFilterIsIgnoredForVisitors()
    {
        var person = AddUser("Tagged");
        var tagged = Add("tagged", 2024);
        Add("untagged", 2024);
        Tag(tagged, person);

        var visitor = await _service.GetTimelineAsync(Visitor, new GalleryQuery(PersonId: person));
        visitor.Items.Should().HaveCount(2, "a visitor cannot filter by person");
        visitor.Items.Should().OnlyContain(i => i.People.Count == 0, "visitors never learn who is in a photo");
        visitor.People.Should().BeEmpty();

        var member = await _service.GetTimelineAsync(Member, new GalleryQuery(PersonId: person));
        member.Items.Should().ContainSingle().Which.People.Should().ContainSingle().Which.Name.Should().Be("Tagged");
        member.People.Should().ContainSingle().Which.Id.Should().Be(person);
    }

    [Fact]
    public async Task ItemType_IsImageOrVideo()
    {
        Add("photo", 2024);
        Add("clip", 2024, type: MediaType.Video);

        var result = await _service.GetTimelineAsync(Visitor, new GalleryQuery());

        result.Items.Single(i => i.Title == "photo").Type.Should().Be("image");
        result.Items.Single(i => i.Title == "clip").Type.Should().Be("video");
    }

    [Theory]
    [InlineData("https://pub-test.r2.dev/images/x/gallery/image/a.jpg", "https://pub-test.r2.dev/images/x/gallery/image/a.jpg")]
    [InlineData("/images/a.jpg", "/images/a.jpg")]
    [InlineData("http://insecure.example/a.jpg", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("//evil.example/a.jpg", null)]
    public async Task OnlyAUsableMediaUrl_IsPublished(string stored, string? expected)
    {
        Add("photo", 2024, url: stored);

        var item = (await _service.GetTimelineAsync(Visitor, new GalleryQuery())).Items.Single();

        item.Url.Should().Be(expected, "the page shows a placeholder for anything it cannot safely load");
    }

    [Fact]
    public async Task OneItem_IsHiddenFromAVisitor_WhenMembersOnly_AndUnknownIdsAreNull()
    {
        var hidden = Add("members", 2024, isPrivate: true);
        var shown = Add("public", 2024);

        (await _service.GetItemAsync(hidden, Visitor)).Should().BeNull();
        (await _service.GetItemAsync(hidden, Member)).Should().NotBeNull();
        (await _service.GetItemAsync(shown, Visitor))!.Title.Should().Be("public");
        (await _service.GetItemAsync(999_999, Member)).Should().BeNull();
    }

    [Fact]
    public void PublicContracts_CarryNoUploaderOrInternalField()
    {
        // A new property on these records is a new field in the browser: add it here on purpose.
        Names<GalleryTimelineDto>().Should().BeEquivalentTo("IsMember", "Items", "Total", "Page", "PageSize", "Years", "People");
        Names<GalleryItemDto>().Should().BeEquivalentTo("Id", "Title", "Type", "Url", "Year", "Month", "Day", "MembersOnly", "People");
        Names<GalleryPersonDto>().Should().BeEquivalentTo("Id", "Name");
    }

    // ---------- helpers ----------

    private static IEnumerable<string> Names<T>() => typeof(T).GetProperties().Select(p => p.Name);

    private string AddUser(string nickname)
    {
        using var db = _db.CreateContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "u" + Guid.NewGuid().ToString("N")[..10],
            Email = "private@test.com",
            Nickname = nickname,
            FirstName = "First",
            LastName = "Last"
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    private int Add(string title, int year, byte? month = null, byte? day = null, bool isPrivate = false,
        MediaType type = MediaType.Image, string url = "https://pub-test.r2.dev/images/Test/gallery/image/a.jpg")
    {
        using var db = _db.CreateContext();
        var media = GalleryMedia.Create(_uploader, title, type, url, year, month, day, isPrivate: isPrivate);
        db.GalleryMedia.Add(media);
        db.SaveChanges();
        return media.Id;
    }

    private void Tag(int mediaId, string userId)
    {
        using var db = _db.CreateContext();
        db.GalleryMediaPersonTags.Add(GalleryMediaPersonTag.Create(mediaId, userId));
        db.SaveChanges();
    }

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }
}
