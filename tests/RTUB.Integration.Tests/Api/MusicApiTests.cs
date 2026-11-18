using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/music (React track 006) through the real host: real Identity roles, the real login, real
/// antiforgery, the real database. Storage is faked - no test reaches Cloudflare R2 or uploads a file.
/// </summary>
public partial class MusicApiTests : IClassFixture<MusicApiFactory>
{
    private readonly MusicApiFactory _factory;

    public MusicApiTests(MusicApiFactory factory)
    {
        _factory = factory;
    }

    private static object Chart(int version = 1, double bpm = 80, string chord = "F") => new
    {
        version,
        duration = 10.0,
        bpm,
        beats = new[] { 0.5, 1.25, 2.0 },
        extra = "dropped",
        sections = new[]
        {
            new
            {
                name = "Verso 1",
                kind = "verse",
                start = 0.5,
                end = 4.0,
                lines = new[]
                {
                    new
                    {
                        text = "Quis ser caçador",
                        start = 0.5,
                        end = 4.0,
                        words = new[] { "Quis", "ser", "caçador" },
                        wordTimes = new[] { 0.5, 1.0, 1.5 },
                        chords = new[] { new { label = "FÁ", name = chord, time = 0.5, word = 0, @char = 0 } },
                    },
                },
            },
        },
    };

    private sealed record Seed(
        int PublicAlbum, int PrivateAlbum, int ExclusiveAlbum, int ExclusivePublicAlbum, int StaleAccessAlbum,
        int SongWithPdf, int SongWithText, int SongWithout, int SongMissingFile, int PrivateSong, int ExclusiveSong);

    /// <summary>A fresh, uniquely named set of albums for one test (the database is shared by the class).</summary>
    private async Task<Seed> SeedAsync(string? accessFor = null)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Album NewAlbum(string title, int? year, bool isPrivate = false, bool exclusive = false) =>
            Album.Create($"{title} {tag}", year, null, isPrivate, exclusive);

        var publicAlbum = NewAlbum("Público", 1995);
        var privateAlbum = NewAlbum("Privado", null, isPrivate: true);
        var exclusive = NewAlbum("Exclusivo", null, isPrivate: true, exclusive: true);
        var exclusivePublic = NewAlbum("Exclusivo aberto", 2000, exclusive: true);
        var stale = NewAlbum("Antigo exclusivo", null, isPrivate: true);
        db.Albums.AddRange(publicAlbum, privateAlbum, exclusive, exclusivePublic, stale);
        await db.SaveChangesAsync();

        var withPdf = Song.Create("Abertura", publicAlbum.Id, 1, "Letrista", "Compositor", spotifyUrl: "https://open.spotify.com/track/abertura", hasMusic: true);
        withPdf.YouTubeUrls.Add(SongYouTubeUrl.Create(0, "https://www.youtube.com/watch?v=abertura"));
        withPdf.YouTubeUrls.Add(SongYouTubeUrl.Create(0, "javascript:alert(1)"));
        var withText = Song.Create("Serenata", publicAlbum.Id, 2, hasMusic: true);
        withText.SetLyrics("Letra guardada");
        var without = Song.Create("Sem faixa", publicAlbum.Id, null);
        var missing = Song.Create(MissingFileSong, publicAlbum.Id, 3, hasMusic: true);
        var privateSong = Song.Create("Privada", privateAlbum.Id, 1, hasMusic: true);
        var exclusiveSong = Song.Create("Exclusiva", exclusive.Id, 1, hasMusic: true);
        db.Songs.AddRange(without, withText, withPdf, missing, privateSong, exclusiveSong);
        await db.SaveChangesAsync();

        if (accessFor is not null)
        {
            db.AlbumAccesses.Add(AlbumAccess.Create(exclusive.Id, accessFor));
        }

