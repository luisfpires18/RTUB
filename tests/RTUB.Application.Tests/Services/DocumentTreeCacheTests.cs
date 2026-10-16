using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>#226 F01: the shared per-year Documentation listing.</summary>
public class DocumentTreeCacheTests
{
    private const string Year = "docs/Production/2025-2026/";

    private static List<DocumentMetadata> Tree(params string[] keys) =>
        keys.Select(k => new DocumentMetadata { FilePath = k, FileName = Path.GetFileName(k) }).ToList();

    [Fact]
    public async Task OneListing_IsReused_UntilAWriteUnderThatYear()
    {
        var storage = new Mock<IDocumentStorageService>();
        storage.Setup(s => s.ListTreeAsync(It.IsAny<string>())).ReturnsAsync(() => Tree(Year + "A/x.pdf"));
        var cache = new DocumentTreeCache(new MemoryCache(new MemoryCacheOptions()));

        await cache.GetAsync(storage.Object, Year);
        await cache.GetAsync(storage.Object, Year);
        storage.Verify(s => s.ListTreeAsync(Year), Times.Once);

        cache.Forget("docs/Production/2024-2025/B/y.pdf");
        await cache.GetAsync(storage.Object, Year);
        storage.Verify(s => s.ListTreeAsync(Year), Times.Once, "a write in another year keeps this one");

        cache.Forget(Year + "A/");
        await cache.GetAsync(storage.Object, Year);
        storage.Verify(s => s.ListTreeAsync(Year), Times.Exactly(2));
    }

    [Fact]
    public async Task AFailedListing_IsNotKept()
    {
        var storage = new Mock<IDocumentStorageService>();
        storage.Setup(s => s.ListTreeAsync(Year)).ReturnsAsync((List<DocumentMetadata>?)null);
        var cache = new DocumentTreeCache(new MemoryCache(new MemoryCacheOptions()));

        (await cache.GetAsync(storage.Object, Year)).Should().BeNull();
        await cache.GetAsync(storage.Object, Year);
        storage.Verify(s => s.ListTreeAsync(Year), Times.Exactly(2));
    }

    [Fact]
    public async Task AListingOverlappingAWrite_IsReturnedButNotKept()
    {
        var pending = new TaskCompletionSource<List<DocumentMetadata>?>();
        var storage = new Mock<IDocumentStorageService>();
        storage.SetupSequence(s => s.ListTreeAsync(Year)).Returns(pending.Task).ReturnsAsync(Tree(Year + "A/new.pdf"));
        var cache = new DocumentTreeCache(new MemoryCache(new MemoryCacheOptions()));

        var inFlight = cache.GetAsync(storage.Object, Year);
        cache.Forget(Year + "A/new.pdf");
        pending.SetResult(Tree(Year + "A/old.pdf"));

        (await inFlight)!.Select(d => d.FileName).Should().Equal("old.pdf");
        (await cache.GetAsync(storage.Object, Year))!.Select(d => d.FileName).Should().Equal(new[] { "new.pdf" }, "the stale result was not stored");
    }

    [Theory]
    [InlineData("docs/Production/2025-2026/Atas/x.pdf", "docs/Production/2025-2026/")]
    [InlineData("docs/Production/2025-2026/Logistics/Jantar/", "docs/Production/2025-2026/")]
    [InlineData("docs/Production/2025-2026/", "docs/Production/2025-2026/")]
    [InlineData("docs/rtub_rgi.pdf", null)]
    [InlineData("images/x.png", null)]
    public void TheYearOfAWrittenPath(string path, string? year) =>
        DocumentTreeCache.YearPathOf(path).Should().Be(year);
}
