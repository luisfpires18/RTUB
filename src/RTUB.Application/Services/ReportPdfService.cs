using Microsoft.Extensions.Caching.Memory;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Service for generating PDF reports using QuestPDF with caching support.
/// </summary>
public partial class ReportPdfService
{
    private readonly TimeProvider _time;

    /// <summary>The "Gerado em" stamp: Portugal's wall clock (#226 F02; was the server's DateTime.Now, UTC on Azure).</summary>
    internal string GeneratedAt() => PortugalTime.Now(_time).ToString("dd/MM/yyyy HH:mm");

    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    public ReportPdfService(IMemoryCache cache, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _cache = cache;

        // Cache generated PDFs for 1 hour (they don't change frequently)
        _cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            SlidingExpiration = TimeSpan.FromMinutes(30),
            Size = 1 // Size is used for eviction policy
        };
    }
}