        // Left over from when the album was exclusive: must not matter any more.
        var someone = await db.Users.Select(u => u.Id).FirstAsync();
        db.AlbumAccesses.Add(AlbumAccess.Create(stale.Id, someone));
        await db.SaveChangesAsync();

        _factory.Storage.Pdfs[(publicAlbum.Title, withPdf.Title)] = "https://test-account-id.r2.cloudflarestorage.com/test-bucket/lyrics/abertura.pdf?X-Amz-Signature=x";
        _factory.Storage.MissingAudio.Add(missing.Title);

        return new Seed(publicAlbum.Id, privateAlbum.Id, exclusive.Id, exclusivePublic.Id, stale.Id,
            withPdf.Id, withText.Id, without.Id, missing.Id, privateSong.Id, exclusiveSong.Id);
    }

    private async Task AddPlaysAsync(int songId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.Select(u => u.Id).FirstAsync();
        for (var i = 0; i < count; i++)
        {
            db.SongPlayCounts.Add(new SongPlayCount { SongId = songId, UserId = user, PlayedAt = DateTime.UtcNow });
        }

        await db.SaveChangesAsync();
    }

    private async Task<int> PlaysAsync(int songId, string? userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.SongPlayCounts.CountAsync(p => p.SongId == songId && p.UserId == userId);
    }

    private async Task<Album> AlbumAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Albums.AsNoTracking().FirstAsync(a => a.Id == id);
    }

    private async Task<bool> AlbumExistsAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Albums.AnyAsync(a => a.Id == id);
    }

    private async Task<List<string>> AccessAsync(int albumId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AlbumAccesses
            .Where(a => a.AlbumId == albumId).Select(a => a.UserId).ToListAsync();
    }

    private static List<int> AlbumIds(JsonElement list) =>
        list.GetProperty("albums").EnumerateArray().Select(a => a.GetProperty("id").GetInt32()).ToList();

    private const string MissingFileSong = "Ficheiro em falta";

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string role)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"music-{role.ToLowerInvariant()}-{Guid.NewGuid():N}"[..30],
            $"10.62.{n / 250}.{n % 250 + 1}", role);
    }

    private Task<HttpClient> AnonymousAsync(string ip = "10.63.0.1")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, ip);
        return Task.FromResult(client);
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    private static MultipartFormDataContent AlbumForm(string title, string year = "", bool isPrivate = false, bool exclusive = false,
        string[]? access = null, HttpContent? cover = null, string coverType = "image/webp")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(title), "title" },
            { new StringContent(year), "year" },
            { new StringContent(string.Empty), "description" },
            { new StringContent(isPrivate ? "true" : "false"), "isPrivate" },
            { new StringContent(exclusive ? "true" : "false"), "isExclusive" },
        };
        foreach (var id in access ?? Array.Empty<string>())
        {
            form.Add(new StringContent(id), "accessUserIds");
        }

        if (cover is not null)
        {
            cover.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(coverType);
            form.Add(cover, "cover", "album-cover.webp");
        }

        return form;
    }

    /// <summary>Not a real image - storage is faked, the server only checks type and size.</summary>
    private static ByteArrayContent Webp() => new(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4 });

    private static MultipartFormDataContent VideoForm(string title, string type, string fileName = "clip.mp4")
    {
        var file = new ByteArrayContent(new byte[] { 0, 0, 0, 24, 0x66, 0x74, 0x79, 0x70 });
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type);
        return new MultipartFormDataContent { { file, "file", fileName }, { new StringContent(title), "title" } };
    }

    private static object SongBody(string title, string? spotify = null, int? track = null, string[]? youTube = null, string? lyrics = null) => new
    {
        title,
        trackNumber = track,
        lyricAuthor = "",
        musicAuthor = "",
        adaptation = "",
        spotifyUrl = spotify ?? "",
        hasAudio = true,
        youTubeUrls = youTube ?? Array.Empty<string>(),
        lyrics = lyrics ?? "",
    };
}
