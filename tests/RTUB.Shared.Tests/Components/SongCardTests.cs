using AutoFixture;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the SongCard component to ensure song cards display correctly
/// </summary>
public class SongCardTests : TestContext
{
    private readonly Fixture _fixture;

    public SongCardTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void SongCard_RendersSongTitle()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("Favaios", "card should display song title");
    }

    [Fact]
    public void SongCard_RendersTrackNumberInTitle_WhenPresent()
    {
        // Arrange
        var song = Song.Create("Favaios", 1, 1);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("#1", "card should display track number");
        cut.Markup.Should().Contain("song-track-number", "track number should have proper class");
    }

    [Fact]
    public void SongCard_DoesNotRenderTrackNumber_WhenNotPresent()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().NotContain("song-track-number", "track number should not appear when not set");
    }

    [Fact]
    public void SongCard_RendersLyricAuthor_WhenPresent()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);
        song.UpdateDetails("Favaios", null, "João Silva", null, null, null);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("Letra:", "lyric author label should be present");
        cut.Markup.Should().Contain("João Silva", "lyric author name should be displayed");
        cut.Markup.Should().Contain("bi-pen", "lyric author should have pen icon");
    }

    [Fact]
    public void SongCard_RendersMusicAuthor_WhenPresent()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);
        song.UpdateDetails("Favaios", null, null, "Maria Santos", null, null);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("Música:", "music author label should be present");
        cut.Markup.Should().Contain("Maria Santos", "music author name should be displayed");
        cut.Markup.Should().Contain("bi-music-note", "music author should have music note icon");
    }

    [Fact]
    public void SongCard_RendersAdaptation_WhenPresent()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);
        song.UpdateDetails("Favaios", null, "João Silva", "Maria Santos", "Pedro Costa", null);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("Adaptação:", "adaptation label should be present");
        cut.Markup.Should().Contain("Pedro Costa", "adaptation name should be displayed");
        cut.Markup.Should().Contain("bi-arrow-repeat", "adaptation should have arrow-repeat icon");
    }

    [Fact]
    public void SongCard_DoesNotRenderAdaptation_WhenNotPresent()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);
        song.UpdateDetails("Favaios", null, "João Silva", "Maria Santos", null, null);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().NotContain("Adaptação:", "adaptation section should not appear when not set");
    }

    [Fact]
    public void SongCard_ShowsUnknownAuthor_WhenNoAuthorsPresent()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("Autor desconhecido", "should display unknown author message");
    }

    [Fact]
    public void SongCard_ShowsPlayButton_WhenSongHasMusic()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);
        song.SetHasMusic(true);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("bi-play-circle-fill", "should show play button icon");
        cut.Markup.Should().Contain("Play", "should show play button label");
        var playButtons = cut.FindAll("button").Where(b => b.InnerHtml.Contains("Play"));
        playButtons.Should().Contain(b => !b.HasAttribute("disabled"), "play button should be enabled");
    }

    [Fact]
    public void SongCard_ShowsDisabledPlayButton_WhenSongHasNoMusic()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);
        song.SetHasMusic(false);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        var playButtons = cut.FindAll("button").Where(b => b.InnerHtml.Contains("Play"));
        playButtons.Should().Contain(b => b.HasAttribute("disabled"), "play button should be disabled when no music");
    }

    [Fact]
    public void SongCard_ShowsViewLyricButton()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("bi-file-text-fill", "should show lyric button icon");
        cut.Markup.Should().Contain("Letra", "should show lyric button label");
    }

    [Fact]
    public void SongCard_ShowsEnabledLinksButton_WhenLinksPresent()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);
        song.SetSpotifyUrl("https://spotify.com/song");

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        var linksButtons = cut.FindAll("button").Where(b => b.InnerHtml.Contains("Links"));
        linksButtons.Should().Contain(b => !b.HasAttribute("disabled"), "links button should be enabled when links present");
    }

    [Fact]
    public void SongCard_ShowsDisabledLinksButton_WhenNoLinksPresent()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        var linksButtons = cut.FindAll("button").Where(b => b.InnerHtml.Contains("Links"));
        linksButtons.Should().Contain(b => b.HasAttribute("disabled"), "links button should be disabled when no links");
    }

    [Fact]
    public void SongCard_ShowsAdminButtons_WhenUserIsOwner()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, true));

        // Assert
        cut.Markup.Should().Contain("song-admin-overlay", "admin overlay should appear");
        cut.Markup.Should().Contain("bi-pencil", "edit button should appear");
        cut.Markup.Should().Contain("bi-trash", "delete button should appear");
        cut.Markup.Should().Contain("music-btn-edit", "edit button should have white style");
        cut.Markup.Should().Contain("music-btn-delete", "delete button should have red style");
    }

    [Fact]
    public void SongCard_DoesNotShowAdminButtons_WhenUserIsNotOwner()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().NotContain("song-admin-overlay", "admin overlay should not appear for non-owner");
    }

    [Fact]
    public void SongCard_HasCardClasses()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("card", "should have card class");
        cut.Markup.Should().Contain("song-card", "should have song-card class");
    }

    [Fact]
    public void SongCard_HasMusicIconHeader()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);

        // Act
        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("song-icon-circle", "should have icon circle");
        cut.Markup.Should().Contain("bi-music-note-beamed", "should have music note icon");
    }

    [Fact]
    public void SongCard_InvokesOnPlay_WhenPlayButtonClicked()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);
        song.SetHasMusic(true);
        bool callbackInvoked = false;

        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false)
            .Add(p => p.OnPlay, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var playButton = cut.FindAll("button").Where(b => b.InnerHtml.Contains("Play")).First(b => !b.HasAttribute("disabled"));
        playButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnPlay callback should be invoked");
    }

    [Fact]
    public void SongCard_InvokesOnViewLyric_WhenLyricButtonClicked()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);
        bool callbackInvoked = false;

        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false)
            .Add(p => p.OnViewLyric, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var lyricButton = cut.FindAll("button").First(b => b.InnerHtml.Contains("Letra"));
        lyricButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnViewLyric callback should be invoked");
    }

    [Fact]
    public void SongCard_InvokesOnViewLinks_WhenLinksButtonClicked()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);
        song.SetSpotifyUrl("https://spotify.com/song");
        bool callbackInvoked = false;

        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, false)
            .Add(p => p.OnViewLinks, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var linksButton = cut.FindAll("button").Where(b => b.InnerHtml.Contains("Links")).First(b => !b.HasAttribute("disabled"));
        linksButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnViewLinks callback should be invoked");
    }

    [Fact]
    public void SongCard_InvokesOnEdit_WhenEditButtonClicked()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);
        bool callbackInvoked = false;

        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, true)
            .Add(p => p.OnEdit, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var editButton = cut.FindAll("button").First(b => b.ClassList.Contains("music-btn-edit"));
        editButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnEdit callback should be invoked");
    }

    [Fact]
    public void SongCard_InvokesOnDelete_WhenDeleteButtonClicked()
    {
        // Arrange
        var song = Song.Create("Favaios", 1);
        bool callbackInvoked = false;

        var cut = RenderComponent<SongCard>(parameters => parameters
            .Add(p => p.Song, song)
            .Add(p => p.IsOwner, true)
            .Add(p => p.OnDelete, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var deleteButton = cut.FindAll("button").First(b => b.ClassList.Contains("music-btn-delete"));
        deleteButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnDelete callback should be invoked");
    }
}
