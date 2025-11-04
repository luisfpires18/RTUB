using AutoFixture;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the AlbumCard component to ensure album cards display correctly
/// </summary>
public class AlbumCardTests : TestContext
{
    private readonly Fixture _fixture;

    public AlbumCardTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void AlbumCard_RendersAlbumTitle()
    {
        // Arrange
        var album = Album.Create("Greatest Hits", 2020);

        // Act
        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 12));

        // Assert
        cut.Markup.Should().Contain("Greatest Hits", "card should display album title");
    }

    [Fact]
    public void AlbumCard_RendersAlbumYear()
    {
        // Arrange
        var album = Album.Create("Classic Album", 2018);

        // Act
        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 10));

        // Assert
        cut.Markup.Should().Contain("2018", "card should display album year");
    }

    [Fact]
    public void AlbumCard_RendersSongCount()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);

        // Act
        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 15));

        // Assert
        cut.Markup.Should().Contain("15 músicas", "card should display song count");
        cut.Markup.Should().Contain("bi-music-note-list", "song count should have icon");
    }

    [Fact]
    public void AlbumCard_RendersDescription_WhenProvided()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020, "This is a great album!");

        // Act
        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 10));

        // Assert
        cut.Markup.Should().Contain("This is a great album!", "card should display description");
    }

    [Fact]
    public void AlbumCard_DoesNotRenderDescription_WhenNotProvided()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);

        // Act
        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 10));

        // Assert
        var paragraphCount = cut.FindAll("p").Count;
        paragraphCount.Should().Be(1, "only year paragraph should appear, not description");
    }

    [Fact]
    public void AlbumCard_ShowsOwnerButtons_WhenUserIsOwner()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);

        // Act
        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 10)
            .Add(p => p.IsOwner, true));

        // Assert
        cut.Markup.Should().Contain("admin-overlay", "admin overlay should appear");
        cut.Markup.Should().Contain("bi-pencil", "edit button should appear");
        cut.Markup.Should().Contain("bi-trash", "delete button should appear");
    }

    [Fact]
    public void AlbumCard_DoesNotShowOwnerButtons_WhenUserIsNotOwner()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);

        // Act
        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 10)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().NotContain("admin-overlay", "admin overlay should not appear for non-owner");
    }

    [Fact]
    public void AlbumCard_HasCardClasses()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);

        // Act
        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 10));

        // Assert
        cut.Markup.Should().Contain("card", "should have card class");
        cut.Markup.Should().Contain("album-card", "should have album-card class");
    }

    [Fact]
    public void AlbumCard_IsClickable()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);

        // Act
        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 10));

        // Assert
        cut.Markup.Should().Contain("music-card-clickable", "card should have clickable class");
    }

    [Fact]
    public void AlbumCard_RendersPlaceholderImage_WhenNoImageProvided()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);

        // Act
        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 10));

        // Assert
        cut.Markup.Should().Contain("album-cover-placeholder", "should show placeholder");
        cut.Markup.Should().Contain("bi-disc", "placeholder should have disc icon");
    }

    [Fact]
    public void AlbumCard_InvokesOnClick_WhenCardClicked()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);
        bool callbackInvoked = false;

        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 10)
            .Add(p => p.OnClick, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var clickableArea = cut.Find(".music-card-clickable");
        clickableArea.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnClick callback should be invoked");
    }

    [Fact]
    public void AlbumCard_InvokesOnEdit_WhenEditButtonClicked()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);
        bool callbackInvoked = false;

        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 10)
            .Add(p => p.IsOwner, true)
            .Add(p => p.OnEdit, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var editButton = cut.FindAll("button").First(b => b.InnerHtml.Contains("bi-pencil"));
        editButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnEdit callback should be invoked");
    }

    [Fact]
    public void AlbumCard_InvokesOnDelete_WhenDeleteButtonClicked()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);
        bool callbackInvoked = false;

        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 10)
            .Add(p => p.IsOwner, true)
            .Add(p => p.OnDelete, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var deleteButton = cut.FindAll("button").First(b => b.InnerHtml.Contains("bi-trash"));
        deleteButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnDelete callback should be invoked");
    }

    [Fact]
    public void AlbumCard_HandlesNullYear()
    {
        // Arrange
        var album = Album.Create("Test Album", null);

        // Act
        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, 10));

        // Assert
        cut.Markup.Should().Contain("Test Album", "should still render title with null year");
    }

    [Theory]
    [InlineData(0, "0 músicas")]
    [InlineData(1, "1 músicas")]
    [InlineData(25, "25 músicas")]
    public void AlbumCard_RendersSongCount_ForDifferentCounts(int songCount, string expectedText)
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);

        // Act
        var cut = RenderComponent<AlbumCard>(parameters => parameters
            .Add(p => p.Album, album)
            .Add(p => p.SongCount, songCount));

        // Assert
        cut.Markup.Should().Contain(expectedText, $"should display {songCount} songs correctly");
    }
}
