using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Service for inventory operations
/// Handles business logic for using and managing inventory items
/// </summary>
public partial class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<InventoryService> _logger;
    private readonly IOptionsSnapshot<MyTunoScalingConfiguration> _scalingOptions;
    private MyTunoScalingConfiguration _scalingConfig => _scalingOptions.Value;
    private GatheringConfig _gatheringConfig => _scalingConfig.Gathering;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IMemoryCache _cache;

    // Per-user lock to prevent multi-tab energy exploits (race conditions on read-modify-write).
    // Static so it is shared across all scoped InventoryService instances (one per Blazor circuit).
    // SemaphoreSlim is ~100 bytes; intentionally not evicted because a live WaitAsync caller holds
    // a reference to the object — evicting it from a cache would not free it, and could create a
    // second lock for the same user if the entry were re-created before the waiter releases.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _userEnergyLocks = new();
    private static SemaphoreSlim GetUserEnergyLock(string userId) => _userEnergyLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

    // Cache key prefix for last-gather timestamps stored in IMemoryCache (bounded, auto-evicting).
    private const string GatherTimeCacheKeyPrefix = "gather:";
    private static readonly MemoryCacheEntryOptions _gatherCacheOptions =
        new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(1));

    // Fino heals 25% of total HP
    private const double FinoHealPercentage = 0.25;
    // Caneca heals 50% of total HP
    private const double CanecaHealPercentage = 0.50;
    // Cigarro grants +10% dodge for N runs
    private const int CigarroBuffRuns = 5;
    // Canhão grants AOE attacks for N runs
    // Canhão and Penalty are now timed — durations configured in scaling.config.json

    public InventoryService(
        IInventoryRepository inventoryRepository,
        ICharacterRepository characterRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<InventoryService> logger,
        IOptionsSnapshot<MyTunoScalingConfiguration> config,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IMemoryCache cache)
    {
        _inventoryRepository = inventoryRepository;
        _characterRepository = characterRepository;
        _userManager = userManager;
        _logger = logger;
        _scalingOptions = config;
        _contextFactory = contextFactory;
        _cache = cache;
    }
}
