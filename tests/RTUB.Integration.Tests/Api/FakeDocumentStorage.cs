using System.Collections.Concurrent;
using FluentAssertions;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// An in-memory S3-like bucket with the listing semantics of <c>CloudflareDocumentStorageService</c>: "folder/" marker
/// objects, delimiter listing in ordinal key order, documents by file name. Pre-signed URLs are fake strings.
/// </summary>
public sealed class FakeDocumentStorage : IDocumentStorageService
{
    /// <summary>ETag: a fresh value per upload (different content), kept by a copy (same content).</summary>
    public sealed record StoredObject(long Size, string ContentType, string ETag = "");

    public ConcurrentDictionary<string, StoredObject> Objects { get; } = new();

    /// <summary>Seeds an object; a key ending in "/" is a folder marker. Parent folder markers are added too.</summary>
    public void Put(string key, long size, string contentType = "application/pdf", string? etag = null)
    {
        Objects[key] = new StoredObject(size, contentType, etag ?? Guid.NewGuid().ToString("N"));
        for (var i = key.IndexOf('/', "docs/".Length); i > 0 && i < key.Length - 1; i = key.IndexOf('/', i + 1))
        {
            Objects.TryAdd(key[..(i + 1)], new StoredObject(0, "application/x-directory"));
        }
    }

    public Task<string?> GetDocumentUrlAsync(string documentPath, bool forceDownload = false) =>
        Task.FromResult(Objects.ContainsKey(documentPath) ? $"https://r2.test/{documentPath}?download={forceDownload}" : null);

    public Task<bool> DocumentExistsAsync(string documentPath) => Task.FromResult(Objects.ContainsKey(documentPath));

    public Task<List<string>> ListFoldersAsync(string prefix = "docs/") => ListSubfoldersAsync($"{prefix}Test/");

    /// <summary>How many subfolder (delimiter) listings were asked for.</summary>
    public int SubfolderCalls;

    /// <summary>How many whole-prefix listings were asked for (#226 F01: one per fiscal year, then memory).</summary>
    public int TreeCalls;

    /// <summary>Every whole-prefix listing fails (null, what the real service returns on an R2 error) while set.</summary>
    public bool Broken;

    public Task<List<DocumentMetadata>?> ListTreeAsync(string prefix)
    {
        Interlocked.Increment(ref TreeCalls);
        return Task.FromResult(Broken ? null : Objects
            .Where(o => o.Key.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(o => o.Key, StringComparer.Ordinal)
            .Select(o => new DocumentMetadata
            {
                FileName = Path.GetFileName(o.Key),
                FilePath = o.Key,
                SizeBytes = o.Value.Size,
                LastModified = DateTime.UtcNow,
                Extension = Path.GetExtension(o.Key),
            })
            .ToList());
    }

    /// <summary>Every storage round trip that lists something.</summary>
    public int AllListings => ListCalls + SubfolderCalls + TreeCalls;

    public Task<List<string>> ListSubfoldersAsync(string folderPath)
    {
        Interlocked.Increment(ref SubfolderCalls);
        var prefix = folderPath.EndsWith('/') ? folderPath : folderPath + "/";
        return Task.FromResult(Objects.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && k.IndexOf('/', prefix.Length) > prefix.Length)
            .Select(k => k[prefix.Length..k.IndexOf('/', prefix.Length)])
            .Distinct()
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList());
    }

    /// <summary>How many folder listings were asked for (each one is a storage round trip).</summary>
    public int ListCalls;

    public Task<List<DocumentMetadata>> ListDocumentsInFolderAsync(string folderPath)
    {
        Interlocked.Increment(ref ListCalls);
        var prefix = folderPath.EndsWith('/') ? folderPath : folderPath + "/";
        return Task.FromResult(Objects
            .Where(o => o.Key.StartsWith(prefix, StringComparison.Ordinal) && o.Key.Length > prefix.Length && o.Key.IndexOf('/', prefix.Length) < 0)
            .Select(o => new DocumentMetadata
            {
                FileName = Path.GetFileName(o.Key),
                FilePath = o.Key,
                SizeBytes = o.Value.Size,
                LastModified = DateTime.UtcNow,
                Extension = Path.GetExtension(o.Key),
            })
            .OrderBy(d => d.FileName)
            .ToList());
    }

    public async Task<string> UploadDocumentAsync(string folderPath, string fileName, Stream fileStream, string contentType)
    {
        var prefix = folderPath.EndsWith('/') ? folderPath : folderPath + "/";
        using var copy = new MemoryStream();
        await fileStream.CopyToAsync(copy);
        Put(prefix + fileName, copy.Length, contentType);
        return prefix + fileName;
    }

    public Task CreateFolderAsync(string folderPath)
    {
        Put(folderPath.EndsWith('/') ? folderPath : folderPath + "/", 0, "application/x-directory");
        return Task.CompletedTask;
    }

    public Task<long> GetFileSizeAsync(string documentPath) =>
        Task.FromResult(Objects.TryGetValue(documentPath, out var o) ? o.Size : 0);

    /// <summary>Set by a test: a write ("copy" or "delete") on a matching key throws, like an R2 outage.</summary>
    public Func<string, string, bool>? Fails;

    private void Check(string operation, string key)
    {
        if (Fails?.Invoke(operation, key) == true)
        {
            throw new InvalidOperationException($"Storage {operation} failed for {key}");
        }
    }

    public Task DeleteDocumentAsync(string documentPath)
    {
        Check("delete", documentPath);
        Objects.TryRemove(documentPath, out _);
        return Task.CompletedTask;
    }

    public Task CopyDocumentAsync(string sourcePath, string targetPath)
    {
        Check("copy", sourcePath);
        if (!Objects.TryGetValue(sourcePath, out var source))
        {
            throw new InvalidOperationException($"No object {sourcePath}");
        }

        Put(targetPath, source.Size, source.ContentType, source.ETag);
        return Task.CompletedTask;
    }

    public Task<string?> GetDocumentETagAsync(string documentPath) =>
        Task.FromResult(Objects.TryGetValue(documentPath, out var o) ? o.ETag : null);

    public Task DeleteFolderAsync(string folderPath)
    {
        var prefix = folderPath.EndsWith('/') ? folderPath : folderPath + "/";
        foreach (var key in Objects.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            Objects.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
