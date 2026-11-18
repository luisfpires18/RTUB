// FakeReceiptStorage: test class moved here from TreasuryApiTests.cs.
using System.Collections.Concurrent;
using RTUB.Application.Interfaces;

namespace RTUB.Integration.Tests.Api;
/// <summary>An in-memory receipt bucket: URLs are fake public URLs; nothing reaches R2.</summary>
public sealed class FakeReceiptStorage : IReceiptStorageService
{
    private const string Base = "https://receipts.test/";

    public ConcurrentDictionary<string, long> Objects { get; } = new();

    /// <summary>Makes every delete throw, as an unreachable bucket would.</summary>
    public bool FailDeletes { get; set; }

    public string Put(string key)
    {
        Objects[Base + key] = 1;
        return Base + key;
    }

    public async Task<string> UploadReceiptAsync(Stream fileStream, string fileName, string contentType, int transactionId)
    {
        using var copy = new MemoryStream();
        await fileStream.CopyToAsync(copy);
        var url = $"{Base}receipts/test/{transactionId}_{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        Objects[url] = copy.Length;
        return url;
    }

    public Task DeleteReceiptAsync(string receiptUrl)
    {
        if (FailDeletes)
        {
            throw new IOException("storage unavailable (test)");
        }

        Objects.TryRemove(receiptUrl, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ReceiptExistsAsync(string receiptUrl) => Task.FromResult(Objects.ContainsKey(receiptUrl));
}
