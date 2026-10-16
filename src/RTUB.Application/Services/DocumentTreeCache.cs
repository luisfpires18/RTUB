using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// #226 F01: one storage listing per fiscal year ("docs/{environment}/{year}/", every object at any depth), shared by
/// every Documentation request for <see cref="Lifetime"/>. It holds storage metadata only - never a user's filtered view,
/// an authorization decision or a pre-signed URL: each request still filters the folders it may see.
/// Every writer under docs/ calls <see cref="Forget"/> after its write (Documentation, Logistics board files, Meetings
/// ata PDFs), so the next read lists again. A failed listing is never kept. One App Service instance: memory is enough.
/// </summary>
public sealed class DocumentTreeCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private readonly IMemoryCache _cache;

    public DocumentTreeCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>Every object under <paramref name="yearPath"/>, from memory when listed in the last 30 s; null when storage failed.</summary>
    public async Task<IReadOnlyList<DocumentMetadata>?> GetAsync(IDocumentStorageService storage, string yearPath)
    {
        if (_cache.TryGetValue(TreeKey(yearPath), out IReadOnlyList<DocumentMetadata>? tree))
        {
            return tree;
        }

        // A write that lands while this listing is in flight bumps the generation: the (maybe older) result is returned
        // to this request but not kept for the next ones.
        var generation = Generation(yearPath).Value;
        var listed = await storage.ListTreeAsync(yearPath);
        if (listed is not null && Volatile.Read(ref Generation(yearPath).Value) == generation)
        {
            _cache.Set(TreeKey(yearPath), (IReadOnlyList<DocumentMetadata>)listed, Lifetime);
        }

        return listed;
    }

    /// <summary>Drops the listing of the fiscal year a written key or folder belongs to ("docs/{environment}/{year}/…").</summary>
    public void Forget(string path)
    {
        if (YearPathOf(path) is not { } yearPath)
        {
            return;
        }

        Interlocked.Increment(ref Generation(yearPath).Value);
        _cache.Remove(TreeKey(yearPath));
    }

    internal static string? YearPathOf(string path)
    {
        var parts = path.Split('/');
        return parts.Length >= 4 && parts[0] == "docs" && parts[1] != "" && parts[2] != "" ? $"docs/{parts[1]}/{parts[2]}/" : null;
    }

    private static string TreeKey(string yearPath) => $"documentation-tree:{yearPath}";

    private readonly object _gate = new();

    // GetOrCreate is not atomic: the lock makes every caller share one counter per year.
    private StrongBox<int> Generation(string yearPath)
    {
        lock (_gate)
        {
            return _cache.GetOrCreate($"documentation-tree-generation:{yearPath}", entry =>
            {
                entry.Priority = CacheItemPriority.NeverRemove;
                return new StrongBox<int>();
            })!;
        }
    }
}
