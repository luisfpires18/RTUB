using System.Collections.Concurrent;
using RTUB.Application.Interfaces;

namespace RTUB.Integration.Tests.Api;

public sealed class FakeMusicStorage : IAudioStorageService, ILyricStorageService, IImageStorageService, ISongVideoStorageService
{
    private const string Signed = "https://test-account-id.r2.cloudflarestorage.com/test-bucket";

    public ConcurrentDictionary<(string Album, string Song), string> Pdfs { get; } = new();
    public ConcurrentBag<string> MissingAudio { get; } = new();
    public ConcurrentBag<string> UploadedImages { get; } = new();
    public ConcurrentBag<string> DeletedImages { get; } = new();
    public ConcurrentBag<string> DeletedVideos { get; } = new();

    public Task<string?> GetAudioUrlAsync(string albumTitle, int? trackNumber, string songTitle) =>
        Task.FromResult(MissingAudio.Contains(songTitle) ? null : $"{Signed}/audio/{Guid.NewGuid():N}.mp3?X-Amz-Signature=x");

    public Task<bool> AudioFileExistsAsync(string albumTitle, int? trackNumber, string songTitle) => Task.FromResult(!MissingAudio.Contains(songTitle));

    public Task<string?> GetLyricPdfUrlAsync(string albumTitle, string songTitle) =>
        Task.FromResult(Pdfs.TryGetValue((albumTitle, songTitle), out var url) ? url : null);

    public Task<bool> LyricPdfExistsAsync(string albumTitle, string songTitle) => Task.FromResult(Pdfs.ContainsKey((albumTitle, songTitle)));

    /// <summary>An album with this in its title gets a storage failure on cover upload.</summary>
    public const string FailingTitle = "Falha de armazenamento";

    public Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string entityType, string entityId)
    {
        if (entityId.StartsWith("falha_de_armazenamento", StringComparison.Ordinal))
        {
            throw new HttpRequestException("storage unreachable (test)");
        }

        var url = $"https://pub-test.r2.dev/images/{entityType}/{entityId}-{Guid.NewGuid():N}.webp";
        UploadedImages.Add(url);
        return Task.FromResult(url);
    }

    public Task DeleteImageAsync(string imageUrl)
    {
        DeletedImages.Add(imageUrl);
        return Task.CompletedTask;
    }

    public Task<bool> ImageExistsAsync(string imageUrl) => Task.FromResult(UploadedImages.Contains(imageUrl));

    public Task<string> UploadVideoAsync(Stream fileStream, string fileName, string contentType, int songId) =>
        Task.FromResult($"https://pub-test.r2.dev/videos/{songId}/{Guid.NewGuid():N}.mp4");

    public Task DeleteVideoAsync(string videoUrl)
    {
        DeletedVideos.Add(videoUrl);
        return Task.CompletedTask;
    }

    public Task<bool> VideoExistsAsync(string videoUrl) => Task.FromResult(true);
}
