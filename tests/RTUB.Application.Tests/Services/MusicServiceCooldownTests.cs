using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// The play cooldown (React track 006) is the server's: a listener's repeat inside the song's
/// duration - 30 s when unknown - plays but is not counted, and counts again once it has passed.
/// The integration tests prove the refusal through HTTP; these prove the window, on a test clock.
/// </summary>
public class MusicServiceCooldownTests : IClassFixture<DatabaseFixture>
{
    private static readonly ClaimsPrincipal Visitor = new(new ClaimsIdentity());

    private readonly DatabaseFixture _db;
    private readonly TestClock _clock = new();
    private readonly Mock<ISongService> _songs = new();
    private readonly MusicService _service;

    public MusicServiceCooldownTests(DatabaseFixture db)
    {
        _db = db;
        var audio = new Mock<IAudioStorageService>();
        audio.Setup(a => a.GetAudioUrlAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
            .ReturnsAsync("https://test-account-id.r2.cloudflarestorage.com/a.mp3");

        _service = new MusicService(
            new Contexts(db),
            Mock.Of<IAlbumService>(),
            _songs.Object,
            audio.Object,
            Mock.Of<ILyricStorageService>(),
            Mock.Of<IImageStorageService>(),
            Mock.Of<ISongContentService>(),
            Mock.Of<IAuditLogService>(),
            new MemoryCache(new MemoryCacheOptions { Clock = _clock }),
            NullLogger<MusicService>.Instance);
    }

    [Fact]
    public async Task ARepeatInsideTheSongsDuration_IsNotCounted_ThenCountsOnceItHasPassed()
    {
        var song = await SeedSongAsync(duration: 10);

        (await PlayAsync(song, "1.1.1.1")).Should().BeTrue();
        _clock.Advance(TimeSpan.FromSeconds(9));
        (await PlayAsync(song, "1.1.1.1")).Should().BeFalse();
        _clock.Advance(TimeSpan.FromSeconds(2));
        (await PlayAsync(song, "1.1.1.1")).Should().BeTrue();

        _songs.Verify(s => s.IncrementPlayCountAsync(song, null), Times.Exactly(2));
    }

    [Fact]
    public async Task WithoutADuration_TheCooldownIs30Seconds()
    {
        var song = await SeedSongAsync(duration: null);

        (await PlayAsync(song, "2.2.2.2")).Should().BeTrue();
        _clock.Advance(TimeSpan.FromSeconds(29));
        (await PlayAsync(song, "2.2.2.2")).Should().BeFalse();
        _clock.Advance(TimeSpan.FromSeconds(2));
        (await PlayAsync(song, "2.2.2.2")).Should().BeTrue();
    }

    [Fact]
    public async Task TheCooldownBelongsToOneListener()
    {
        var song = await SeedSongAsync(duration: null);

        (await PlayAsync(song, "3.3.3.3")).Should().BeTrue();
        (await PlayAsync(song, "4.4.4.4")).Should().BeTrue("another address is another listener");
        (await PlayAsync(song, "3.3.3.3")).Should().BeFalse();
    }

    private async Task<bool> PlayAsync(int songId, string address)
    {
        var result = await _service.PlayAsync(songId, Visitor, address);
        result.Value.Should().NotBeNull("the song is public and has audio");
        return result.Value!.Counted;
    }

    private async Task<int> SeedSongAsync(int? duration)
    {
        await using var ctx = _db.CreateContext();
        var album = Album.Create($"Álbum {Guid.NewGuid():N}", 2000);
        ctx.Albums.Add(album);
        await ctx.SaveChangesAsync();
        var song = Song.Create("Faixa", album.Id, 1, duration: duration, hasMusic: true);
        ctx.Songs.Add(song);
        await ctx.SaveChangesAsync();
        return song.Id;
    }

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }

    private sealed class TestClock : Microsoft.Extensions.Internal.ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UtcNow;

        public void Advance(TimeSpan by) => UtcNow += by;
    }
}
